using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auth;
using CloudFilm.Infrastructure.Configuration;
using CloudFilm.Application.Auditing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using NSubstitute.ExceptionExtensions;




namespace CloudFilm.Api.Tests.ReadState;

/// <summary>
/// The HTTP contract of study read state: who may call it, whose state it
/// touches, what it records, and what it does when part of the work fails.
/// </summary>
public sealed class ReadStateEndpointTests : IClassFixture<ReadStateApiFactory>
{
    private const string _uid = "1.2.840.113564.145320946818674.1292.636566240007310666.706";

    private static readonly Guid _actor = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid _otherActor = Guid.Parse("99999999-8888-7777-6666-555555555555");

    private readonly ReadStateApiFactory _factory;

    public ReadStateEndpointTests(ReadStateApiFactory factory)
    {
        _factory = factory;

        _factory.ResetSubstitutes();
    }

    private static string Path(string studyInstanceUid) =>
        $"/api/v1/imaging/studies/{Uri.EscapeDataString(studyInstanceUid)}/read-state";

    private HttpClient CreateSignedInClient(Guid actorUserId)
    {
        HttpClient client = _factory.CreateClient();

        using IServiceScope scope = _factory.Services.CreateScope();
        IAccessTokenIssuer issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        AccessToken token = issuer.Issue(
            actorUserId,
            "clinician@example.test",
            "Dr. Example",
            [RoleNames.Clinician]
        );

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);

