using System.Net;
using System.Net.Http.Headers;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auditing;
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
using CloudFilm.Application.Auth;

namespace CloudFilm.Api.Tests.Imaging;

/// <summary>
/// A host with the search catalogue and the audit log substituted.
/// </summary>
/// <remarks>
/// The real catalogue calls the Retrieve Service, which this suite runs without.
/// Substituting it is also what makes the assertions possible: what matters is
/// the contract the endpoint builds, and whether it recorded the search before
/// answering.
/// </remarks>
public sealed class SearchApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Gets the substituted search catalogue.</summary>
    public IStudySearchCatalog Catalog { get; } = Substitute.For<IStudySearchCatalog>();

    /// <summary>Gets the substituted audit log.</summary>
    public IAuditLogService AuditLog { get; } = Substitute.For<IAuditLogService>();

    /// <summary>Gets the result the catalogue answers with by default.</summary>
    public static StudySearchResult AResult { get; } = new(
        [
            new StudySearchItem(
                new PatientSummary("1000192961", "Fixture Patient", "Unknown", "1971-07-24"),
                new StudySummary(
                    "1.2.840.113564.145320946818674.1292.636566240007310666.706",
                    "CR",
                    "Untitled study",
                    "ACC-1",
                    "2018-03-14",
                    "Completed",
                    1,
                    2)),
        ],
        HasMore: false,
        Page: 0,
        PageSize: 25);

    /// <summary>Returns both substitutes to their as-created state.</summary>
    /// <remarks>
    /// <c>ClearOptions.All</c> rather than <c>ClearReceivedCalls()</c>: the
    /// latter keeps configured return values, so a test that arranges a failure
    /// would leave every later test failing depending on the order xUnit picked.
    /// </remarks>
    public void ResetSubstitutes()
    {
        Catalog.ClearSubstitute(ClearOptions.All);
        AuditLog.ClearSubstitute(ClearOptions.All);

        Catalog
            .SearchStudiesAsync(Arg.Any<StudySearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(AResult);

        AuditLog
            .RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
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

            services.RemoveAll<IStudySearchCatalog>();
            services.AddScoped(_ => Catalog);

            services.RemoveAll<IAuditLogService>();
            services.AddScoped(_ => AuditLog);
        });
    }
}

/// <summary>
/// The HTTP contract of study search: who may call it, what it refuses, and what
/// it records before it answers.
/// </summary>
public sealed class SearchEndpointTests : IClassFixture<SearchApiFactory>
{
    private const string _path = "/api/v1/studies/search";

    private static readonly Guid _actor = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly SearchApiFactory _factory;

    public SearchEndpointTests(SearchApiFactory factory)
    {
        _factory = factory;

        _factory.ResetSubstitutes();
    }

