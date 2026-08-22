using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auditing;
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

namespace CloudFilm.Api.Tests.Sharing;

/// <summary>
/// A host with the share link store, the viewer session store, the link status
/// query and the audit log substituted.
/// </summary>
/// <remarks>
/// Every request in this suite is anonymous. Both routes under test must work
/// with no Authorization header - one is redeemed by a stranger holding a URL,
/// the other is called by nginx, which has no account either.
/// </remarks>
public sealed class ViewerSessionFactory : WebApplicationFactory<Program>
{
    /// <summary>A cache that remembers what it was asked for.</summary>
    /// <remarks>
    /// Hand written rather than substituted, like the download suite's: the
    /// assertions are about the key - that it carries a digest and never the
    /// token - and about counting across calls, both of which read more clearly
    /// here than as a stack of argument matchers.
    /// </remarks>
    public sealed class RecordingCache : ICacheStore
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

        public List<string> Keys { get; } = [];

        public void Reset()
        {
            _entries.Clear();
            Keys.Clear();
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            Keys.Add(key);

            return Task.FromResult(_entries.TryGetValue(key, out object? value) ? (T?)value : null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken = default)
            where T : class
        {
            Keys.Add(key);
            _entries[key] = value;

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);

            return Task.CompletedTask;
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

    /// <summary>The study the link and the session both name.</summary>
    public const string SharedStudy = "1.2.840.113564.1";

    /// <summary>A study the holder must never be able to reach.</summary>
    public const string OtherStudy = "1.2.840.113564.999";

    /// <summary>The identifier a valid cookie carries in these tests.</summary>
    public const string SessionId = "hp7Qk1u1RzB4bJmZs3wV9c0LtT2nGxYqQeR8sD1aFbI";

    /// <summary>The link a redeemed session came from.</summary>
    public static Guid ShareLinkId { get; } = Guid.Parse("2f14b0a1-4c8e-4a2f-9c1d-0b6f2a7e5c33");

    public IShareLinkStore ShareLinks { get; } = Substitute.For<IShareLinkStore>();

    public IViewerSessionStore ViewerSessions { get; } = Substitute.For<IViewerSessionStore>();

    public IShareLinkStatus ShareLinkStatus { get; } = Substitute.For<IShareLinkStatus>();

    public IAuditLogService AuditLog { get; } = Substitute.For<IAuditLogService>();

    /// <summary>The imaging catalogue, which neither route may touch.</summary>
    public IImagingCatalog Catalog { get; } = Substitute.For<IImagingCatalog>();

    public RecordingUnitOfWork UnitOfWork { get; } = new();

    /// <summary>The cache the redemption rate limiter counts in.</summary>
    public RecordingCache Cache { get; } = new();

    /// <summary>Returns every double to its as-created state.</summary>
    public void ResetSubstitutes()
    {
        ShareLinks.ClearSubstitute(ClearOptions.All);
        ViewerSessions.ClearSubstitute(ClearOptions.All);
        ShareLinkStatus.ClearSubstitute(ClearOptions.All);
        AuditLog.ClearSubstitute(ClearOptions.All);
        Catalog.ClearSubstitute(ClearOptions.All);
        UnitOfWork.Reset();
        Cache.Reset();

        ShareLinks.FindActiveAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new ShareLinkTarget(ShareLinkId, SharedStudy, GrantPermissions.View));

        ViewerSessions
            .CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(call => new ViewerSession(
                SessionId,
                (string)call[0],
                (string)call[1],
                DateTimeOffset.UtcNow + SessionLifetime));

        // The default session: issued from a link, for the shared study.
        ViewerSessions.FindAsync(SessionId, Arg.Any<CancellationToken>())
            .Returns(new ViewerSessionState(SharedStudy, GrantPermissions.View, ShareLinkId));

        ShareLinkStatus.IsActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);

