using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auditing;
using CloudFilm.Application.Auth;
using CloudFilm.Application.Grants;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NSubstitute.ExceptionExtensions;

namespace CloudFilm.Api.Tests.Grants;

/// <summary>
/// A host with the grant store, the grant reader, the user directory and the
/// audit log substituted.
/// </summary>
/// <remarks>
/// The real implementations need PostgreSQL, which this suite runs without.
/// Faking them is also what makes the assertions possible: what matters is which
/// callers get through, what the endpoint asks for on their behalf, and whether
/// it recorded the act before answering.
/// </remarks>
public sealed class AccessGrantApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Runs the operation and remembers whether it committed.</summary>
    /// <remarks>
    /// A hand-written fake, not a substitute: what is under test is what the
    /// endpoint puts inside the boundary. It opens no transaction - that is the
    /// real one's job, against PostgreSQL - but it records whether the endpoint
    /// left the boundary by committing or by unwinding, which is what makes
    /// "an audit failure rolls the grant back" an assertion rather than a hope.
    /// </remarks>
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

    /// <summary>Gets the substituted writing side.</summary>
    public IAccessGrantStore Store { get; } = Substitute.For<IAccessGrantStore>();

    /// <summary>Gets the substituted reading side.</summary>
    public IAccessGrantQuery Query { get; } = Substitute.For<IAccessGrantQuery>();

    /// <summary>Gets the substituted account directory.</summary>
    public IUserDirectory Users { get; } = Substitute.For<IUserDirectory>();

    /// <summary>Gets the substituted audit log.</summary>
    public IAuditLogService AuditLog { get; } = Substitute.For<IAuditLogService>();

    /// <summary>Gets the recording unit of work.</summary>
    public RecordingUnitOfWork UnitOfWork { get; } = new();

    /// <summary>Returns every double to its as-created state.</summary>
    public void ResetSubstitutes()
    {
        Store.ClearSubstitute(ClearOptions.All);
        Query.ClearSubstitute(ClearOptions.All);
        Users.ClearSubstitute(ClearOptions.All);
        AuditLog.ClearSubstitute(ClearOptions.All);
        UnitOfWork.Reset();

        Users.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        Store.GrantAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
            .Returns(true);

        Store.RevokeByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        Query.FindActiveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AGrant());

        Query.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(AGrant());

        Query.ListForStudyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<StudyAccessGrant>>([AGrant()]));

        AuditLog.RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    /// <summary>A grant the reader answers with.</summary>
    public static StudyAccessGrant AGrant() => new(
        Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
        "1.2.840.113564.1",
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        GrantPermissions.View,
        DateTimeOffset.UnixEpoch,
        null,
        null);

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

            services.RemoveAll<IAccessGrantStore>();
            services.AddScoped(_ => Store);

            services.RemoveAll<IAccessGrantQuery>();
            services.AddScoped(_ => Query);

            services.RemoveAll<IUserDirectory>();
            services.AddScoped(_ => Users);

            services.RemoveAll<IAuditLogService>();
            services.AddScoped(_ => AuditLog);

            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(_ => UnitOfWork);
        });
    }
}

/// <summary>
/// Who may manage access grants, and what managing one records.
/// </summary>
public sealed class AccessGrantEndpointTests : IClassFixture<AccessGrantApiFactory>
{
    private const string _path = "/api/v1/access-grants";
    private const string _study = "1.2.840.113564.1";

    private static readonly Guid _administrator = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _grantee = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _grantId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private readonly AccessGrantApiFactory _factory;

    public AccessGrantEndpointTests(AccessGrantApiFactory factory)
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

    private static CreateAccessGrantRequest ARequest(
        string? study = null,
        Guid? grantee = null,
        string? permission = null,
        DateTimeOffset? expiresAtUtc = null) =>
        new(study ?? _study, grantee ?? _grantee, permission ?? GrantPermissions.View, expiresAtUtc);

    // -----------------------------------------------------------------------
    // Who may manage grants.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AnAnonymousCallerIsNotAuthenticated()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage created = await client.PostAsJsonAsync(_path, ARequest());
        using HttpResponseMessage listed = await client.GetAsync($"{_path}?studyInstanceUid={_study}");
        using HttpResponseMessage revoked = await client.DeleteAsync($"{_path}/{_grantId}");

