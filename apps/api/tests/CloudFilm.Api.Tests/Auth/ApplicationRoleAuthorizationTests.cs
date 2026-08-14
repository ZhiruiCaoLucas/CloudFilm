using System.Net;
using System.Net.Http.Headers;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Access;
using CloudFilm.Application.Auditing;
using CloudFilm.Application.Auth;
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

namespace CloudFilm.Api.Tests.Auth;

/// <summary>
/// A host whose every protected dependency is substituted.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here fakes authentication or authorisation. Requests carry real
/// tokens signed by the host's own issuer, and they travel through the bearer
/// handler, the role claims, the policy and the endpoint exactly as a browser's
/// would. What is faked is everything <em>behind</em> the endpoints, so that a
/// call which gets through can be recognised by the fact that it arrived rather
/// than by whether Orthanc happened to be running.
/// </para>
/// <para>
/// That distinction is the whole point of this suite: authorisation is a
/// server-side rule, and a test that reaches the handler directly would prove
/// nothing about it.
/// </para>
/// </remarks>
public sealed class AuthorizationMatrixFactory : WebApplicationFactory<Program>
{
    /// <summary>A pass-through unit of work; the read-state mutations need one.</summary>
    private sealed class ImmediateUnitOfWork : IUnitOfWork
    {
        public Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default) => operation(cancellationToken);
    }

    /// <summary>Gets the substituted imaging catalogue.</summary>
    public IImagingCatalog Catalog { get; } = Substitute.For<IImagingCatalog>();

    /// <summary>Gets the substituted search catalogue.</summary>
    public IStudySearchCatalog Search { get; } = Substitute.For<IStudySearchCatalog>();

    /// <summary>Gets the substituted recent-patient reader.</summary>
    public IRecentPatientAccess Recent { get; } = Substitute.For<IRecentPatientAccess>();

    /// <summary>Gets the substituted read-state store.</summary>
    public IStudyReadStateStore ReadState { get; } = Substitute.For<IStudyReadStateStore>();

    /// <summary>Gets the substituted audit log.</summary>
    public IAuditLogService AuditLog { get; } = Substitute.For<IAuditLogService>();

    /// <summary>Returns every substitute to its as-created state.</summary>
    public void ResetSubstitutes()
    {
        Catalog.ClearSubstitute(ClearOptions.All);
        Search.ClearSubstitute(ClearOptions.All);
        Recent.ClearSubstitute(ClearOptions.All);
        ReadState.ClearSubstitute(ClearOptions.All);
        AuditLog.ClearSubstitute(ClearOptions.All);

        // The catalogue finds nothing, so an admitted caller reaches the endpoint
        // and gets 404 - which is exactly the "not 401 and not 403" this suite
        // asserts, and proves the handler ran.
        Catalog.FindPatientAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PatientRecord?)null);
        Catalog.FindStudyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((StudyContext?)null);

        Search.SearchStudiesAsync(Arg.Any<StudySearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new StudySearchResult([], HasMore: false, Page: 0, PageSize: 25));

        Recent.GetRecentPatientsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RecentPatient>>([]));

        ReadState.IsReadAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        ReadState.MarkReadAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

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

            services.RemoveAll<IImagingCatalog>();
            services.AddScoped(_ => Catalog);

            services.RemoveAll<IStudySearchCatalog>();
            services.AddScoped(_ => Search);

            services.RemoveAll<IRecentPatientAccess>();
            services.AddScoped(_ => Recent);

            services.RemoveAll<IStudyReadStateStore>();
            services.AddScoped(_ => ReadState);

            services.RemoveAll<IAuditLogService>();
            services.AddScoped(_ => AuditLog);

            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork, ImmediateUnitOfWork>();
        });
    }
}

/// <summary>
/// Who may reach the endpoints that return patient data.
/// </summary>
/// <remarks>
/// One matrix, five routes, five kinds of caller. The assertions are status
/// codes and whether the dependency behind the endpoint was reached - the
/// behaviour of those endpoints is covered by their own suites and is not
/// repeated here once per role.
/// </remarks>
public sealed class ApplicationRoleAuthorizationTests : IClassFixture<AuthorizationMatrixFactory>
{
    private const string _patient = "/api/v1/patients/1000192961";
    private const string _study = "/api/v1/studies/1.2.840.113564.1";
    private const string _search = "/api/v1/studies/search?patientName=DOE";
    private const string _recent = "/api/v1/access/recent";
    private const string _readState = "/api/v1/imaging/studies/1.2.840.113564.1/read-state";

    private static readonly Guid _actor = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly AuthorizationMatrixFactory _factory;

    public ApplicationRoleAuthorizationTests(AuthorizationMatrixFactory factory)
    {
        _factory = factory;

        _factory.ResetSubstitutes();
    }