        AuditLog.RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// Fully qualified: this class has a property of the same name holding the
    /// substituted store, and it would otherwise win the lookup.
    /// </summary>
    private static TimeSpan SessionLifetime => CloudFilm.Application.Sharing.ViewerSessions.Lifetime;

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
            services.AddScoped(_ => ShareLinks);

            services.RemoveAll<IViewerSessionStore>();
            services.AddScoped(_ => ViewerSessions);

            services.RemoveAll<IShareLinkStatus>();
            services.AddScoped(_ => ShareLinkStatus);

            services.RemoveAll<IAuditLogService>();
            services.AddScoped(_ => AuditLog);

            services.RemoveAll<IImagingCatalog>();
            services.AddScoped(_ => Catalog);

            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(_ => UnitOfWork);

            // Redemption counts attempts in the cache. Without this the suite
            // would reach for a Redis that is not there.
            services.RemoveAll<ICacheStore>();
            services.AddSingleton<ICacheStore>(Cache);
        });
    }
}

/// <summary>
/// Exchanging a share token for a viewer session.
/// </summary>
public sealed class ShareLinkRedemptionTests : IClassFixture<ViewerSessionFactory>
{
    private readonly ViewerSessionFactory _factory;

    public ShareLinkRedemptionTests(ViewerSessionFactory factory)
    {
        _factory = factory;

        _factory.ResetSubstitutes();
    }

    private static string AToken(byte fill = 0x2A) =>
        WebEncoders.Base64UrlEncode(Enumerable.Repeat(fill, 32).ToArray());

    private static string Path(string token) => $"/api/v1/share/{token}/viewer-session";

    /// <summary>
    /// No account, no bearer token, no role - and a session comes back. This is
    /// the whole point of the route.
    /// </summary>
    [Fact]
    public async Task AHolderOfTheLinkNeedsNoAccount()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(Path(AToken()), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Authorization.Should().BeNull();
    }

