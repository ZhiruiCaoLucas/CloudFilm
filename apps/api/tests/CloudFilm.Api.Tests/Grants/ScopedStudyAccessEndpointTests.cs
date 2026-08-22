using System.Net;
using System.Net.Http.Headers;
using CloudFilm.Application.Abstractions;
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

namespace CloudFilm.Api.Tests.Grants;

/// <summary>
/// A host with the access policy, the imaging catalogue and the audit log
/// substituted.
/// </summary>
/// <remarks>
/// <para>
/// The policy is faked here rather than exercised, because the outcome that
/// matters most to this suite - a caller scoped to one study - cannot be
/// produced over HTTP today: the application-role policy turns a roleless
/// account away with 403 before any endpoint is reached. Substituting it is what
/// lets the endpoint's own handling of each outcome be tested now rather than
/// when a later sprint first produces one.
/// </para>
/// <para>
/// What the real policy decides is covered by <see cref="StudyAccessPolicyTests"/>,
/// including the guarantee that a clinical role is admitted unchanged.
/// </para>
/// </remarks>
public sealed class ScopedStudyAccessFactory : WebApplicationFactory<Program>
{
    /// <summary>Gets the substituted access policy.</summary>
    public IStudyAccessPolicy AccessPolicy { get; } = Substitute.For<IStudyAccessPolicy>();

    /// <summary>Gets the substituted imaging catalogue.</summary>
    public IImagingCatalog Catalog { get; } = Substitute.For<IImagingCatalog>();

    /// <summary>Gets the substituted audit log.</summary>
    public IAuditLogService AuditLog { get; } = Substitute.For<IAuditLogService>();

    /// <summary>The study the tests ask for.</summary>
    public const string GrantedStudy = "1.2.840.113564.1";

    /// <summary>A sibling study belonging to the same patient.</summary>
    public const string SiblingStudy = "1.2.840.113564.2";

    /// <summary>Returns every double to its as-created state.</summary>
    public void ResetSubstitutes()
    {
        AccessPolicy.ClearSubstitute(ClearOptions.All);
        Catalog.ClearSubstitute(ClearOptions.All);
        AuditLog.ClearSubstitute(ClearOptions.All);

        AccessPolicy
            .AuthoriseAsync(
                Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(StudyAccessScope.FullCatalog);

        Catalog.FindStudyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AStudyWithASibling());

        AuditLog.RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    /// <summary>A patient holding two studies, one of which is the one asked for.</summary>
    public static StudyContext AStudyWithASibling()
    {
        StudySummary selected = new(GrantedStudy, "CT", "Chest", "ACC-1", "2018-03-14", "Completed", 2, 57);
        StudySummary sibling = new(SiblingStudy, "MR", "Head", "ACC-2", "2019-05-02", "Completed", 3, 91);

        return new StudyContext(
            new PatientSummary("1000192961", "Fixture Patient", "Unknown", "1971-07-24"),
            [selected, sibling],
            selected,
            []);
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

            services.RemoveAll<IImagingCatalog>();
            services.AddScoped(_ => Catalog);

            services.RemoveAll<IAuditLogService>();
            services.AddScoped(_ => AuditLog);
        });
    }
}

/// <summary>
/// What the study endpoint does with each access outcome.
/// </summary>
public sealed class ScopedStudyAccessEndpointTests : IClassFixture<ScopedStudyAccessFactory>
{
    private static readonly Guid _actor = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static string Path(string studyInstanceUid) =>
        $"/api/v1/studies/{Uri.EscapeDataString(studyInstanceUid)}";

    private readonly ScopedStudyAccessFactory _factory;

    public ScopedStudyAccessEndpointTests(ScopedStudyAccessFactory factory)
    {
        _factory = factory;

        _factory.ResetSubstitutes();
    }

    private HttpClient CreateClientWithRoles(params string[] roles)
    {
        HttpClient client = _factory.CreateClient();

        using IServiceScope scope = _factory.Services.CreateScope();
        IAccessTokenIssuer issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        AccessToken token = issuer.Issue(_actor, "clinician@example.test", "Dr. Example", roles);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        return client;
    }

    // -----------------------------------------------------------------------
    // Existing behaviour.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Full access returns exactly what it always returned, siblings included.
    /// </summary>
    [Theory]
    [InlineData(RoleNames.Administrator)]
    [InlineData(RoleNames.Clinician)]
    public async Task AFullyAuthorisedCallerStillSeesTheWholeContext(string role)
    {
        using HttpClient client = CreateClientWithRoles(role);

        using HttpResponseMessage response = await client.GetAsync(Path(ScopedStudyAccessFactory.GrantedStudy));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(ScopedStudyAccessFactory.GrantedStudy);
        body.Should().Contain(ScopedStudyAccessFactory.SiblingStudy);
    }

