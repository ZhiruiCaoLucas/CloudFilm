using System.Net;
using System.Security.Cryptography;
using System.Text;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auditing;
using CloudFilm.Application.Grants;
using CloudFilm.Application.Imaging;
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
/// A host with the share link store, the download source, the audit log and the
/// cache substituted.
/// </summary>
/// <remarks>
/// No token of the JWT kind is issued anywhere in this suite. Every request is
/// made by a plain client with no Authorization header, which is what the
/// endpoint under test has to accept - and what every other endpoint has to keep
/// refusing.
/// </remarks>
public sealed class AnonymousShareFactory : WebApplicationFactory<Program>
{
    /// <summary>A cache that remembers what it was asked for.</summary>
    /// <remarks>
    /// A hand-written fake rather than a substitute: the assertions are about the
    /// key - that it carries a digest and never the token - and about counting
    /// across calls, both of which read more clearly here than as a stack of
    /// argument matchers.
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

    /// <summary>A stream that reports whether it was disposed.</summary>
    public sealed class TrackedStream(byte[] payload) : MemoryStream(payload)
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;

            base.Dispose(disposing);
        }
    }

    /// <summary>The study the share link leads to.</summary>
    public const string SharedStudy = "1.2.840.113564.1";

    /// <summary>A study the caller must never be able to reach with that link.</summary>
    public const string OtherStudy = "1.2.840.113564.999";

    /// <summary>The media type the archive answers with.</summary>
    public const string DicomMediaType = "multipart/related; type=\"application/dicom\"; boundary=abc123";

    /// <summary>Bytes standing in for a study.</summary>
    public static byte[] Payload { get; } = Encoding.ASCII.GetBytes("DICM-not-a-real-study");

    /// <summary>Gets the substituted share link store.</summary>
    public IShareLinkStore ShareLinks { get; } = Substitute.For<IShareLinkStore>();

    /// <summary>Gets the substituted download source.</summary>
    public IStudyDownloadSource Downloads { get; } = Substitute.For<IStudyDownloadSource>();

    /// <summary>Gets the substituted imaging catalogue, which must never be used.</summary>
    public IImagingCatalog Catalog { get; } = Substitute.For<IImagingCatalog>();

    /// <summary>Gets the substituted audit log.</summary>
    public IAuditLogService AuditLog { get; } = Substitute.For<IAuditLogService>();

    /// <summary>Gets the recording cache.</summary>
    public RecordingCache Cache { get; } = new();

    /// <summary>Gets the recording unit of work.</summary>
    public RecordingUnitOfWork UnitOfWork { get; } = new();

    /// <summary>Gets the stream handed to the last download.</summary>
    public TrackedStream? LastStream { get; private set; }

    /// <summary>Returns every double to its as-created state.</summary>
    public void ResetSubstitutes()
    {
        ShareLinks.ClearSubstitute(ClearOptions.All);
        Downloads.ClearSubstitute(ClearOptions.All);
        Catalog.ClearSubstitute(ClearOptions.All);
        AuditLog.ClearSubstitute(ClearOptions.All);
        Cache.Reset();
        UnitOfWork.Reset();
        LastStream = null;

        ShareLinks.FindActiveAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new ShareLinkTarget(Guid.NewGuid(), SharedStudy, GrantPermissions.Download));

        Downloads.OpenStudyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                LastStream = new TrackedStream(Payload);

                return new StudyDownload(LastStream, DicomMediaType);
            });

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
            services.AddScoped(_ => ShareLinks);

            services.RemoveAll<IStudyDownloadSource>();
            services.AddScoped(_ => Downloads);

            services.RemoveAll<IImagingCatalog>();
            services.AddScoped(_ => Catalog);

            services.RemoveAll<IAuditLogService>();
            services.AddScoped(_ => AuditLog);

            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(_ => UnitOfWork);

            services.RemoveAll<ICacheStore>();
            services.AddSingleton<ICacheStore>(Cache);
        });
    }
}