    /// <summary>
    /// The study is taken from the link, never from the request: there is no
    /// parameter for it, and the session is bound to what the link named.
    /// </summary>
    [Fact]
    public async Task TheSessionIsBoundToTheStudyTheLinkNames()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(Path(AToken()), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.ViewerSessions
            .Received(1)
            .CreateAsync(
                ViewerSessionFactory.SharedStudy,
                GrantPermissions.View,
                ViewerSessionFactory.ShareLinkId,
                Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// One entry, at the one moment a link becomes viewing. The requests that
    /// follow are checked against the session and are not audited.
    /// </summary>
    [Fact]
    public async Task RedeemingALinkIsRecordedExactlyOnceAndWithoutAnActor()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(Path(AToken()), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AuditLog
            .Received(1)
            .RecordAsync(
                Arg.Is<AuditEntry>(entry =>
                    entry.ActorUserId == null
                    && entry.Action == AuditAction.ShareLinkViewed
                    && entry.ResourceType == AuditResourceType.Study
                    && entry.ResourceId == ViewerSessionFactory.SharedStudy),
                Arg.Any<CancellationToken>());

        _factory.UnitOfWork.Commits.Should().Be(1);
        _factory.UnitOfWork.Rollbacks.Should().Be(0);
    }

    [Fact]
    public async Task AMalformedTokenIsRefusedWithoutAskingTheStore()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response =
            await client.PostAsync(Path("not-a-token"), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _factory.ShareLinks
            .DidNotReceive()
            .FindActiveAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Unknown, expired and withdrawn arrive as one answer from the store, and
    /// leave as one answer to the caller.
    /// </summary>
    [Fact]
    public async Task ALinkTheStoreDoesNotResolveYieldsNoSessionAndNoAudit()
    {
        _factory.ShareLinks.FindActiveAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns((ShareLinkTarget?)null);

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(Path(AToken()), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        await _factory.ViewerSessions
            .DidNotReceive()
            .CreateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>());

        await _factory.AuditLog
            .DidNotReceive()
            .RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Permission is read from the link rather than assumed. A link that permits
    /// nothing this route recognises is refused exactly like an unknown one.
    /// </summary>
    [Fact]
    public async Task ALinkThatDoesNotPermitViewingIsRefusedLikeAnUnknownOne()
    {
        _factory.ShareLinks.FindActiveAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new ShareLinkTarget(
                ViewerSessionFactory.ShareLinkId,
                ViewerSessionFactory.SharedStudy,
                "Nonsense"));

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(Path(AToken()), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The link an administrator issues for viewing and nothing else. It mints a
    /// session exactly like a download link does - the difference between the two
    /// is enforced by the download route, not here.
    /// </summary>
    [Fact]
    public async Task AViewOnlyLinkMintsAViewerSession()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(Path(AToken()), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.ViewerSessions
            .Received(1)
            .CreateAsync(
                ViewerSessionFactory.SharedStudy,
                GrantPermissions.View,
                ViewerSessionFactory.ShareLinkId,
                Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The session carries the link's own permission rather than a widened one.
    /// Minting a Download session from a View link would hand the recipient the
    /// study file through the gate's own credential.
    /// </summary>
    [Fact]
    public async Task TheSessionCarriesTheLinksPermissionUnchanged()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(Path(AToken()), content: null);

        ViewerSession? session = await response.Content.ReadFromJsonAsync<ViewerSession>();

        session!.Permission.Should().Be(GrantPermissions.View);
        session.StudyInstanceUid.Should().Be(ViewerSessionFactory.SharedStudy);
    }

    /// <summary>
    /// A link is a bearer credential, and a leaked one must not become an
    /// unbounded service. The ceiling is the download route's, on its own key.
    /// </summary>
    [Fact]
    public async Task ALinkStopsMintingSessionsOnceItHasBeenRedeemedTooOften()
    {
        using HttpClient client = _factory.CreateClient();

        for (int attempt = 0; attempt < 20; attempt++)
        {
            using HttpResponseMessage allowed = await client.PostAsync(Path(AToken()), content: null);

            allowed.StatusCode.Should().Be(HttpStatusCode.OK, "attempt {0} is within the window", attempt + 1);
        }

        using HttpResponseMessage refused = await client.PostAsync(Path(AToken()), content: null);

        // The same answer an unknown token gets: being told you have been
        // throttled tells you the token was real.
        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await refused.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// The counter is keyed on the digest. The key reaches Redis, appears in its
    /// keyspace and may be read by an operator, and none of that should yield a
    /// working credential.
    /// </summary>
    [Fact]
    public async Task TheRateLimitKeyCarriesADigestAndNotTheToken()
    {
        string token = AToken();

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(Path(token), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.Cache.Keys.Should().NotBeEmpty();
        _factory.Cache.Keys.Should().OnlyContain(key => key.StartsWith("cloudfilm:share:view:", StringComparison.Ordinal));
        _factory.Cache.Keys.Should().NotContain(key => key.Contains(token, StringComparison.Ordinal));

        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        _factory.Cache.Keys.Should().Contain($"cloudfilm:share:view:{digest}");
    }

    /// <summary>
    /// Viewing and downloading are separate acts. One must not spend the other's
    /// allowance, which is why the two limiters keep separate namespaces.
    /// </summary>
    [Fact]
    public async Task TheViewLimitDoesNotShareTheDownloadRoutesKey()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(Path(AToken()), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.Cache.Keys.Should().NotContain(key => key.StartsWith("cloudfilm:share:download:", StringComparison.Ordinal));
    }

    /// <summary>
    /// A refused redemption is not counted: an unknown token must not be usable
    /// to fill somebody else's window, and a link whose permission is wrong has
    /// not been redeemed.
    /// </summary>
    [Fact]
    public async Task ARefusedRedemptionIsNotCounted()
    {
        _factory.ShareLinks.FindActiveAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns((ShareLinkTarget?)null);

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(Path(AToken()), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _factory.Cache.Keys.Should().BeEmpty();
    }

    /// <summary>
    /// A link that permits downloading permits viewing: the wider permission
    /// covers the narrower one, decided by the grant vocabulary rather than here.
    /// </summary>
    [Fact]
    public async Task ADownloadLinkAlsoOpensTheViewer()
    {
        _factory.ShareLinks.FindActiveAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new ShareLinkTarget(
                ViewerSessionFactory.ShareLinkId,
                ViewerSessionFactory.SharedStudy,
                GrantPermissions.Download));

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(Path(AToken()), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Redemption reads a link and issues a permit. Nothing about it should
    /// reach the archive.
    /// </summary>
    [Fact]
    public async Task RedemptionNeverTouchesTheImagingCatalogue()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(Path(AToken()), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.Catalog
            .DidNotReceive()
            .FindStudyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

/// <summary>
/// The decision nginx asks for before it proxies one DICOMweb request.
/// </summary>
/// <remarks>
/// The most security-sensitive surface in the sprint, and the reason it is
/// tested through HTTP rather than as a function: what the endpoint receives is
/// a header and a cookie, and the tests should be able to send exactly what
/// nginx sends, including the things it should never accept.
/// </remarks>
public sealed class DicomWebGatewayTests : IClassFixture<ViewerSessionFactory>, IDisposable
{
    private const string _path = "/api/v1/share/dicom-web/authorize";

    private readonly ViewerSessionFactory _factory;

    /// <summary>
    /// One client for the class, deliberately not disposed per call: a response
    /// read after its client is gone is a response with an unreadable body, and
    /// several of these tests assert on an empty one.
    /// </summary>
    private readonly HttpClient _client;

    public DicomWebGatewayTests(ViewerSessionFactory factory)
    {
        _factory = factory;

        _factory.ResetSubstitutes();

        _client = factory.CreateClient();
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    /// <summary>Asks the gateway about one original URI.</summary>
    private async Task<HttpResponseMessage> AskAsync(string? originalUri, string? sessionId = ViewerSessionFactory.SessionId)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, _path);

        if (sessionId is not null)
        {
            request.Headers.Add("Cookie", $"{ViewerSessions.CookieName}={sessionId}");
        }

        if (originalUri is not null)
        {
            request.Headers.Add("X-Original-URI", originalUri);
        }

        return await _client.SendAsync(request);
    }

    private static string Retrieve(string studyInstanceUid) =>
        $"/dicom-web/studies/{studyInstanceUid}/series/1.2.3/instances/4.5.6/frames/1";

    // -----------------------------------------------------------------------
    // What is admitted.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ARequestForTheSessionsOwnStudyIsAdmitted()
    {
        using HttpResponseMessage response = await AskAsync(Retrieve(ViewerSessionFactory.SharedStudy));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MetadataForTheSessionsOwnStudyIsAdmitted()
    {
        using HttpResponseMessage response =
            await AskAsync($"/dicom-web/studies/{ViewerSessionFactory.SharedStudy}/metadata");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// The viewer searches for its own study by identifier. That is the one
    /// search shape a session admits.
    /// </summary>
    [Fact]
    public async Task ASearchFilteredToTheSessionsStudyIsAdmitted()
    {
        using HttpResponseMessage response =
            await AskAsync($"/dicom-web/studies?StudyInstanceUID={ViewerSessionFactory.SharedStudy}&includefield=all");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Clients name the filter either way, so both spellings are accepted -
    /// refusing the tag form would break the viewer rather than protect anything.
    /// </summary>
    [Fact]
    public async Task ASearchFilteredByTagNumberIsAdmitted()
    {
        using HttpResponseMessage response =
            await AskAsync($"/dicom-web/series?0020000D={ViewerSessionFactory.SharedStudy}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task APercentEncodedStudyIdentifierIsAdmitted()
    {
        using HttpResponseMessage response =
            await AskAsync($"/dicom-web/studies/{Uri.EscapeDataString(ViewerSessionFactory.SharedStudy)}/series");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// The gate asks whether the session permits viewing, not which permission it
    /// holds. A view-only session - the default here - and a download session are
    /// both admitted, by the same ladder the rest of the system uses.
    /// </summary>
    [Fact]
    public async Task ASessionFromADownloadLinkIsAdmittedLikeAViewOne()
    {
        _factory.ViewerSessions
            .FindAsync(ViewerSessionFactory.SessionId, Arg.Any<CancellationToken>())
            .Returns(new ViewerSessionState(
                ViewerSessionFactory.SharedStudy,
                GrantPermissions.Download,
                ViewerSessionFactory.ShareLinkId));

        using HttpResponseMessage response = await AskAsync(Retrieve(ViewerSessionFactory.SharedStudy));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// A session issued to a signed-in clinician carries no link, and no link is
    /// looked up: there is nothing to revoke, and the database is not troubled
    /// once per image.
    /// </summary>
    [Fact]
    public async Task ASessionThatCameFromNoLinkIsNotCheckedAgainstOne()
    {
        _factory.ViewerSessions
            .FindAsync(ViewerSessionFactory.SessionId, Arg.Any<CancellationToken>())
            .Returns(new ViewerSessionState(
                ViewerSessionFactory.SharedStudy,
                GrantPermissions.View,
                ShareLinkId: null));

        using HttpResponseMessage response = await AskAsync(Retrieve(ViewerSessionFactory.SharedStudy));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.ShareLinkStatus
            .DidNotReceive()
            .IsActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // What is refused.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ARequestWithNoCookieIsRefused()
    {
        using HttpResponseMessage response =
            await AskAsync(Retrieve(ViewerSessionFactory.SharedStudy), sessionId: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ARequestWithAnUnknownSessionIsRefused()
    {
        _factory.ViewerSessions.FindAsync("stranger", Arg.Any<CancellationToken>())
            .Returns((ViewerSessionState?)null);

        using HttpResponseMessage response =
            await AskAsync(Retrieve(ViewerSessionFactory.SharedStudy), sessionId: "stranger");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The session names one study. Holding it must not open the one next to it.
    /// </summary>
    [Fact]
    public async Task ARequestForAnotherStudyIsRefused()
    {
        using HttpResponseMessage response = await AskAsync(Retrieve(ViewerSessionFactory.OtherStudy));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Identifiers are compared whole. A prefix match would admit every study
    /// whose identifier begins with the permitted one, which in DICOM is a
    /// common way for sibling studies to be numbered.
    /// </summary>
    [Fact]
    public async Task AStudyWhoseIdentifierMerelyStartsWithThePermittedOneIsRefused()
    {
        using HttpResponseMessage response = await AskAsync(Retrieve(ViewerSessionFactory.SharedStudy + "9"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The case this whole gate exists for. An unfiltered search returns the
    /// archive's index - every study of every patient - so an absent filter is
    /// refused rather than read as "no restriction asked for".
    /// </summary>
    [Fact]
    public async Task AnUnfilteredSearchIsRefused()
    {
        using HttpResponseMessage response = await AskAsync("/dicom-web/studies");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnUnfilteredSearchWithOtherParametersIsRefused()
    {
        using HttpResponseMessage response = await AskAsync("/dicom-web/studies?limit=100&includefield=all");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ASearchFilteredToAnotherStudyIsRefused()
    {
        using HttpResponseMessage response =
            await AskAsync($"/dicom-web/studies?StudyInstanceUID={ViewerSessionFactory.OtherStudy}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Repeating the parameter asks for two studies. It is refused rather than
    /// partly honoured, because what the archive does with the second value is
    /// the archive's business and not something to guess at here.
    /// </summary>
    [Fact]
    public async Task ASearchNamingThePermittedStudyAndAnotherIsRefused()
    {
        using HttpResponseMessage response = await AskAsync(
            $"/dicom-web/studies?StudyInstanceUID={ViewerSessionFactory.SharedStudy}"
            + $"&StudyInstanceUID={ViewerSessionFactory.OtherStudy}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ARequestWithNoOriginalUriIsRefused()
    {
        using HttpResponseMessage response = await AskAsync(originalUri: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Anything outside the DICOMweb root is refused outright. Orthanc's native
    /// REST API lives beside it and would answer a request for the whole archive.
    /// </summary>
    [Theory]
    [InlineData("/studies")]
    [InlineData("/patients")]
    [InlineData("/tools/find")]
    [InlineData("/dicom-webbing/studies")]
    [InlineData("//evil.example.com/dicom-web/studies")]
    [InlineData("http://evil.example.com/dicom-web/studies")]
    public async Task ARequestOutsideTheDicomWebRootIsRefused(string originalUri)
    {
        using HttpResponseMessage response = await AskAsync(originalUri);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Traversal is refused rather than resolved. A path that has already been
    /// matched against a prefix and still contains ".." is either an attack or a
    /// bug, and neither should be served.
    /// </summary>
    [Theory]
    [InlineData("/dicom-web/studies/1.2.840.113564.1/../1.2.840.113564.999/series")]
    [InlineData("/dicom-web/studies/1.2.840.113564.1/%2e%2e/1.2.840.113564.999")]
    public async Task ATraversalIsRefused(string originalUri)
    {
        using HttpResponseMessage response = await AskAsync(originalUri);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Revocation has to stop the next image, not the next session. The link is
    /// checked on every request for exactly this case.
    /// </summary>
    [Fact]
    public async Task ASessionWhoseLinkWasWithdrawnStopsWorkingImmediately()
    {
        _factory.ShareLinkStatus
            .IsActiveAsync(ViewerSessionFactory.ShareLinkId, Arg.Any<CancellationToken>())
            .Returns(false);

        using HttpResponseMessage response = await AskAsync(Retrieve(ViewerSessionFactory.SharedStudy));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TheLinkIsCheckedOnEveryRequestRatherThanOncePerSession()
    {
        using (await AskAsync(Retrieve(ViewerSessionFactory.SharedStudy)))
        {
        }

        using (await AskAsync(Retrieve(ViewerSessionFactory.SharedStudy)))
        {
        }

        await _factory.ShareLinkStatus
            .Received(2)
            .IsActiveAsync(ViewerSessionFactory.ShareLinkId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A session that does not permit viewing admits nothing, whatever it names.
    /// </summary>
    [Fact]
    public async Task ASessionWithoutViewPermissionIsRefused()
    {
        _factory.ViewerSessions
            .FindAsync(ViewerSessionFactory.SessionId, Arg.Any<CancellationToken>())
            .Returns(new ViewerSessionState(
                ViewerSessionFactory.SharedStudy,
                "Nonsense",
                ViewerSessionFactory.ShareLinkId));

        using HttpResponseMessage response = await AskAsync(Retrieve(ViewerSessionFactory.SharedStudy));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// nginx understands 2xx and 401 from an authorisation subrequest and turns
    /// anything else into a 500 for the person holding the browser. Every
    /// refusal in this suite must therefore be exactly 401, and the body empty.
    /// </summary>
    [Fact]
    public async Task ARefusalIsAlwaysAnEmpty401()
    {
        using HttpResponseMessage response = await AskAsync("/dicom-web/studies");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// The gate decides from a cookie and a header. It must not need, or take,
    /// an account.
    /// </summary>
    [Fact]
    public async Task TheGateIsReachableWithoutAnAccount()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, _path);

        request.Headers.Add("Cookie", $"{ViewerSessions.CookieName}={ViewerSessionFactory.SessionId}");
        request.Headers.Add("X-Original-URI", Retrieve(ViewerSessionFactory.SharedStudy));

        using HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        request.Headers.Authorization.Should().BeNull();
    }

    /// <summary>
    /// The gate answers from the session and the URI. It has no business asking
    /// the archive anything.
    /// </summary>
    [Fact]
    public async Task TheGateNeverTouchesTheImagingCatalogue()
    {
        using (await AskAsync(Retrieve(ViewerSessionFactory.SharedStudy)))
        {
        }

        await _factory.Catalog
            .DidNotReceive()
            .FindStudyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
