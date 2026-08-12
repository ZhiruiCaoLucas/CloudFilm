using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
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

namespace CloudFilm.Api.Tests.Auditing;

/// <summary>
/// A host with the imaging catalog and the audit log substituted.
/// </summary>
/// <remarks>
/// <para>
/// The catalog is faked so the tests never reach the Retrieve Service or
/// Orthanc; the audit log is faked so the assertions can inspect exactly what
/// the endpoints tried to record, including in the failure case where nothing
/// is ever written to a database.
/// </para>
/// <para>
/// Only CloudFilm's own hosted services are removed - the migrator and the
/// seeder. Removing every <see cref="IHostedService"/> would take the web host's
/// own with it and nothing would serve.
/// </para>
/// </remarks>
public sealed class AuditedApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Gets the substituted audit log.</summary>
    public IAuditLogService AuditLog { get; } = Substitute.For<IAuditLogService>();

    /// <summary>Gets the substituted imaging catalog.</summary>
    public IImagingCatalog Catalog { get; } = Substitute.For<IImagingCatalog>();

    /// <summary>
    /// Returns both substitutes to their as-created state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The host is shared across the class so it is started once, and that makes
    /// the substitutes shared too. <c>ClearReceivedCalls()</c> is not enough:
    /// it forgets which calls arrived but keeps every configured return value.
    /// A test that arranges <c>RecordAsync(Arg.Any&lt;AuditEntry&gt;(), ...)</c>
    /// to fail therefore left every later test's audit write failing, and the
    /// symptom was an unrelated endpoint answering 500 - depending on the order
    /// xUnit happened to pick.
    /// </para>
    /// <para>
    /// <c>ClearOptions.All</c> drops return values, call actions and received
    /// calls together, so each test starts from the defaults: catalog lookups
    /// find nothing, and audit writes succeed. xUnit does not run tests within a
    /// class in parallel, so resetting shared state at the start of each one is
    /// safe.
    /// </para>
    /// <para>
    /// The successful audit write is then re-armed explicitly rather than left
    /// to NSubstitute's automatic value for <see cref="Task"/>. The whole defect
    /// this method exists to fix was a wrong assumption about what a substitute
    /// does when nobody has told it, so the state every test depends on is
    /// stated rather than inferred.
    /// </para>
    /// </remarks>
    public void ResetSubstitutes()
    {
        AuditLog.ClearSubstitute(ClearOptions.All);
        Catalog.ClearSubstitute(ClearOptions.All);

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

            services.RemoveAll<IAuditLogService>();
            services.AddScoped(_ => AuditLog);

            services.RemoveAll<IImagingCatalog>();
            services.AddScoped(_ => Catalog);
        });
    }
}

/// <summary>
/// What the PHI endpoints record, and what they do when recording fails.
/// </summary>
public sealed class AuditedEndpointsTests : IClassFixture<AuditedApiFactory>
{
    private const string _mrn = "1000192961";
    private const string _studyUid = "1.2.840.113564.145320946818674.1292.636566240007310666.706";

    /// <summary>
    /// A name no production code path could produce, so an assertion that it is
    /// absent is unambiguous evidence that the patient record was not returned.
    /// </summary>
    private const string _fixturePatientName = "Fixture Patient";

    private static readonly Guid _actorId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly AuditedApiFactory _factory;

    public AuditedEndpointsTests(AuditedApiFactory factory)
    {
        _factory = factory;

        // Every test begins from the same known state. See ResetSubstitutes.
        _factory.ResetSubstitutes();
    }

    /// <summary>Builds a client carrying a token issued by the host's own issuer.</summary>
    private HttpClient CreateSignedInClient()
    {
        HttpClient client = _factory.CreateClient();

        using IServiceScope scope = _factory.Services.CreateScope();
        IAccessTokenIssuer issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        AccessToken token = issuer.Issue(_actorId.ToString(), "clinician@example.test", "Clinician");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        return client;
    }

    private static PatientRecord APatient() =>
        new(new PatientSummary(_mrn, _fixturePatientName, "Unknown", "1971-07-24"), []);

