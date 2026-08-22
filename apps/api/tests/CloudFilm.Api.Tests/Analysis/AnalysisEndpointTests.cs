using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Analysis;
using CloudFilm.Application.Auditing;
using CloudFilm.Application.Auth;
using CloudFilm.Application.Grants;
using CloudFilm.Application.Imaging;
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

namespace CloudFilm.Api.Tests.Analysis;

/// <summary>
/// A host with the access policy, the download source, the analyser and the
/// audit log substituted.
/// </summary>
/// <remarks>
/// No analysis service runs here and no DICOM is parsed. What these tests are
/// about is the order the endpoint does things in and what it refuses - which is
/// where this route's security lives, because everything downstream of it has
/// already been proved by its own suite.
/// </remarks>
public sealed class AnalysisApiFactory : WebApplicationFactory<Program>
{
    /// <summary>A stream that reports how many times it was closed.</summary>
    /// <remarks>
    /// Disposal is a behaviour worth asserting rather than assuming: the study is
    /// a live connection to the archive, and one left open per request is a
    /// connection pool exhausted by lunchtime.
    /// </remarks>
    public sealed class TrackedStream(byte[] payload) : MemoryStream(payload)
    {
        public int Disposals { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposals++;

            base.Dispose(disposing);
        }
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

    /// <summary>The study every test asks about.</summary>
    public const string Study = "1.2.840.113564.1";

    /// <summary>The account the bearer token names.</summary>
    public static Guid Actor { get; } = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public IStudyAccessPolicy AccessPolicy { get; } = Substitute.For<IStudyAccessPolicy>();

    public IStudyDownloadSource Downloads { get; } = Substitute.For<IStudyDownloadSource>();

    public IStudyAnalyser Analyser { get; } = Substitute.For<IStudyAnalyser>();

    public IAuditLogService AuditLog { get; } = Substitute.For<IAuditLogService>();

    /// <summary>Where an analysis is kept once it has been produced.</summary>
    /// <remarks>
    /// Substituted rather than backed by a database: what these tests are about
    /// is whether the endpoint stores what it returns, reads back without
    /// re-running anything, and refuses before it does either. Whether the SQL
    /// behind it upserts correctly is pinned by the mapping tests and by the
    /// store's own single statement.
    /// </remarks>
    public IStudyAnalysisResultStore AnalysisResults { get; } =
        Substitute.For<IStudyAnalysisResultStore>();

    public RecordingUnitOfWork UnitOfWork { get; } = new();

    /// <summary>The stream handed to the last download.</summary>
    public TrackedStream? LastStream { get; private set; }

    /// <summary>
    /// Whether the analyser is registered at all.
    /// </summary>
    /// <remarks>
    /// Stands in for a deployment with no <c>Downstream:AnalysisServiceUrl</c>:
    /// the port is registered only when one is configured, so "not configured"
    /// and "absent from the container" are the same state.
    /// </remarks>
    public bool AnalyserIsConfigured { get; set; } = true;

    /// <summary>What the analyser answers by default.</summary>
    public static StudyAnalysis AnAnalysis(string studyInstanceUid = Study) =>
        new(
            studyInstanceUid,
            "dicom-characterization",
            "0.1.0",
            new DateTimeOffset(2026, 8, 16, 9, 0, 0, TimeSpan.Zero),
            [new AnalysisFinding("instances=2", 1.0d), new AnalysisFinding("modality=CT", 1.0d)]);

    /// <summary>Returns every double to its as-created state.</summary>
    public void ResetSubstitutes()
    {
        AccessPolicy.ClearSubstitute(ClearOptions.All);
        Downloads.ClearSubstitute(ClearOptions.All);
        Analyser.ClearSubstitute(ClearOptions.All);
        AuditLog.ClearSubstitute(ClearOptions.All);
        AnalysisResults.ClearSubstitute(ClearOptions.All);
        UnitOfWork.Reset();
        LastStream = null;

        AccessPolicy
            .AuthoriseAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(StudyAccessScope.FullCatalog);

        Downloads.OpenStudyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                LastStream = new TrackedStream(Encoding.ASCII.GetBytes("DICM-not-a-real-study"));

                return new StudyDownload(
                    LastStream,
                    "multipart/related; type=\"application/dicom\"; boundary=abc123");
            });