    private HttpClient CreateSignedInClient()
    {
        HttpClient client = _factory.CreateClient();

        using IServiceScope scope = _factory.Services.CreateScope();
        IAccessTokenIssuer issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        AccessToken token = issuer.Issue(
            _actor,
            "clinician@example.test",
            "Dr. Example",
            [RoleNames.Clinician]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        return client;
    }

    // -----------------------------------------------------------------------
    // Authentication.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The one endpoint that returns patients the caller did not already name,
    /// so an anonymous call must not reach the archive at all.
    /// </summary>
    [Fact]
    public async Task RejectsAnAnonymousSearch()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(_path + "?patientName=DOE");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await _factory.Catalog.DidNotReceiveWithAnyArgs().SearchStudiesAsync(default!, default);
        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    // -----------------------------------------------------------------------
    // Refusals.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A search with no criteria is a request for the archive itself, one page
    /// at a time.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("?page=0")]
    [InlineData("?pageSize=100")]
    [InlineData("?patientName=&accessionNumber=")]
    [InlineData("?patientName=%20%20")]
    public async Task RefusesASearchWithNoCriteria(string query)
    {
        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync(_path + query);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _factory.Catalog.DidNotReceiveWithAnyArgs().SearchStudiesAsync(default!, default);
    }

    [Theory]
    [InlineData("?patientName=DOE&page=-1")]
    [InlineData("?patientName=DOE&pageSize=0")]
    [InlineData("?patientName=DOE&pageSize=101")]
    [InlineData("?patientName=DOE&pageSize=-5")]
    public async Task RefusesAPageOutsideTheBounds(string query)
    {
        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync(_path + query);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _factory.Catalog.DidNotReceiveWithAnyArgs().SearchStudiesAsync(default!, default);
    }

    [Theory]
    [InlineData("?studyDateFrom=03/04/2018")]
    [InlineData("?studyDateFrom=2018-3-4")]
    [InlineData("?studyDateTo=yesterday")]
    [InlineData("?studyDateTo=20181332")]
    public async Task RefusesADateItWouldHaveToGuessAt(string query)
    {
        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync(_path + query);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _factory.Catalog.DidNotReceiveWithAnyArgs().SearchStudiesAsync(default!, default);
    }

    [Fact]
    public async Task RefusesAnInvertedDateRange()
    {
        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync(
            _path + "?studyDateFrom=2018-12-31&studyDateTo=2018-01-01");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _factory.Catalog.DidNotReceiveWithAnyArgs().SearchStudiesAsync(default!, default);
    }

    /// <summary>
    /// A refusal must not record a search that never happened.
    /// </summary>
    [Fact]
    public async Task ARefusedSearchRecordsNothing()
    {
        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync(_path + "?page=-1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _factory.AuditLog.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    // -----------------------------------------------------------------------
    // Forwarding.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ForwardsTheQueryExactlyAsAsked()
    {
        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync(
            _path + "?patientName=DOE&accessionNumber=ACC-1&studyDateFrom=2018-01-01&studyDateTo=20181231&page=2&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.Catalog.Received(1).SearchStudiesAsync(
            Arg.Is<StudySearchQuery>(query =>
                query.PatientName == "DOE"
                && query.AccessionNumber == "ACC-1"
                && query.StudyDateFrom == new DateOnly(2018, 1, 1)
                && query.StudyDateTo == new DateOnly(2018, 12, 31)
                && query.Page == 2
                && query.PageSize == 50),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A caller that does not say how much it wants gets a screenful, not the
    /// largest page of patient data the system can produce.
    /// </summary>
    [Fact]
    public async Task DefaultsToTheFirstPageAndAScreenful()
    {
        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync(_path + "?patientName=DOE");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.Catalog.Received(1).SearchStudiesAsync(
            Arg.Is<StudySearchQuery>(query =>
                query.Page == 0 && query.PageSize == StudySearchLimits.DefaultPageSize),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The archive's query language never crosses this boundary, whatever the
    /// client puts in the URL.
    /// </summary>
    [Fact]
    public async Task IgnoresArchiveParametersAClientInvents()
    {
        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync(
            _path + "?patientName=DOE&limit=5000&offset=900&fuzzymatching=true&includefield=all");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.Catalog.Received(1).SearchStudiesAsync(
            Arg.Is<StudySearchQuery>(query =>
                query.Page == 0 && query.PageSize == StudySearchLimits.DefaultPageSize),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsThePageTheCatalogueProduced()
    {
        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync(_path + "?patientName=DOE");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("medicalRecordNumber").And.Contain("studyInstanceUid").And.Contain("hasMore");
    }

    // -----------------------------------------------------------------------
    // Audit.
    // -----------------------------------------------------------------------

    /// <summary>
    /// One record for one search. Forty results are not forty accesses, and the
    /// terms someone searched for are themselves identifying, so nothing about
    /// the query or its results is written down.
    /// </summary>
    [Fact]
    public async Task RecordsExactlyOneSearchEvent()
    {
        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync(
            _path + "?patientName=DOE&accessionNumber=ACC-1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AuditLog.Received(1).RecordAsync(
            Arg.Is<AuditEntry>(entry =>
                entry.Action == AuditAction.SearchStudies
                && entry.ResourceType == AuditResourceType.Study
                && entry.ResourceId == null
                && entry.ActorUserId == _actor),
            Arg.Any<CancellationToken>());

        await _factory.AuditLog.Received(1).RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NeverRecordsAnAccessPerResult()
    {
        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync(_path + "?patientName=DOE");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AuditLog.DidNotReceive().RecordAsync(
            Arg.Is<AuditEntry>(entry =>
                entry.Action == AuditAction.ViewPatient
                || entry.Action == AuditAction.ViewStudy
                || entry.Action == AuditAction.LoginSucceeded
                || entry.Action == AuditAction.LoginFailed),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An unrecorded search is an unrecorded disclosure, so the request fails
    /// and no result leaves the process.
    /// </summary>
    [Fact]
    public async Task AnAuditFailureWithholdsTheResults()
    {
        _factory.AuditLog
            .RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("audit unavailable"));

        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync(_path + "?patientName=DOE");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        string body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("Fixture Patient");
        body.Should().NotContain("1000192961");
        body.Should().NotContain("1.2.840.113564");
        body.Should().NotContain("audit unavailable");
    }
}