    private static StudyContext AStudy()
    {
        StudySummary study = new(_studyUid, "CR", "Untitled study", "201803140707", "2018-03-14", "Completed", 1, 2);

        return new StudyContext(
            new PatientSummary(_mrn, _fixturePatientName, "Unknown", "1971-07-24"),
            [study],
            study,
            []);
    }

    [Fact]
    public async Task ViewingAPatientRecordsAnAuditEventCarryingTheMedicalRecordNumber()
    {
        _factory.Catalog.FindPatientAsync(_mrn, Arg.Any<CancellationToken>()).Returns(APatient());

        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync($"/api/v1/patients/{_mrn}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AuditLog.Received(1).RecordAsync(
            Arg.Is<AuditEntry>(entry =>
                entry.Action == AuditAction.ViewPatient
                && entry.ResourceType == AuditResourceType.Patient
                && entry.ResourceId == _mrn
                && entry.ActorUserId == _actorId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ViewingAStudyRecordsAnAuditEventCarryingTheStudyInstanceUid()
    {
        _factory.Catalog.FindStudyAsync(_studyUid, Arg.Any<CancellationToken>()).Returns(AStudy());

        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync($"/api/v1/studies/{_studyUid}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.AuditLog.Received(1).RecordAsync(
            Arg.Is<AuditEntry>(entry =>
                entry.Action == AuditAction.ViewStudy
                && entry.ResourceType == AuditResourceType.Study
                && entry.ResourceId == _studyUid
                && entry.ActorUserId == _actorId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A lookup that matched nothing is not an access, and must not be recorded
    /// as one.
    /// </summary>
    [Fact]
    public async Task AMissingPatientRecordsNothing()
    {
        _factory.Catalog.FindPatientAsync("no-such-mrn", Arg.Any<CancellationToken>())
            .Returns((PatientRecord?)null);

        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/patients/no-such-mrn");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _factory.AuditLog.DidNotReceive()
            .RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The central guarantee: patient data is never disclosed unaudited.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The catalog succeeds and the audit write fails. The response must not be
    /// a 200 - an access that could not be recorded must not happen at all - and
    /// the patient record must not appear in the error body.
    /// </para>
    /// <para>
    /// The Medical Record Number itself is expected in the response, in the
    /// problem document's <c>instance</c> member, because that member is the
    /// request path and the caller is the one who sent it. Echoing back a URL
    /// somebody just requested discloses nothing. What must not appear is the
    /// patient's data, which is what this now asserts.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AFailedAuditWritePreventsThePatientResponse()
    {
        _factory.Catalog.FindPatientAsync(_mrn, Arg.Any<CancellationToken>()).Returns(APatient());
        _factory.AuditLog
            .RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("audit store unavailable")));

        using HttpClient client = CreateSignedInClient();

        using HttpResponseMessage response = await client.GetAsync($"/api/v1/patients/{_mrn}");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        string body = await response.Content.ReadAsStringAsync();

        // (b) and (c): the response is the standard problem document, carrying a
        // correlation id an operator can trace, and nothing else.
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement problem = document.RootElement;

        problem.GetProperty("status").GetInt32().Should().Be(500);
        problem.GetProperty("title").GetString().Should().Be("An unexpected error occurred.");
        problem.GetProperty("instance").GetString().Should().Be($"/api/v1/patients/{_mrn}");

        problem.TryGetProperty("correlationId", out JsonElement correlationId)
            .Should().BeTrue("an operator must be able to trace the failure");
        correlationId.GetString().Should().NotBeNullOrWhiteSpace();

        // (d) the guarantee itself: none of the patient record reached the client.
        body.Should().NotContain(
            _fixturePatientName,
            "an access that could not be audited must not disclose the patient");
        body.Should().NotContain(
            "medicalRecordNumber",
            "no field of the patient record may appear in the error body");
        body.Should().NotContain("1971-07-24", "the date of birth must not be disclosed either");
    }

    [Fact]
    public async Task AnonymousRequestsAreRefusedBeforeAnythingIsAudited()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync($"/api/v1/patients/{_mrn}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await _factory.AuditLog.DidNotReceive()
            .RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }
}
