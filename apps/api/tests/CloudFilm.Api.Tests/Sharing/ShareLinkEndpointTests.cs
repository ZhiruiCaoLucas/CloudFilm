using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auditing;
using CloudFilm.Application.Auth;
using CloudFilm.Application.Grants;
using CloudFilm.Application.Sharing;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NSubstitute.ExceptionExtensions;

namespace CloudFilm.Api.Tests.Sharing;

/// <summary>
/// A host with the share link store, the audit log and the clock substituted.
/// </summary>
/// <remarks>
/// The store is faked so that what the endpoint hands it - a digest, never a
/// token - can be inspected. The clock is fixed so that expiry boundaries can be
/// tested exactly rather than approximately.
/// </remarks>
public sealed class ShareLinkApiFactory : WebApplicationFactory<Program>
{
    /// <summary>A clock the tests move on purpose.</summary>
    /// <remarks>
    /// Frozen, but anchored to the real instant rather than to a date written
    /// into the source. The token issuer resolves this same clock, and the
    /// bearer handler validates a token's lifetime against the system clock, so
    /// a fixed date would issue every test token already expired and every
    /// request would be refused before it reached an endpoint. Freezing at "now"
    /// keeps the expiry boundaries exact - the test and the endpoint read one
    /// instant - without pulling the two clocks apart.
    /// </remarks>
    public sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>Runs the operation and remembers whether it committed.</summary>
    public sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int Commits { get; private set; }

        public int Rollbacks { get; private set; }

        public void Reset()
        {
            Commits = 0;
            Rollbacks = 0;
        }

        public async Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);

            bool committed = false;

            try
            {
                TResult result = await operation(cancellationToken);

                Commits++;
                committed = true;

                return result;
            }
            finally
            {
                if (!committed)
                {
                    Rollbacks++;
                }
            }
        }
    }

    /// <summary>The study every test shares.</summary>
    public const string Study = "1.2.840.113564.1";

    /// <summary>Gets the substituted store.</summary>
    public IShareLinkStore Store { get; } = Substitute.For<IShareLinkStore>();

    /// <summary>Gets the substituted audit log.</summary>
    public IAuditLogService AuditLog { get; } = Substitute.For<IAuditLogService>();

    /// <summary>Gets the fixed clock.</summary>
    public FixedClock Clock { get; } = new();

    /// <summary>Gets the recording unit of work.</summary>
    public RecordingUnitOfWork UnitOfWork { get; } = new();

    /// <summary>The identifier the store answers with.</summary>
    public static Guid LinkId { get; } = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    /// <summary>Returns every double to its as-created state.</summary>
    public void ResetSubstitutes()
    {
        Store.ClearSubstitute(ClearOptions.All);
        AuditLog.ClearSubstitute(ClearOptions.All);
        UnitOfWork.Reset();
        Clock.UtcNow = DateTimeOffset.UtcNow;

        Store.CreateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(call => new ShareLinkSummary(
                LinkId,
                call.ArgAt<string>(0),
                call.ArgAt<string>(1),
                Clock.UtcNow,
                call.ArgAt<DateTimeOffset>(4),
                null));

        // Standing by default: the link exists and has not been withdrawn.
        Store.RevokeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ShareLinkSummary(
                LinkId,
                Study,
                GrantPermissions.Download,
                Clock.UtcNow.AddHours(-1),
                Clock.UtcNow.AddDays(1),
                null));

        AuditLog.RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            foreach (ServiceDescriptor descriptor in services
                .Where(service => service.ServiceType == typeof(IHostedService)
                    && service.ImplementationType?.Namespace?.StartsWith("CloudFilm", StringComparison.Ordinal) == true)
                .ToList())
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<IShareLinkStore>();
            services.AddScoped(_ => Store);

            services.RemoveAll<IAuditLogService>();
            services.AddScoped(_ => AuditLog);

            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(_ => UnitOfWork);

            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });
    }
}

/// <summary>
/// Who may issue and withdraw share links, and what issuing one hands over.
/// </summary>
public sealed class ShareLinkEndpointTests : IClassFixture<ShareLinkApiFactory>
{
    private const string _path = "/api/v1/share-links";

    private static readonly Guid _administrator = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly ShareLinkApiFactory _factory;

    public ShareLinkEndpointTests(ShareLinkApiFactory factory)
    {
        _factory = factory;

        _factory.ResetSubstitutes();
    }

    private HttpClient CreateClientWithRoles(params string[] roles)
    {
        HttpClient client = _factory.CreateClient();

        using IServiceScope scope = _factory.Services.CreateScope();
        IAccessTokenIssuer issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        AccessToken token = issuer.Issue(_administrator, "admin@example.test", "Seed Administrator", roles);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        return client;
    }