    // -----------------------------------------------------------------------
    // Order of operations.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A refusal must cost the archive nothing. Asking it first would leave a
    /// timing difference between a study that exists and one that does not, and
    /// would spend a hospital's imaging system on a request that was never going
    /// to be answered.
    /// </summary>
    [Fact]
    public async Task ARefusedCallerNeverReachesTheArchive()
    {
        _factory.AccessPolicy
            .AuthoriseAsync(
                Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(StudyAccessScope.Denied);

        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.GetAsync(Path(ScopedStudyAccessFactory.GrantedStudy));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _factory.Catalog.DidNotReceiveWithAnyArgs().FindStudyAsync(default!, default);
        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    [Fact]
    public async Task AsksTheAccessPolicyAboutTheStudyBeingRequested()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.GetAsync(Path(ScopedStudyAccessFactory.GrantedStudy));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AccessPolicy.Received(1).AuthoriseAsync(
            Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
            ScopedStudyAccessFactory.GrantedStudy,
            GrantPermissions.View,
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // A refusal and an absence must look the same.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A Study Instance UID is all it takes to open a study in the viewer, so
    /// "exists but not for you" is a disclosure. Both answers are 404, with the
    /// same body.
    /// </summary>
    [Fact]
    public async Task ARefusalIsIndistinguishableFromAStudyThatDoesNotExist()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        _factory.AccessPolicy
            .AuthoriseAsync(
                Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(StudyAccessScope.Denied);

        using HttpResponseMessage refused = await client.GetAsync(Path(ScopedStudyAccessFactory.GrantedStudy));
        string refusedBody = await refused.Content.ReadAsStringAsync();

        _factory.ResetSubstitutes();
        _factory.Catalog.FindStudyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((StudyContext?)null);

        using HttpResponseMessage absent = await client.GetAsync(Path(ScopedStudyAccessFactory.GrantedStudy));
        string absentBody = await absent.Content.ReadAsStringAsync();

        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);
        absent.StatusCode.Should().Be(HttpStatusCode.NotFound);
        refusedBody.Should().Be(absentBody);
    }

    // -----------------------------------------------------------------------
    // Scoped access.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A caller admitted by a grant was granted one study, not the patient's
    /// imaging history. The siblings' identifiers are removed, because a UID is
    /// the whole of what is needed to open one.
    /// </summary>
    [Fact]
    public async Task AScopedCallerIsNotToldAboutTheOtherStudies()
    {
        _factory.AccessPolicy
            .AuthoriseAsync(
                Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(StudyAccessScope.GrantedStudyOnly);

        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.GetAsync(Path(ScopedStudyAccessFactory.GrantedStudy));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(ScopedStudyAccessFactory.GrantedStudy);
        body.Should().NotContain(ScopedStudyAccessFactory.SiblingStudy);
        body.Should().NotContain("ACC-2");
        body.Should().NotContain("Head");
    }

    // -----------------------------------------------------------------------
    // Audit.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AnAdmittedReadStillRecordsExactlyOneStudyView()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.GetAsync(Path(ScopedStudyAccessFactory.GrantedStudy));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AuditLog.Received(1).RecordAsync(
            Arg.Is<AuditEntry>(entry =>
                entry.Action == AuditAction.ViewStudy
                && entry.ResourceType == AuditResourceType.Study
                && entry.ResourceId == ScopedStudyAccessFactory.GrantedStudy),
            Arg.Any<CancellationToken>());

        await _factory.AuditLog.Received(1).RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A scoped read is still a disclosure of a patient's study, so it is
    /// recorded exactly like any other - one event, same action.
    /// </summary>
    [Fact]
    public async Task AScopedReadIsRecordedTheSameWay()
    {
        _factory.AccessPolicy
            .AuthoriseAsync(
                Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(StudyAccessScope.GrantedStudyOnly);

        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.GetAsync(Path(ScopedStudyAccessFactory.GrantedStudy));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AuditLog.Received(1).RecordAsync(
            Arg.Is<AuditEntry>(entry => entry.Action == AuditAction.ViewStudy),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // The read mark is guarded by the same rule.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Read state is keyed by Study Instance UID, so an unguarded read mark
    /// would answer questions about studies the caller may not see.
    /// </summary>
    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task ARefusedCallerCannotProbeAStudyThroughItsReadMark(string method)
    {
        _factory.AccessPolicy
            .AuthoriseAsync(
                Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(StudyAccessScope.Denied);

        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);
        using HttpRequestMessage request = new(
            new HttpMethod(method),
            $"/api/v1/imaging/studies/{ScopedStudyAccessFactory.GrantedStudy}/read-state");

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }
}