/// <summary>
/// What a share link's holder can and cannot reach without an account.
/// </summary>
public sealed class AnonymousShareDownloadTests : IClassFixture<AnonymousShareFactory>
{
    private readonly AnonymousShareFactory _factory;

    public AnonymousShareDownloadTests(AnonymousShareFactory factory)
    {
        _factory = factory;

        _factory.ResetSubstitutes();
    }

    /// <summary>A token of the shape the issuer produces: 32 bytes, Base64Url.</summary>
    private static string AToken(byte fill = 0x2A) =>
        WebEncoders.Base64UrlEncode(Enumerable.Repeat(fill, 32).ToArray());

    private static string Path(string token) => $"/api/v1/share/{token}/download";

    // -----------------------------------------------------------------------
    // Anonymous access.
    // -----------------------------------------------------------------------

    /// <summary>
    /// No Authorization header, no account, no role - and the study arrives.
    /// This is the whole point of the feature.
    /// </summary>
    [Fact]
    public async Task AHolderOfTheLinkNeedsNoAccount()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Path(AToken()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Authorization.Should().BeNull();

        byte[] body = await response.Content.ReadAsByteArrayAsync();

        body.Should().Equal(AnonymousShareFactory.Payload);
    }

    [Fact]
    public async Task ThePassedThroughMediaTypeIsPreserved()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Path(AToken()));

        response.Content.Headers.ContentType!.ToString()
            .Should().StartWith("multipart/related");
        response.Content.Headers.ContentType!.ToString()
            .Should().Contain("boundary=abc123", "a multipart body without its boundary is unparseable");
    }

    /// <summary>
    /// A constant filename. Anything drawn from the study would put a patient's
    /// name or record number into the recipient's downloads folder.
    /// </summary>
    [Fact]
    public async Task TheDownloadIsAnAttachmentNamedAfterNobody()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Path(AToken()));

        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");

        string disposition = response.Content.Headers.ContentDisposition!.ToString();

        disposition.Should().Contain("study.dcm");
        disposition.Should().NotContain(AnonymousShareFactory.SharedStudy);
    }

    // -----------------------------------------------------------------------
    // Refusals, and their sameness.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Shape only, and cheap: an obviously impossible token must not cost a
    /// database query.
    /// </summary>
    [Theory]
    [InlineData("short")]
    [InlineData("this-token-is-far-too-long-to-have-come-from-the-issuer-at-all")]
    [InlineData("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    public async Task AMalformedTokenIsRefusedWithoutAskingAnything(string token)
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Path(token));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _factory.ShareLinks.DidNotReceiveWithAnyArgs().FindActiveAsync(default!, default);
        await _factory.Downloads.DidNotReceiveWithAnyArgs().OpenStudyAsync(default!, default);
    }

    /// <summary>
    /// Unknown, expired and revoked all resolve to nothing in the store, so the
    /// endpoint cannot tell them apart even if a later edit wanted it to. What is
    /// asserted here is that the answer is the same as for a token that never
    /// existed - and that nothing downstream was touched.
    /// </summary>
    [Fact]
    public async Task AnUnknownExpiredOrRevokedTokenIsRefused()
    {
        _factory.ShareLinks.FindActiveAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns((ShareLinkTarget?)null);

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Path(AToken()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _factory.Downloads.DidNotReceiveWithAnyArgs().OpenStudyAsync(default!, default);
        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    /// <summary>
    /// A view-only link is refused exactly like an invalid one, because saying
    /// "wrong permission" would confirm the token is real.
    /// </summary>
    /// <remarks>
    /// The link this stands for is now one an administrator can actually issue,
    /// which makes this the boundary that gives View its meaning: a recipient
    /// admitted to the viewer must not be able to pull the study file. The
    /// archive is never asked, and nothing is recorded as downloaded.
    /// </remarks>
    [Fact]
    public async Task AViewOnlyLinkCannotDownload()
    {
        _factory.ShareLinks.FindActiveAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new ShareLinkTarget(Guid.NewGuid(), AnonymousShareFactory.SharedStudy, GrantPermissions.View));

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Path(AToken()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        await _factory.Downloads.DidNotReceiveWithAnyArgs().OpenStudyAsync(default!, default);

        // No DOWNLOAD_STUDY. A refused request is not an access, and recording
        // one would put a retrieval in the trail that never happened.
        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
        _factory.UnitOfWork.Commits.Should().Be(0);
    }

    [Fact]
    public async Task AStudyTheArchiveNoLongerHoldsIsRefused()
    {
        _factory.Downloads.OpenStudyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((StudyDownload?)null);

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Path(AToken()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    /// <summary>
    /// Every refusal is the same response. A difference in body, header or shape
    /// would be a channel for asking questions about tokens and studies.
    /// </summary>
    [Fact]
    public async Task EveryRefusalLooksIdentical()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage malformed = await client.GetAsync(Path("not-a-token"));
        string malformedBody = await malformed.Content.ReadAsStringAsync();

        _factory.ShareLinks.FindActiveAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns((ShareLinkTarget?)null);

        using HttpResponseMessage unknown = await client.GetAsync(Path(AToken()));
        string unknownBody = await unknown.Content.ReadAsStringAsync();

        _factory.ResetSubstitutes();
        _factory.ShareLinks.FindActiveAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new ShareLinkTarget(Guid.NewGuid(), AnonymousShareFactory.SharedStudy, GrantPermissions.View));

        using HttpResponseMessage wrongPermission = await client.GetAsync(Path(AToken()));
        string wrongPermissionBody = await wrongPermission.Content.ReadAsStringAsync();

        malformed.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        wrongPermission.StatusCode.Should().Be(HttpStatusCode.NotFound);

        unknownBody.Should().Be(malformedBody);
        wrongPermissionBody.Should().Be(malformedBody);
        malformedBody.Should().BeEmpty("the least that can be said is nothing at all");
    }

    /// <summary>A refusal must not carry anything about a patient or a study.</summary>
    [Fact]
    public async Task ARefusalDisclosesNothing()
    {
        _factory.ShareLinks.FindActiveAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns((ShareLinkTarget?)null);

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Path(AToken()));

        string body = await response.Content.ReadAsStringAsync();
        string headers = response.Headers.ToString() + response.Content.Headers;

        body.Should().BeEmpty();
        headers.Should().NotContain(AnonymousShareFactory.SharedStudy);
        headers.Should().NotContain("expire");
        headers.Should().NotContain("revoke");
    }

    // -----------------------------------------------------------------------
    // Scope: one token, one study.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The study comes from the link, not the request. There is no parameter to
    /// edit, and one added later would be ignored.
    /// </summary>
    [Fact]
    public async Task TheStudyComesFromTheLinkAndNotTheCaller()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            Path(AToken()) + $"?studyInstanceUid={AnonymousShareFactory.OtherStudy}&study={AnonymousShareFactory.OtherStudy}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.Downloads.Received(1)
            .OpenStudyAsync(AnonymousShareFactory.SharedStudy, Arg.Any<CancellationToken>());
        await _factory.Downloads.DidNotReceive()
            .OpenStudyAsync(AnonymousShareFactory.OtherStudy, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The anonymous path never touches the catalogue, so it cannot inherit the
    /// patient's other studies the way an authenticated study read would.
    /// </summary>
    [Fact]
    public async Task NoPatientOrSiblingStudyIsEverLookedUp()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Path(AToken()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.Catalog.DidNotReceiveWithAnyArgs().FindStudyAsync(default!, default);
        await _factory.Catalog.DidNotReceiveWithAnyArgs().FindPatientAsync(default!, default);
        await _factory.Catalog.DidNotReceiveWithAnyArgs().ListPatientsAsync(default);
    }

    // -----------------------------------------------------------------------
    // Audit.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ADownloadIsRecordedOnceWithNoActor()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Path(AToken()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AuditLog.Received(1).RecordAsync(
            Arg.Is<AuditEntry>(entry =>
                entry.Action == AuditAction.DownloadStudy
                && entry.ResourceType == AuditResourceType.Study
                && entry.ResourceId == AnonymousShareFactory.SharedStudy
                && entry.ActorUserId == null),
            Arg.Any<CancellationToken>());

        await _factory.AuditLog.Received(1).RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Neither the token nor its digest may appear anywhere in the trail: an
    /// audit row holding a live credential is a second place to steal it from.
    /// </summary>
    [Fact]
    public async Task TheTrailNeverCarriesTheToken()
    {
        string token = AToken();
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Path(token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AuditLog.DidNotReceive().RecordAsync(
            Arg.Is<AuditEntry>(entry =>
                entry.ResourceId != null
                && (entry.ResourceId.Contains(token, StringComparison.Ordinal)
                    || entry.ResourceId.Contains(digest, StringComparison.OrdinalIgnoreCase))),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An unrecorded retrieval must not happen at all, so the failure has to
    /// arrive before the body does - and it does, because nothing has been
    /// written when the audit is taken.
    /// </summary>
    [Fact]
    public async Task AnAuditFailureWithholdsTheStudy()
    {
        _factory.AuditLog.RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Path(AToken()));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        byte[] body = await response.Content.ReadAsByteArrayAsync();

        body.Should().NotEqual(AnonymousShareFactory.Payload);
        Encoding.UTF8.GetString(body).Should().NotContain("audit unavailable");

        _factory.UnitOfWork.Commits.Should().Be(0);
        _factory.UnitOfWork.Rollbacks.Should().Be(1);

        // The connection to the archive is closed rather than left open behind a
        // failed request.
        _factory.LastStream!.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task ASuccessfulDownloadCommitsOnce()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Path(AToken()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.UnitOfWork.Commits.Should().Be(1);
        _factory.UnitOfWork.Rollbacks.Should().Be(0);
    }

    /// <summary>
    /// The stream is closed once the response has been written, which is what
    /// proves the endpoint hands it on rather than copying it.
    /// </summary>
    [Fact]
    public async Task TheArchiveConnectionIsReleasedAfterTheResponse()
    {
        using HttpClient client = _factory.CreateClient();

        using (HttpResponseMessage response = await client.GetAsync(Path(AToken())))
        {
            await response.Content.ReadAsByteArrayAsync();
        }

        _factory.LastStream!.WasDisposed.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Rate limiting.
    // -----------------------------------------------------------------------

    /// <summary>
    /// One link cannot be turned into a download service. The limit bounds what a
    /// leaked URL is worth before somebody revokes it.
    /// </summary>
    [Fact]
    public async Task ALinkStopsWorkingOnceItHasBeenRedeemedTooOften()
    {
        string token = AToken();

        using HttpClient client = _factory.CreateClient();

        for (int attempt = 0; attempt < 20; attempt++)
        {
            using HttpResponseMessage allowed = await client.GetAsync(Path(token));

            allowed.StatusCode.Should().Be(HttpStatusCode.OK, $"attempt {attempt + 1} is within the limit");
        }

        using HttpResponseMessage refused = await client.GetAsync(Path(token));

        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await refused.Content.ReadAsStringAsync()).Should().BeEmpty(
            "a throttled caller learns nothing a caller with a bad token would not");
    }

    /// <summary>
    /// The key travels to Redis and may be read by anyone who can list its
    /// keyspace, so it carries a digest and never the credential.
    /// </summary>
    [Fact]
    public async Task TheRateLimitKeyCarriesADigestAndNotTheToken()
    {
        string token = AToken();
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(Path(token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.Cache.Keys.Should().NotBeEmpty();
        _factory.Cache.Keys.Should().OnlyContain(key => !key.Contains(token, StringComparison.Ordinal));
        _factory.Cache.Keys.Should().Contain(key => key.Contains(digest, StringComparison.OrdinalIgnoreCase));
    }
}