    /// <summary>
    /// Builds a client carrying a real token with the given roles.
    /// </summary>
    /// <remarks>
    /// Issued by the application's own registered issuer, so the token is signed
    /// with the key the bearer handler validates against and its role claims are
    /// written exactly as production writes them. Nothing in production is
    /// altered to let a test name a role the seeder never creates - the issuer
    /// has always taken the roles it is given, and deciding which ones an account
    /// really holds belongs to the sign-in path, not here.
    /// </remarks>
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
    // Anonymous.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(_patient)]
    [InlineData(_study)]
    [InlineData(_search)]
    [InlineData(_recent)]
    [InlineData(_readState)]
    public async Task AnAnonymousCallerIsNotAuthenticated(string path)
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path);

        response.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "an unauthenticated caller has not failed authorisation, they have failed authentication");
    }

    // -----------------------------------------------------------------------
    // Admitted.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Both roles reach the same functionality today, so the matrix is the same
    /// for each. What is asserted is admission, not what the endpoint then does.
    /// </summary>
    [Theory]
    [InlineData(_patient)]
    [InlineData(_study)]
    [InlineData(_search)]
    [InlineData(_recent)]
    [InlineData(_readState)]
    public async Task AnAdministratorIsAdmitted(string path)
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator);

        using HttpResponseMessage response = await client.GetAsync(path);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(_patient)]
    [InlineData(_study)]
    [InlineData(_search)]
    [InlineData(_recent)]
    [InlineData(_readState)]
    public async Task AClinicianIsAdmitted(string path)
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage response = await client.GetAsync(path);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnAccountHoldingBothRolesIsAdmitted()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Administrator, RoleNames.Clinician);

        using HttpResponseMessage response = await client.GetAsync(_recent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // -----------------------------------------------------------------------
    // Refused.
    // -----------------------------------------------------------------------

    /// <summary>
    /// An account nobody has given a role to signs in and is refused everything
    /// else. It is never promoted by default.
    /// </summary>
    [Theory]
    [InlineData(_patient)]
    [InlineData(_study)]
    [InlineData(_search)]
    [InlineData(_recent)]
    [InlineData(_readState)]
    public async Task AnAccountWithNoApplicationRoleIsForbidden(string path)
    {
        using HttpClient client = CreateClientWithRoles();

        using HttpResponseMessage response = await client.GetAsync(path);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "the caller authenticated; what they lack is a role");
    }

    /// <summary>
    /// A role this application never defined grants nothing. Membership of some
    /// other system's group is not membership of ours.
    /// </summary>
    [Theory]
    [InlineData(_patient)]
    [InlineData(_study)]
    [InlineData(_search)]
    [InlineData(_recent)]
    [InlineData(_readState)]
    public async Task AnAccountWithAnUnrelatedRoleIsForbidden(string path)
    {
        using HttpClient client = CreateClientWithRoles("SomeOtherRole");

        using HttpResponseMessage response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -----------------------------------------------------------------------
    // A refusal must stop before the data.
    // -----------------------------------------------------------------------

    /// <summary>
    /// 403 is not enough on its own: the refusal has to happen before anything
    /// reads a patient. This asserts the dependency behind each endpoint was
    /// never reached.
    /// </summary>
    [Fact]
    public async Task ARefusedCallerNeverReachesTheDataOrTheAuditTrail()
    {
        using HttpClient client = CreateClientWithRoles("SomeOtherRole");

        foreach (string path in new[] { _patient, _study, _search, _recent, _readState })
        {
            using HttpResponseMessage response = await client.GetAsync(path);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        await _factory.Catalog.DidNotReceiveWithAnyArgs().FindPatientAsync(default!, default);
        await _factory.Catalog.DidNotReceiveWithAnyArgs().FindStudyAsync(default!, default);
        await _factory.Search.DidNotReceiveWithAnyArgs().SearchStudiesAsync(default!, default);
        await _factory.Recent.DidNotReceiveWithAnyArgs().GetRecentPatientsAsync(default, default);
        await _factory.ReadState.DidNotReceiveWithAnyArgs().IsReadAsync(default, default!, default);
        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    [Fact]
    public async Task AnAdmittedCallerReachesTheEndpointBehindThePolicy()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage patient = await client.GetAsync(_patient);

        patient.StatusCode.Should().Be(HttpStatusCode.NotFound, "the catalogue was asked and found nothing");

        await _factory.Catalog.Received(1).FindPatientAsync("1000192961", Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Mutations are covered by the same rule.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ARefusedCallerCannotChangeReadState()
    {
        using HttpClient client = CreateClientWithRoles();

        using HttpResponseMessage put = await client.PutAsync(_readState, content: null);
        using HttpResponseMessage delete = await client.DeleteAsync(_readState);

        put.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await _factory.ReadState.DidNotReceiveWithAnyArgs().MarkReadAsync(default, default!, default);
        await _factory.ReadState.DidNotReceiveWithAnyArgs().MarkUnreadAsync(default, default!, default);
        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    [Fact]
    public async Task AnAdmittedCallerCanChangeReadState()
    {
        using HttpClient client = CreateClientWithRoles(RoleNames.Clinician);

        using HttpResponseMessage put = await client.PutAsync(_readState, content: null);

        put.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.ReadState.Received(1)
            .MarkReadAsync(_actor, "1.2.840.113564.1", Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // What the policy must not have touched.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Signing in and reading who you are must keep working for an account with
    /// no role - otherwise an operator cannot even discover that a role is what
    /// they are missing.
    /// </summary>
    [Fact]
    public async Task AnAccountWithNoRoleCanStillReadItsOwnIdentity()
    {
        using HttpClient client = CreateClientWithRoles();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/v1/system/info")]
    [InlineData("/health/live")]
    public async Task TheIntentionallyAnonymousEndpointsStayAnonymous(string path)
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Readiness runs the PostgreSQL, Redis and Retrieve checks, which this host
    /// has none of, so a healthy 200 is not something this test can honestly
    /// expect. What it can assert is that the new policy did not close it.
    /// </summary>
    [Fact]
    public async Task ReadinessIsNotRefusedForWantOfARole()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }
}