        return client;
    }

    /// <summary>
    /// Builds a validly signed token whose subject is not an account identifier.
    /// </summary>
    /// <remarks>
    /// The issuer cannot produce one: its subject is a <see cref="Guid"/>, which
    /// is the point of that signature. The endpoint still has to defend against
    /// such a token, because a signing key that ever leaks or a future issuer
    /// that ever changes would produce exactly this, so the token is minted here
    /// with the host's own key rather than the test being deleted along with the
    /// way it used to be written.
    /// </remarks>
    private HttpClient CreateClientWithSubject(string subject)
    {
        HttpClient client = _factory.CreateClient();

        using IServiceScope scope = _factory.Services.CreateScope();
        JwtOptions jwt = scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;

        DateTime issuedAt = DateTime.UtcNow;

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [JwtRegisteredClaimNames.Sub] = subject,
                [JwtRegisteredClaimNames.Email] = "clinician@example.test",
                [JwtRegisteredClaimNames.Name] = "Dr. Example",
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                [System.Security.Claims.ClaimTypes.Role] = RoleNames.Clinician,
            },
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JsonWebTokenHandler().CreateToken(descriptor));

        return client;
    }

    private static async Task<bool> ReadFlagAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("read").GetBoolean();
    }

    // -----------------------------------------------------------------------
    // Authentication.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Which studies a clinician has read is a list of studies they opened, so
    /// it is as protected as the studies themselves.
    /// </summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task RejectsAnonymousRequests(string method)
    {
        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage request = new(new HttpMethod(method), Path(_uid));

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await _factory.ReadState.DidNotReceiveWithAnyArgs().IsReadAsync(default, default!, default);
        await _factory.ReadState.DidNotReceiveWithAnyArgs().MarkReadAsync(default, default!, default);
        await _factory.ReadState.DidNotReceiveWithAnyArgs().MarkUnreadAsync(default, default!, default);
        _factory.UnitOfWork.Events.Should().BeEmpty("no transaction should open for a request that is refused");
    }

    /// <summary>
    /// A validated token whose subject is not an account identifier cannot be
    /// scoped to anybody, and a mark that belongs to nobody is worse than a
    /// refusal.
    /// </summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task RejectsATokenWhoseSubjectIsNotAnAccountIdentifier(string method)
    {
        using HttpClient client = CreateClientWithSubject("not-a-guid");
        using HttpRequestMessage request = new(new HttpMethod(method), Path(_uid));

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await _factory.ReadState.DidNotReceiveWithAnyArgs().MarkReadAsync(default, default!, default);
        _factory.UnitOfWork.Events.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // State.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AStudyNobodyHasMarkedIsUnread()
    {
        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.GetAsync(Path(_uid));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadFlagAsync(response)).Should().BeFalse();
    }

    [Fact]
    public async Task GetReportsTheStudyAsReadOnceItHasBeenMarked()
    {
        _factory.ReadState
            .IsReadAsync(_actor, _uid, Arg.Any<CancellationToken>())
            .Returns(true);

        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.GetAsync(Path(_uid));

        (await ReadFlagAsync(response)).Should().BeTrue();
    }

    [Fact]
    public async Task MarkingAStudyReadReportsItAsRead()
    {
        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.PutAsync(Path(_uid), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadFlagAsync(response)).Should().BeTrue();

        await _factory.ReadState.Received(1)
            .MarkReadAsync(_actor, _uid, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkingAStudyUnreadReportsItAsUnread()
    {
        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.DeleteAsync(Path(_uid));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadFlagAsync(response)).Should().BeFalse();

        await _factory.ReadState.Received(1)
            .MarkUnreadAsync(_actor, _uid, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The response says whether the study is read and nothing else - no actor,
    /// no study identifier, no timestamp, and nothing about the patient.
    /// </summary>
    [Fact]
    public async Task TheResponseCarriesTheReadFlagAndNothingElse()
    {
        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.GetAsync(Path(_uid));

        string body = await response.Content.ReadAsStringAsync();

        using JsonDocument document = JsonDocument.Parse(body);

        document.RootElement.EnumerateObject().Select(property => property.Name)
            .Should().Equal("read");

        body.Should().NotContain(_actor.ToString());
        body.Should().NotContain(_uid);
        body.Should().NotContain("markedReadAt");
    }

    // -----------------------------------------------------------------------
    // Actor scoping.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TheActorComesFromTheTokenSubject()
    {
        using (HttpClient first = CreateSignedInClient(_actor))
        {
            using HttpResponseMessage response = await first.PutAsync(Path(_uid), content: null);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using (HttpClient second = CreateSignedInClient(_otherActor))
        {
            using HttpResponseMessage response = await second.PutAsync(Path(_uid), content: null);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await _factory.ReadState.Received(1).MarkReadAsync(_actor, _uid, Arg.Any<CancellationToken>());
        await _factory.ReadState.Received(1).MarkReadAsync(_otherActor, _uid, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// There is no parameter for whose state to touch - but a route that grew one
    /// silently would still answer 200, so absence is asserted rather than
    /// assumed.
    /// </summary>
    [Theory]
    [InlineData("?actorUserId=99999999-8888-7777-6666-555555555555")]
    [InlineData("?userId=99999999-8888-7777-6666-555555555555")]
    [InlineData("?sub=99999999-8888-7777-6666-555555555555")]
    public async Task IgnoresAnyAttemptToNameAnotherUser(string query)
    {
        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage read = await client.GetAsync(Path(_uid) + query);
        using HttpResponseMessage write = await client.PutAsync(Path(_uid) + query, content: null);

        read.StatusCode.Should().Be(HttpStatusCode.OK);
        write.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.ReadState.Received(1).IsReadAsync(_actor, _uid, Arg.Any<CancellationToken>());
        await _factory.ReadState.Received(1).MarkReadAsync(_actor, _uid, Arg.Any<CancellationToken>());

        await _factory.ReadState.DidNotReceive().IsReadAsync(_otherActor, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _factory.ReadState.DidNotReceive().MarkReadAsync(_otherActor, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// One user's mark must not answer another user's question. The store is
    /// asked only about the caller, so B's state is not reachable through A's
    /// token even for the same study.
    /// </summary>
    [Fact]
    public async Task OneUserCannotReachAnothersState()
    {
        _factory.ReadState
            .IsReadAsync(_otherActor, _uid, Arg.Any<CancellationToken>())
            .Returns(true);

        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.GetAsync(Path(_uid));

        (await ReadFlagAsync(response)).Should().BeFalse("the caller has not marked this study, whatever anyone else did");

        await _factory.ReadState.DidNotReceive()
            .IsReadAsync(_otherActor, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Identifier handling.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The archive trims before it looks a study up, so a padded identifier must
    /// not key a second row for the same study.
    /// </summary>
    [Fact]
    public async Task TrimsTheStudyInstanceUid()
    {
        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.PutAsync(Path($"  {_uid}  "), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.ReadState.Received(1)
            .MarkReadAsync(_actor, _uid, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task RejectsAnIdentifierThatIsOnlyWhitespace(string method)
    {
        using HttpClient client = CreateSignedInClient(_actor);
        using HttpRequestMessage request = new(new HttpMethod(method), Path("   "));

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _factory.ReadState.DidNotReceiveWithAnyArgs().IsReadAsync(default, default!, default);
        await _factory.ReadState.DidNotReceiveWithAnyArgs().MarkReadAsync(default, default!, default);
        _factory.UnitOfWork.Events.Should().BeEmpty();
    }

    /// <summary>
    /// Refused rather than truncated: a shortened UID identifies a different
    /// study, and marking the wrong one read is worse than refusing.
    /// </summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task RejectsAnIdentifierLongerThanTheColumn(string method)
    {
        string tooLong = new('1', 129);

        using HttpClient client = CreateSignedInClient(_actor);
        using HttpRequestMessage request = new(new HttpMethod(method), Path(tooLong));

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _factory.ReadState.DidNotReceiveWithAnyArgs().MarkReadAsync(default, default!, default);
        _factory.UnitOfWork.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task AcceptsAnIdentifierOfExactlyTheMaximumLength()
    {
        string atLimit = new('1', 128);

        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.PutAsync(Path(atLimit), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.ReadState.Received(1)
            .MarkReadAsync(_actor, atLimit, Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Idempotency and audit semantics.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MarkingAnAlreadyReadStudyReadChangesNothingAndRecordsNothing()
    {
        _factory.ReadState
            .MarkReadAsync(_actor, _uid, Arg.Any<CancellationToken>())
            .Returns(false);

        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.PutAsync(Path(_uid), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadFlagAsync(response)).Should().BeTrue("the study is read, which is what the caller asked for");

        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
        _factory.UnitOfWork.Commits.Should().Be(1, "a no-op still completes successfully");
    }

    [Fact]
    public async Task MarkingAnAlreadyUnreadStudyUnreadChangesNothingAndRecordsNothing()
    {
        _factory.ReadState
            .MarkUnreadAsync(_actor, _uid, Arg.Any<CancellationToken>())
            .Returns(false);

        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.DeleteAsync(Path(_uid));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadFlagAsync(response)).Should().BeFalse();

        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
        _factory.UnitOfWork.Commits.Should().Be(1);
    }

    [Fact]
    public async Task AnUnreadToReadTransitionIsRecorded()
    {
        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.PutAsync(Path(_uid), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AuditLog.Received(1).RecordAsync(
            Arg.Is<AuditEntry>(entry =>
                entry.Action == AuditAction.MarkStudyRead
                && entry.ResourceType == AuditResourceType.Study
                && entry.ResourceId == _uid
                && entry.ActorUserId == _actor),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AReadToUnreadTransitionIsRecorded()
    {
        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.DeleteAsync(Path(_uid));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AuditLog.Received(1).RecordAsync(
            Arg.Is<AuditEntry>(entry =>
                entry.Action == AuditAction.MarkStudyUnread
                && entry.ResourceType == AuditResourceType.Study
                && entry.ResourceId == _uid
                && entry.ActorUserId == _actor),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Reading the state is not an access to patient data and must not fill the
    /// trail with entries for a panel that renders on every page load.
    /// </summary>
    [Fact]
    public async Task ReadingTheStateRecordsNothingAndOpensNoTransaction()
    {
        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.GetAsync(Path(_uid));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
        _factory.UnitOfWork.Events.Should().BeEmpty("a read needs no transaction");
    }

    /// <summary>
    /// The sign-in actions belong to authentication and must never be written by
    /// this endpoint, whatever it is asked to do.
    /// </summary>
    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task NeverRecordsAnAuthenticationAction(string method)
    {
        using HttpClient client = CreateSignedInClient(_actor);
        using HttpRequestMessage request = new(new HttpMethod(method), Path(_uid));

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AuditLog.DidNotReceive().RecordAsync(
            Arg.Is<AuditEntry>(entry =>
                entry.Action == AuditAction.LoginSucceeded
                || entry.Action == AuditAction.LoginFailed
                || entry.ResourceType == AuditResourceType.Authentication),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Transaction composition.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The mutation and its audit record must be one operation, in that order,
    /// inside one boundary.
    /// </summary>
    [Fact]
    public async Task TheMutationAndItsAuditRecordShareOneBoundary()
    {
        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.PutAsync(Path(_uid), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.UnitOfWork.Events.Should().Equal("begin", "mark-read", "audit", "commit");
        _factory.UnitOfWork.Rollbacks.Should().Be(0);
    }

    /// <summary>
    /// An audit failure must take the state change with it.
    /// </summary>
    /// <remarks>
    /// The endpoint's job is to leave the boundary by unwinding rather than by
    /// committing, and to report failure rather than success. Whether the
    /// database then actually rolls the row back is PostgreSQL's job, exercised
    /// against a live stack - see the class remarks on
    /// <see cref="RecordingUnitOfWork"/>.
    /// </remarks>
    [Fact]
    public async Task AnAuditFailureUnwindsTheOperationAndFailsTheRequest()
    {
        _factory.AuditLog
            .RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));

        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.PutAsync(Path(_uid), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        _factory.UnitOfWork.Commits.Should().Be(0);
        _factory.UnitOfWork.Rollbacks.Should().Be(1);
        _factory.UnitOfWork.Events.Should().Equal("begin", "mark-read", "rollback");
    }

    /// <summary>
    /// A failed state change must not leave an audit record claiming it happened.
    /// </summary>
    [Fact]
    public async Task AFailedMutationRecordsNothing()
    {
        _factory.ReadState
            .MarkReadAsync(_actor, _uid, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.PutAsync(Path(_uid), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
        _factory.UnitOfWork.Commits.Should().Be(0);
        _factory.UnitOfWork.Rollbacks.Should().Be(1);
    }

    /// <summary>
    /// A failure must say nothing about the study, the patient or the account.
    /// </summary>
    [Fact]
    public async Task AFailureDisclosesNothingAboutTheRequest()
    {
        _factory.ReadState
            .MarkReadAsync(_actor, _uid, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Npgsql: connection to 10.0.0.4 refused"));

        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.PutAsync(Path(_uid), content: null);

        string body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("Npgsql");
        body.Should().NotContain("10.0.0.4");
        body.Should().NotContain(_actor.ToString());
    }
}