        Analyser
            .AnalyseAsync(Arg.Any<string>(), Arg.Any<StudyDownload>(), Arg.Any<CancellationToken>())
            .Returns(AnAnalysis());

        AuditLog.RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Nothing stored, which is the state a study starts in. Tests that want
        // a previously analysed study say so themselves.
        AnalysisResults.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((StudyAnalysis?)null);

        AnalysisResults
            .SaveAsync(Arg.Any<StudyAnalysis>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
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

            services.RemoveAll<IStudyAccessPolicy>();
            services.AddScoped(_ => AccessPolicy);

            services.RemoveAll<IStudyDownloadSource>();
            services.AddScoped(_ => Downloads);

            services.RemoveAll<IAuditLogService>();
            services.AddScoped(_ => AuditLog);

            services.RemoveAll<IStudyAnalysisResultStore>();
            services.AddScoped(_ => AnalysisResults);

            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(_ => UnitOfWork);

            // Removed unconditionally first: the real registration exists only
            // when an address is configured, and a test that wants the
            // unconfigured deployment must be able to reproduce its absence.
            services.RemoveAll<IStudyAnalyser>();

            if (AnalyserIsConfigured)
            {
                services.AddScoped(_ => Analyser);
            }
        });
    }
}

/// <summary>
/// What the analysis endpoint does, in what order, and what it refuses.
/// </summary>
public sealed class AnalysisEndpointTests : IClassFixture<AnalysisApiFactory>
{
    private static string Path(string studyInstanceUid) =>
        $"/api/v1/imaging/studies/{Uri.EscapeDataString(studyInstanceUid)}/analysis";

    private readonly AnalysisApiFactory _factory;

    public AnalysisEndpointTests(AnalysisApiFactory factory)
    {
        _factory = factory;

        _factory.ResetSubstitutes();
    }

    private HttpClient CreateClientWithRoles(params string[] roles)
    {
        HttpClient client = _factory.CreateClient();

        using IServiceScope scope = _factory.Services.CreateScope();
        IAccessTokenIssuer issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        AccessToken token = issuer.Issue(
            AnalysisApiFactory.Actor,
            "clinician@example.test",
            "Dr. Example",
            roles);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        return client;
    }