    private CreateShareLinkRequest ARequest(
        string? study = null,
        string? permission = null,
        DateTimeOffset? expiresAtUtc = null) =>
        new(
            study ?? ShareLinkApiFactory.Study,
            permission ?? GrantPermissions.Download,
            expiresAtUtc ?? _factory.Clock.UtcNow.AddDays(1));

    private async Task<CreatedShareLink> CreateAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(_path, ARequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<CreatedShareLink>())!;
    }

    // -----------------------------------------------------------------------
    // Who may manage share links.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AnAnonymousCallerIsNotAuthenticated()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage created = await client.PostAsJsonAsync(_path, ARequest());
        using HttpResponseMessage revoked = await client.DeleteAsync($"{_path}/{ShareLinkApiFactory.LinkId}");

        created.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        revoked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await _factory.Store.DidNotReceiveWithAnyArgs()
            .CreateAsync(default!, default!, default!, default, default, default);
        await _factory.Store.DidNotReceiveWithAnyArgs().RevokeAsync(default, default);
    }

    /// <summary>
    /// Handing out a credential that reaches a patient's imaging is not clinical
    /// work.
    /// </summary>
    [Theory]
    [InlineData(RoleNames.Clinician)]
    public async Task AClinicianIsRefused(string role)
    {
        using HttpClient client = CreateClientWithRoles(role);

        using HttpResponseMessage created = await client.PostAsJsonAsync(_path, ARequest());
        using HttpResponseMessage revoked = await client.DeleteAsync($"{_path}/{ShareLinkApiFactory.LinkId}");

        created.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        revoked.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await _factory.Store.DidNotReceiveWithAnyArgs()
            .CreateAsync(default!, default!, default!, default, default, default);
        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    [Fact]
    public async Task AnAccountWithNoApplicationRoleIsRefused()
    {
        using HttpClient client = CreateClientWithRoles();

        using HttpResponseMessage created = await client.PostAsJsonAsync(_path, ARequest());
        using HttpResponseMessage revoked = await client.DeleteAsync($"{_path}/{ShareLinkApiFactory.LinkId}");

        created.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        revoked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnAdministratorMayIssueALink()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(_path, ARequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().EndWith(ShareLinkApiFactory.LinkId.ToString());
    }

    // -----------------------------------------------------------------------
    // What a create refuses.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The permission is stored as sent, because the routes that read it back
    /// decide what it means. A link recorded as Download when View was asked for
    /// would hand a recipient the study file.
    /// </summary>
    [Theory]
    [InlineData(GrantPermissions.View)]
    [InlineData(GrantPermissions.Download)]
    public async Task IssuesALinkCarryingThePermissionThatWasAskedFor(string permission)
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            _path,
            ARequest(permission: permission));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // What the store was told, character for character.
        await _factory.Store
            .Received(1)
            .CreateAsync(
                ShareLinkApiFactory.Study,
                permission,
                Arg.Any<byte[]>(),
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>());

        CreatedShareLink? created = await response.Content.ReadFromJsonAsync<CreatedShareLink>();

        // And what the administrator is told, which must be the same thing: they
        // are about to send this link to somebody on the strength of it.
        created!.Permission.Should().Be(permission);
        created.StudyInstanceUid.Should().Be(ShareLinkApiFactory.Study);
    }

    /// <summary>
    /// A view link is a credential like any other, and arrives the same way: once,
    /// in the response that created it.
    /// </summary>
    [Fact]
    public async Task AViewLinkReturnsItsTokenLikeADownloadLinkDoes()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            _path,
            ARequest(permission: GrantPermissions.View));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        CreatedShareLink? created = await response.Content.ReadFromJsonAsync<CreatedShareLink>();

        created!.Token.Should().NotBeNullOrWhiteSpace();

        // 32 bytes, Base64Url, unpadded - the shape every share token has.
        created.Token.Length.Should().Be(43);
        created.Token.Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    /// <summary>
    /// Only the two permissions the ladder can evaluate. Anything else - a
    /// different spelling, a set name, nothing at all - is refused, because a
    /// stored value no downstream route recognises is a link that would fail
    /// somewhere less obvious than here.
    /// </summary>
    [Theory]
    [InlineData("All")]
    [InlineData("download")]
    [InlineData("view")]
    [InlineData("Downloads")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefusesAnUnrecognisedPermission(string permission)
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            _path,
            ARequest(permission: permission));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _factory.Store.DidNotReceiveWithAnyArgs()
            .CreateAsync(default!, default!, default!, default, default, default);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefusesAStudyIdentifierItCannotStore(string study)
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(_path, ARequest(study: study));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefusesAStudyIdentifierLongerThanTheColumn()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            _path,
            ARequest(study: new string('1', ShareLinkLimits.MaxStudyInstanceUidLength + 1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A missing expiry deserialises to the year one, so absent and impossible
    /// are the same answer.
    /// </summary>
    [Fact]
    public async Task RefusesARequestWithNoExpiry()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(_path, new
        {
            studyInstanceUid = ShareLinkApiFactory.Study,
            permission = GrantPermissions.Download,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _factory.Store.DidNotReceiveWithAnyArgs()
            .CreateAsync(default!, default!, default!, default, default, default);
    }

    [Fact]
    public async Task RefusesAnExpiryInThePast()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            _path,
            ARequest(expiresAtUtc: _factory.Clock.UtcNow.AddSeconds(-1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A link expiring exactly now is already dead when it is issued.</summary>
    [Fact]
    public async Task RefusesAnExpiryOfExactlyNow()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            _path,
            ARequest(expiresAtUtc: _factory.Clock.UtcNow));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefusesAnExpiryBeyondTheCeiling()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            _path,
            ARequest(expiresAtUtc: _factory.Clock.UtcNow + ShareLinkLimits.MaximumLifetime + TimeSpan.FromSeconds(1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The ceiling is inclusive: exactly seven days is allowed.</summary>
    [Fact]
    public async Task AcceptsAnExpiryOfExactlyTheCeiling()
    {
        DateTimeOffset expiry = _factory.Clock.UtcNow + ShareLinkLimits.MaximumLifetime;

        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            _path,
            ARequest(expiresAtUtc: expiry));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await _factory.Store.Received(1).CreateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Any<Guid>(),
            expiry,
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A caller on another clock, or in another zone, buys itself nothing: the
    /// comparison happens in UTC.
    /// </summary>
    [Fact]
    public async Task ComparesTheExpiryInUtcWhateverOffsetWasSent()
    {
        // The same instant as "tomorrow", written as a wall-clock reading in a
        // +08:00 zone. The DateTime has to be Unspecified: a Utc-kinded value
        // paired with a non-zero offset is a contradiction, and DateTimeOffset
        // rejects it rather than guessing which half the caller meant.
        DateTime wallClockInPlusEight = DateTime.SpecifyKind(
            _factory.Clock.UtcNow.UtcDateTime.AddDays(1).AddHours(8),
            DateTimeKind.Unspecified);

        DateTimeOffset expiry = new(wallClockInPlusEight, TimeSpan.FromHours(8));

        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            _path,
            ARequest(expiresAtUtc: expiry));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // -----------------------------------------------------------------------
    // The token.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A structural check, not a statistical one: one sample cannot demonstrate
    /// entropy. What is asserted is that the token decodes to the full 32 bytes
    /// the generator was asked for, and that two issues differ - which is what
    /// would break if somebody swapped in a counter, a GUID, or a shortened
    /// value.
    /// </summary>
    [Fact]
    public async Task IssuesATokenOfThirtyTwoDecodedBytes()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        CreatedShareLink created = await CreateAsync(client);

        WebEncoders.Base64UrlDecode(created.Token).Should().HaveCount(32);
    }

    [Fact]
    public async Task IssuesADifferentTokenEveryTime()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        CreatedShareLink first = await CreateAsync(client);
        CreatedShareLink second = await CreateAsync(client);

        second.Token.Should().NotBe(first.Token);
    }

    /// <summary>
    /// The store is handed the digest and never the token. This is the single
    /// most important assertion in the suite: everything downstream - the row,
    /// the backup, the replica - only ever sees what is passed here.
    /// </summary>
    [Fact]
    public async Task HandsTheStoreTheDigestAndNeverTheToken()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        CreatedShareLink created = await CreateAsync(client);

        byte[] expected = SHA256.HashData(Encoding.UTF8.GetBytes(created.Token));

        await _factory.Store.Received(1).CreateAsync(
            ShareLinkApiFactory.Study,
            GrantPermissions.Download,
            Arg.Is<byte[]>(hash => hash.Length == 32 && hash.SequenceEqual(expected)),
            _administrator,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());

        await _factory.Store.DidNotReceive().CreateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<byte[]>(hash => Encoding.UTF8.GetString(hash) == created.Token),
            Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Who is recorded as issuing.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The request has nowhere to name an issuer, so a body that tries to is
    /// simply not read.
    /// </summary>
    [Fact]
    public async Task IgnoresAnIssuerTheClientTriesToName()
    {
        Guid impersonated = Guid.Parse("99999999-9999-9999-9999-999999999999");

        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(_path, new
        {
            studyInstanceUid = ShareLinkApiFactory.Study,
            permission = GrantPermissions.Download,
            expiresAtUtc = _factory.Clock.UtcNow.AddDays(1),
            createdByUserId = impersonated,
            token = "chosen-by-the-client",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await _factory.Store.Received(1).CreateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            _administrator,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());

        CreatedShareLink created = (await response.Content.ReadFromJsonAsync<CreatedShareLink>())!;

        created.Token.Should().NotBe("chosen-by-the-client");
    }

    // -----------------------------------------------------------------------
    // Audit.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RecordsExactlyOneEventWhenALinkIsIssued()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        CreatedShareLink created = await CreateAsync(client);

        await _factory.AuditLog.Received(1).RecordAsync(
            Arg.Is<AuditEntry>(entry =>
                entry.Action == AuditAction.ShareLinkCreated
                && entry.ResourceType == AuditResourceType.Study
                && entry.ResourceId == ShareLinkApiFactory.Study
                && entry.ActorUserId == _administrator),
            Arg.Any<CancellationToken>());

        await _factory.AuditLog.Received(1).RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());

        // Neither the token nor anything derived from it reaches the trail. The
        // resource id is a study, and there is no other field to hide one in.
        string digest = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(created.Token)));

        await _factory.AuditLog.DidNotReceive().RecordAsync(
            Arg.Is<AuditEntry>(entry =>
                entry.ResourceId != null
                && (entry.ResourceId.Contains(created.Token, StringComparison.Ordinal)
                    || entry.ResourceId.Contains(digest, StringComparison.Ordinal))),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Transaction.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task IssuingALinkCommitsOnce()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        await CreateAsync(client);

        _factory.UnitOfWork.Commits.Should().Be(1);
        _factory.UnitOfWork.Rollbacks.Should().Be(0);
    }

    /// <summary>
    /// An unrecorded credential to a patient's imaging is worse than a failed
    /// request, so the link goes back with the audit write - and the response
    /// carries neither the token nor the reason.
    /// </summary>
    [Fact]
    public async Task AnAuditFailureUnwindsTheIssue()
    {
        _factory.AuditLog.RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));

        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(_path, ARequest());

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        _factory.UnitOfWork.Commits.Should().Be(0);
        _factory.UnitOfWork.Rollbacks.Should().Be(1);

        string body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("audit unavailable");
        body.Should().NotContain("token");
        body.Should().NotContain(ShareLinkApiFactory.Study);
    }

    // -----------------------------------------------------------------------
    // Revocation.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WithdrawsTheLinkThatWasNamed()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.DeleteAsync($"{_path}/{ShareLinkApiFactory.LinkId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.Store.Received(1).RevokeAsync(ShareLinkApiFactory.LinkId, Arg.Any<CancellationToken>());

        await _factory.AuditLog.Received(1).RecordAsync(
            Arg.Is<AuditEntry>(entry =>
                entry.Action == AuditAction.ShareLinkRevoked
                && entry.ResourceType == AuditResourceType.Study
                && entry.ResourceId == ShareLinkApiFactory.Study
                && entry.ActorUserId == _administrator),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnswersNotFoundForALinkThatNeverExisted()
    {
        _factory.Store.RevokeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ShareLinkSummary?)null);

        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.DeleteAsync($"{_path}/{ShareLinkApiFactory.LinkId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    /// <summary>
    /// Withdrawing something already withdrawn changed nothing. The answer is the
    /// same either way, so repeating the request is safe and tells the caller
    /// nothing new.
    /// </summary>
    [Fact]
    public async Task WithdrawingTwiceIsSafeAndRecordedOnce()
    {
        _factory.Store.RevokeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ShareLinkSummary(
                ShareLinkApiFactory.LinkId,
                ShareLinkApiFactory.Study,
                GrantPermissions.Download,
                _factory.Clock.UtcNow.AddHours(-2),
                _factory.Clock.UtcNow.AddDays(1),
                _factory.Clock.UtcNow.AddHours(-1)));

        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.DeleteAsync($"{_path}/{ShareLinkApiFactory.LinkId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    [Fact]
    public async Task AnAuditFailureUnwindsAWithdrawal()
    {
        _factory.AuditLog.RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));

        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.DeleteAsync($"{_path}/{ShareLinkApiFactory.LinkId}");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        _factory.UnitOfWork.Commits.Should().Be(0);
        _factory.UnitOfWork.Rollbacks.Should().Be(1);
    }

    /// <summary>A withdrawal says nothing about the link beyond that it is gone.</summary>
    /// <remarks>
    /// The status is asserted as well as the body. A refused request also
    /// answers with an empty body, so a body-only assertion would be satisfied
    /// by an authentication failure - passing for the wrong reason is worse than
    /// failing.
    /// </remarks>
    [Fact]
    public async Task AWithdrawalReturnsNoBody()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.DeleteAsync($"{_path}/{ShareLinkApiFactory.LinkId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }
}