        created.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        listed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        revoked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await _factory.Store.DidNotReceiveWithAnyArgs()
            .GrantAsync(default, default!, default!, default, default, default);
        await _factory.Query.DidNotReceiveWithAnyArgs().ListForStudyAsync(default!, default);
    }

    /// <summary>
    /// Deciding who else may see a patient's imaging is an administrative act. A
    /// clinician doing clinical work reads studies; they do not open them up.
    /// </summary>
    [Fact]
    public async Task AClinicianIsRefused()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage created = await client.PostAsJsonAsync(_path, ARequest());
        using HttpResponseMessage listed = await client.GetAsync($"{_path}?studyInstanceUid={_study}");
        using HttpResponseMessage revoked = await client.DeleteAsync($"{_path}/{_grantId}");

        created.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        listed.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        revoked.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await _factory.Store.DidNotReceiveWithAnyArgs()
            .GrantAsync(default, default!, default!, default, default, default);
        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    [Fact]
    public async Task AnAccountWithNoApplicationRoleIsRefused()
    {
        using HttpClient client = CreateClientWithRoles();

        using HttpResponseMessage response = await client.PostAsJsonAsync(_path, ARequest());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnAdministratorMayCreateAGrant()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(_path, ARequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().EndWith(_grantId.ToString());

        StudyAccessGrant? body = await response.Content.ReadFromJsonAsync<StudyAccessGrant>();

        body!.Id.Should().Be(_grantId);
        body.StudyInstanceUid.Should().Be(_study);
        body.GranteeUserId.Should().Be(_grantee);
        body.Permission.Should().Be(GrantPermissions.View);
    }

    // -----------------------------------------------------------------------
    // What a create refuses, and how quietly.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("All")]
    [InlineData("view")]
    [InlineData("Admin")]
    [InlineData("")]
    public async Task RefusesAPermissionItDoesNotDefine(string permission)
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            _path,
            ARequest(permission: permission));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _factory.Store.DidNotReceiveWithAnyArgs()
            .GrantAsync(default, default!, default!, default, default, default);
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
            ARequest(study: new string('1', GrantLimits.MaxStudyInstanceUidLength + 1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefusesAGrantToAnAccountThatDoesNotExist()
    {
        _factory.Users.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(_path, ARequest());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _factory.Store.DidNotReceiveWithAnyArgs()
            .GrantAsync(default, default!, default!, default, default, default);
    }

    /// <summary>
    /// The refusal for an account that does not exist must be indistinguishable
    /// from the refusal for a permission that does not exist. Anything else is
    /// an account enumerator wearing a 400.
    /// </summary>
    [Fact]
    public async Task SaysNothingAboutWhichPartOfTheRequestWasWrong()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage badPermission = await client.PostAsJsonAsync(
            _path,
            ARequest(permission: "Everything"));

        string permissionBody = await badPermission.Content.ReadAsStringAsync();

        _factory.Users.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        using HttpResponseMessage unknownAccount = await client.PostAsJsonAsync(_path, ARequest());

        string accountBody = await unknownAccount.Content.ReadAsStringAsync();

        badPermission.StatusCode.Should().Be(unknownAccount.StatusCode);

        // Title and status, not the whole body: a problem response also carries
        // a per-request trace identifier, which differs by design and says
        // nothing about the request that was refused.
        Title(permissionBody).Should().Be(Title(accountBody));

        accountBody.Should().NotContain(_grantee.ToString());
        accountBody.Should().NotContain("exist");
    }

    private static string Title(string problemBody)
    {
        using JsonDocument document = JsonDocument.Parse(problemBody);

        return document.RootElement.GetProperty("title").GetString() ?? string.Empty;
    }

    /// <summary>
    /// One live grant per account per study. Widening or narrowing is a revoke
    /// and a new grant, so both appear in the record.
    /// </summary>
    [Fact]
    public async Task RefusesASecondLiveGrantForTheSamePair()
    {
        _factory.Store.GrantAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
            .Returns(false);

        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(_path, ARequest());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    // -----------------------------------------------------------------------
    // Who is recorded as granting.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RecordsTheGrantAgainstTheAuthenticatedActor()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            _path,
            ARequest(permission: GrantPermissions.Download));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await _factory.Store.Received(1).GrantAsync(
            _grantee,
            _study,
            GrantPermissions.Download,
            _administrator,
            null,
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The request has nowhere to name a granting account, so a body that tries
    /// to is simply not read. The value comes from the token.
    /// </summary>
    [Fact]
    public async Task IgnoresAGrantingAccountTheClientTriesToName()
    {
        Guid impersonated = Guid.Parse("99999999-9999-9999-9999-999999999999");

        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(_path, new
        {
            studyInstanceUid = _study,
            granteeUserId = _grantee,
            permission = GrantPermissions.View,
            expiresAtUtc = (DateTimeOffset?)null,
            grantedByUserId = impersonated,
            grantedBy = impersonated,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await _factory.Store.Received(1).GrantAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            _administrator,
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>());

        await _factory.Store.DidNotReceive().GrantAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            impersonated,
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Listing.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ListsTheGrantsInForceOnAStudy()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.GetAsync($"{_path}?studyInstanceUid={_study}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.Query.Received(1).ListForStudyAsync(_study, Arg.Any<CancellationToken>());

        string body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("granteeUserId").And.Contain("permission");
        body.Should().NotContain("passwordHash");
        body.Should().NotContain("securityStamp");
        body.Should().NotContain("grantedByUserId");
    }

    [Fact]
    public async Task RefusesAListingWithNoStudy()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.GetAsync(_path);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _factory.Query.DidNotReceiveWithAnyArgs().ListForStudyAsync(default!, default);
    }

    // -----------------------------------------------------------------------
    // Revocation.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RevokesTheGrantThatWasNamed()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.DeleteAsync($"{_path}/{_grantId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.Store.Received(1).RevokeByIdAsync(_grantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnswersNotFoundForAGrantThatNeverExisted()
    {
        _factory.Query.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((StudyAccessGrant?)null);

        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.DeleteAsync($"{_path}/{_grantId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _factory.Store.DidNotReceiveWithAnyArgs().RevokeByIdAsync(default, default);
        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    /// <summary>
    /// Revoking something already revoked changed nothing. The answer is the
    /// same either way, so repeating the request is safe, and nothing is
    /// recorded a second time.
    /// </summary>
    [Fact]
    public async Task RevokingTwiceIsSafeAndRecordedOnce()
    {
        _factory.Store.RevokeByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.DeleteAsync($"{_path}/{_grantId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    // -----------------------------------------------------------------------
    // Audit.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RecordsExactlyOneEventWhenAGrantIsCreated()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(_path, ARequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await _factory.AuditLog.Received(1).RecordAsync(
            Arg.Is<AuditEntry>(entry =>
                entry.Action == AuditAction.GrantCreated
                && entry.ResourceType == AuditResourceType.Study
                && entry.ResourceId == _study
                && entry.ActorUserId == _administrator),
            Arg.Any<CancellationToken>());

        await _factory.AuditLog.Received(1).RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordsExactlyOneEventWhenAGrantIsRevoked()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.DeleteAsync($"{_path}/{_grantId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.AuditLog.Received(1).RecordAsync(
            Arg.Is<AuditEntry>(entry =>
                entry.Action == AuditAction.GrantRevoked
                && entry.ResourceType == AuditResourceType.Study
                && entry.ResourceId == _study
                && entry.ActorUserId == _administrator),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An unrecorded widening of who can see a patient's imaging is worse than a
    /// failed request, so the grant goes back with the audit write.
    /// </summary>
    [Fact]
    public async Task AnAuditFailureUnwindsTheGrant()
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
        body.Should().NotContain(_grantee.ToString());
    }

    [Fact]
    public async Task AnAuditFailureUnwindsARevocation()
    {
        _factory.AuditLog.RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));

        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.DeleteAsync($"{_path}/{_grantId}");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        _factory.UnitOfWork.Commits.Should().Be(0);
        _factory.UnitOfWork.Rollbacks.Should().Be(1);
    }

    [Fact]
    public async Task TheGrantAndItsAuditRecordShareOneBoundary()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.PostAsJsonAsync(_path, ARequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        _factory.UnitOfWork.Commits.Should().Be(1);
        _factory.UnitOfWork.Rollbacks.Should().Be(0);
    }
}