    // -----------------------------------------------------------------------
    // Who may ask.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AnAnonymousCallerIsNotAuthenticated()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await _factory.Downloads.DidNotReceiveWithAnyArgs().OpenStudyAsync(default!, default);
    }

    [Fact]
    public async Task AnAccountWithNoApplicationRoleIsForbidden()
    {
        using HttpClient client = CreateClientWithRoles();

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await _factory.Downloads.DidNotReceiveWithAnyArgs().OpenStudyAsync(default!, default);
        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    [Theory]
    [InlineData(RoleNames.Clinician)]
    [InlineData(RoleNames.Administrator)]
    public async Task AnAuthorisedCallerReceivesAnAnalysis(string role)
    {
        using HttpClient client = CreateClientWithRoles(role);

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Sending a study to a processor is not reading a record. The wider
    /// permission is what this route asks for, and asking for the narrower one
    /// would let a view-only entitlement move a study out of CloudFilm.
    /// </summary>
    [Fact]
    public async Task AuthorisationIsAskedForTheDownloadPermission()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AccessPolicy
            .Received(1)
            .AuthoriseAsync(
                Arg.Any<ClaimsPrincipal>(),
                AnalysisApiFactory.Study,
                GrantPermissions.Download,
                Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // What is refused, and how early.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The same answer a study that does not exist gets. A distinct status would
    /// say "this study exists, but not for you" - and a Study Instance UID is all
    /// it takes to open a study in the viewer.
    /// </summary>
    [Fact]
    public async Task ADeniedStudyIsRefusedLikeAMissingOne()
    {
        _factory.AccessPolicy
            .AuthoriseAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(StudyAccessScope.Denied);

        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The order is the security property. A caller who may not see a study must
    /// not be able to make the archive produce it, however briefly.
    /// </summary>
    [Fact]
    public async Task ADeniedStudyIsNeverOpened()
    {
        _factory.AccessPolicy
            .AuthoriseAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(StudyAccessScope.Denied);

        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _factory.Downloads.DidNotReceiveWithAnyArgs().OpenStudyAsync(default!, default);
        await _factory.Analyser.DidNotReceiveWithAnyArgs().AnalyseAsync(default!, default!, default);
        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    [Fact]
    public async Task AStudyTheArchiveDoesNotHoldIsRefused()
    {
        _factory.Downloads.OpenStudyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((StudyDownload?)null);

        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _factory.Analyser.DidNotReceiveWithAnyArgs().AnalyseAsync(default!, default!, default);
        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    /// <summary>
    /// The archive produced the study and it held no instances. Nothing was
    /// analysed because there was nothing to analyse.
    /// </summary>
    [Fact]
    public async Task AStudyWithNothingToAnalyseIsRefused()
    {
        _factory.Analyser
            .AnalyseAsync(Arg.Any<string>(), Arg.Any<StudyDownload>(), Arg.Any<CancellationToken>())
            .Returns((StudyAnalysis?)null);

        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
        _factory.UnitOfWork.Commits.Should().Be(0);
    }

    [Theory]
    [InlineData("   ")]
    public async Task AStudyIdentifierThatNamesNothingIsRefused(string studyInstanceUid)
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.PostAsync(Path(studyInstanceUid), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _factory.Downloads.DidNotReceiveWithAnyArgs().OpenStudyAsync(default!, default);
    }

    /// <summary>
    /// Refused rather than truncated: a truncated UID identifies a different
    /// study, and analysing the wrong one is worse than refusing.
    /// </summary>
    [Fact]
    public async Task AStudyIdentifierTooLongToBeOneIsRefused()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        string tooLong = new('1', AnalysisLimits.MaxStudyInstanceUidLength + 1);

        using HttpResponseMessage response = await client.PostAsync(Path(tooLong), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _factory.Downloads.DidNotReceiveWithAnyArgs().OpenStudyAsync(default!, default);
    }

    // -----------------------------------------------------------------------
    // A deployment with no analysis service.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ADeploymentWithNoAnalysisServiceSaysSo()
    {
        // Its own host, because the shared fixture builds one and keeps it: a
        // deployment without an analysis service is a different container, not a
        // different substitute, and flipping the flag on the shared factory would
        // change nothing or change everything depending on test order.
        using AnalysisApiFactory unconfigured = new();
        unconfigured.AnalyserIsConfigured = false;
        unconfigured.ResetSubstitutes();

        using HttpClient client = unconfigured.CreateClient();

        using IServiceScope scope = unconfigured.Services.CreateScope();
        IAccessTokenIssuer issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        AccessToken token = issuer.Issue(
            AnalysisApiFactory.Actor,
            "clinician@example.test",
            "Dr. Example",
            [RoleNames.Clinician]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);

        // The study is never fetched for a capability that does not exist, and
        // nothing is recorded as analysed.
        await unconfigured.Downloads.DidNotReceiveWithAnyArgs().OpenStudyAsync(default!, default);
        await unconfigured.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    // -----------------------------------------------------------------------
    // The answer.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TheAnalysisIsReturnedAsTheApplicationContract()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        StudyAnalysis? analysis = await response.Content.ReadFromJsonAsync<StudyAnalysis>();

        analysis.Should().NotBeNull();
        analysis!.StudyInstanceUid.Should().Be(AnalysisApiFactory.Study);
        analysis.ModelName.Should().Be("dicom-characterization");
        analysis.ModelVersion.Should().Be("0.1.0");
        analysis.CompletedAtUtc.Should().Be(new DateTimeOffset(2026, 8, 16, 9, 0, 0, TimeSpan.Zero));
        analysis.Findings.Should().HaveCount(2);
        analysis.Findings[0].Label.Should().Be("instances=2");
        analysis.Findings[0].Score.Should().Be(1.0d);
    }

    /// <summary>
    /// The identifier the caller was authorised for is the one that reaches the
    /// analyser and the one that comes back.
    /// </summary>
    [Fact]
    public async Task TheStudyAnalysedIsTheOneInTheRoute()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.Downloads
            .Received(1)
            .OpenStudyAsync(AnalysisApiFactory.Study, Arg.Any<CancellationToken>());

        await _factory.Analyser
            .Received(1)
            .AnalyseAsync(AnalysisApiFactory.Study, Arg.Any<StudyDownload>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// There is no request body, so there is no field a caller could use to name
    /// a different study. One sent anyway changes nothing.
    /// </summary>
    [Fact]
    public async Task ABodyCannotNameADifferentStudy()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using StringContent body = new(
            """{"studyInstanceUid":"1.2.840.113564.999","url":"http://orthanc.internal/studies"}""",
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.Downloads
            .Received(1)
            .OpenStudyAsync(AnalysisApiFactory.Study, Arg.Any<CancellationToken>());

        await _factory.Downloads
            .DidNotReceive()
            .OpenStudyAsync("1.2.840.113564.999", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The study is a live connection to the archive. One left open per request
    /// is a connection pool exhausted by lunchtime.
    /// </summary>
    [Fact]
    public async Task TheStudyIsReleasedExactlyOnce()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.LastStream.Should().NotBeNull();
        _factory.LastStream!.Disposals.Should().Be(1);
    }

    // -----------------------------------------------------------------------
    // The trail.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AnAnalysisIsRecordedOnceAgainstTheCallerAndTheStudy()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AuditLog
            .Received(1)
            .RecordAsync(
                Arg.Is<AuditEntry>(entry =>
                    entry.ActorUserId == AnalysisApiFactory.Actor
                    && entry.Action == AuditAction.AnalyseStudy
                    && entry.ResourceType == AuditResourceType.Study
                    && entry.ResourceId == AnalysisApiFactory.Study),
                Arg.Any<CancellationToken>());

        _factory.UnitOfWork.Commits.Should().Be(1);
        _factory.UnitOfWork.Rollbacks.Should().Be(0);
    }

    /// <summary>
    /// A study that reached a processor and was not recorded as having done so is
    /// exactly the access the trail exists to hold. If it cannot be written, the
    /// caller gets the API's usual error and no result.
    /// </summary>
    [Fact]
    public async Task AnUnrecordableAnalysisIsNotReturned()
    {
        _factory.AuditLog
            .RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("the trail is unavailable"));

        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        string body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("dicom-characterization", "no result may reach a caller it was not recorded for");
        body.Should().NotContain("the trail is unavailable", "the API never discloses an internal message");

        _factory.UnitOfWork.Rollbacks.Should().Be(1);
    }

    /// <summary>
    /// A failing analyser follows the API's existing error behaviour rather than
    /// a status of its own: this sprint deliberately introduced no gateway codes.
    /// </summary>
    [Fact]
    public async Task AFailingAnalyserFollowsTheApisExistingErrorBehaviour()
    {
        _factory.Analyser
            .AnalyseAsync(Arg.Any<string>(), Arg.Any<StudyDownload>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("the analysis service answered 500"));

        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        string body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("the analysis service answered 500");

        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    // -----------------------------------------------------------------------
    // What is kept, and what is read back.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The point of the whole feature: a result that is returned is a result that
    /// was stored, under the identifier that was authorised.
    /// </summary>
    [Fact]
    public async Task AnAnalysisIsStoredForTheStudyItWasAuthorisedFor()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AnalysisResults
            .Received(1)
            .SaveAsync(
                Arg.Is<StudyAnalysis>(analysis =>
                    analysis.StudyInstanceUid == AnalysisApiFactory.Study),
                AnalysisApiFactory.Actor,
                Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Stored inside the transaction that records the access, so a result cannot
    /// outlive the audit entry that says who produced it.
    /// </summary>
    [Fact]
    public async Task AnUnrecordableAnalysisIsNotStored()
    {
        _factory.AuditLog
            .RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("the trail is unavailable"));

        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        await _factory.AnalysisResults.DidNotReceiveWithAnyArgs().SaveAsync(default!, default, default);
        _factory.UnitOfWork.Rollbacks.Should().Be(1);
    }

    /// <summary>
    /// The refresh scenario. A second request for a study that has been analysed
    /// answers from the store, and nothing is sent anywhere to do it.
    /// </summary>
    [Fact]
    public async Task AStoredAnalysisIsReadBackWithoutRunningAnotherOne()
    {
        _factory.AnalysisResults
            .FindAsync(AnalysisApiFactory.Study, Arg.Any<CancellationToken>())
            .Returns(AnalysisApiFactory.AnAnalysis());

        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.GetAsync(Path(AnalysisApiFactory.Study));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        StudyAnalysis? analysis = await response.Content.ReadFromJsonAsync<StudyAnalysis>();

        analysis.Should().NotBeNull();
        analysis!.StudyInstanceUid.Should().Be(AnalysisApiFactory.Study);
        analysis.ModelName.Should().Be("dicom-characterization");

        await _factory.Analyser.DidNotReceiveWithAnyArgs().AnalyseAsync(default!, default!, default);
        await _factory.Downloads.DidNotReceiveWithAnyArgs().OpenStudyAsync(default!, default);
    }

    /// <summary>
    /// One study's findings are never another's. The store is asked for the
    /// study in the route and no other.
    /// </summary>
    [Fact]
    public async Task OneStudysStoredAnalysisIsNotReturnedForAnother()
    {
        const string other = "1.2.840.113564.9999";

        _factory.AnalysisResults
            .FindAsync(AnalysisApiFactory.Study, Arg.Any<CancellationToken>())
            .Returns(AnalysisApiFactory.AnAnalysis());

        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.GetAsync(Path(other));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _factory.AnalysisResults
            .Received(1)
            .FindAsync(other, Arg.Any<CancellationToken>());

        await _factory.AnalysisResults
            .DidNotReceive()
            .FindAsync(AnalysisApiFactory.Study, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A study nobody has analysed answers exactly as a study the caller may not
    /// see does. The portal has already loaded the study before it asks, so the
    /// two are never ambiguous to a client entitled to be asking.
    /// </summary>
    [Fact]
    public async Task AStudyThatHasNeverBeenAnalysedIsNotFound()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.GetAsync(Path(AnalysisApiFactory.Study));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Reading a stored result is not sending a study anywhere, so it asks for
    /// the permission the workspace already requires - and it asks before the
    /// store is touched.
    /// </summary>
    [Fact]
    public async Task ReadingAStoredAnalysisAsksForTheViewPermission()
    {
        _factory.AnalysisResults
            .FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AnalysisApiFactory.AnAnalysis());

        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.GetAsync(Path(AnalysisApiFactory.Study));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AccessPolicy
            .Received(1)
            .AuthoriseAsync(
                Arg.Any<ClaimsPrincipal>(),
                AnalysisApiFactory.Study,
                GrantPermissions.View,
                Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A caller the policy refuses learns nothing, including whether a result
    /// exists - and the store is never asked.
    /// </summary>
    [Fact]
    public async Task ADeniedCallerCannotReadAStoredAnalysis()
    {
        _factory.AccessPolicy
            .AuthoriseAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(StudyAccessScope.Denied);

        _factory.AnalysisResults
            .FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AnalysisApiFactory.AnAnalysis());

        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.GetAsync(Path(AnalysisApiFactory.Study));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        await _factory.AnalysisResults.DidNotReceiveWithAnyArgs().FindAsync(default!, default);
    }

    [Fact]
    public async Task AnAnonymousCallerCannotReadAStoredAnalysis()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Path(AnalysisApiFactory.Study));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await _factory.AnalysisResults.DidNotReceiveWithAnyArgs().FindAsync(default!, default);
    }

    /// <summary>
    /// Analysing the same study twice replaces one row rather than accumulating
    /// two.
    /// </summary>
    /// <remarks>
    /// Two halves prove that, in the two places they can be proved. Here: the
    /// endpoint files both runs under the one identifier it authorised, so it
    /// never asks for a second row. In
    /// <c>StudyAnalysisResultMappingTests.HoldsOneCurrentResultPerStudy</c>: the
    /// unique index on that column, which is what makes a second row impossible
    /// and what the store's <c>ON CONFLICT … DO UPDATE</c> conflicts on.
    ///
    /// The upsert statement itself is not exercised - this suite has no database,
    /// by an existing decision, and adding one would be a larger change than the
    /// behaviour warrants.
    /// </remarks>
    [Fact]
    public async Task AnalysingTheSameStudyTwiceStoresItUnderTheSameIdentifier()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage first = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);
        using HttpResponseMessage second = await client.PostAsync(Path(AnalysisApiFactory.Study), content: null);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AnalysisResults
            .Received(2)
            .SaveAsync(
                Arg.Is<StudyAnalysis>(analysis =>
                    analysis.StudyInstanceUid == AnalysisApiFactory.Study),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>());

        // Nothing else was written under any other identifier, so the second run
        // can only have landed on the first run's row.
        await _factory.AnalysisResults
            .DidNotReceive()
            .SaveAsync(
                Arg.Is<StudyAnalysis>(analysis =>
                    analysis.StudyInstanceUid != AnalysisApiFactory.Study),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>());
    }
}
