using System.Diagnostics;
using System.Text.Json;
using CloudFilm.Retrieve.Hospital;

namespace CloudFilm.Retrieve.Orthanc;

/// <summary>
/// Reads patients and studies from Orthanc over DICOMweb.
/// </summary>
/// <remarks>
/// <para>
/// The first real <see cref="IHospitalAdapter"/>. Orthanc is treated exactly as
/// any other imaging source would be: queried through QIDO-RS, never through its
/// proprietary REST API, so a hospital archive speaking the same standard can
/// replace it without touching anything above this class.
/// </para>
/// <para>
/// The HTTP client is created per call from the factory rather than injected,
/// because adapters are registered as singletons and a captured
/// <see cref="HttpClient"/> would never observe DNS changes.
/// </para>
/// </remarks>
internal sealed class OrthancDicomWebAdapter(IHttpClientFactory httpClientFactory) : IHospitalAdapter
{
    /// <summary>The named client carrying the Orthanc base address.</summary>
    public const string HttpClientName = "orthanc-dicomweb";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    /// <inheritdoc />
    public string HospitalCode => "LOCAL";

    /// <inheritdoc />
    public string DisplayName => "Local Orthanc";

    /// <inheritdoc />
    public async Task<HospitalConnectivity> ProbeAsync(CancellationToken cancellationToken = default)
    {
        long started = Stopwatch.GetTimestamp();

        try
        {
            using HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
            using HttpResponseMessage response = await client
                .GetAsync("studies?limit=1", cancellationToken)
                .ConfigureAwait(false);

            TimeSpan elapsed = Stopwatch.GetElapsedTime(started);

            return response.IsSuccessStatusCode
                ? HospitalConnectivity.Reachable(elapsed)
                : HospitalConnectivity.Unreachable(
                    $"DICOMweb answered {(int)response.StatusCode}.",
                    elapsed);
        }
        catch (HttpRequestException exception)
        {
            return HospitalConnectivity.Unreachable(
                exception.Message,
                Stopwatch.GetElapsedTime(started));
        }
        catch (TaskCanceledException exception)
        {
            return HospitalConnectivity.Unreachable(
                $"DICOMweb did not answer in time: {exception.Message}",
                Stopwatch.GetElapsedTime(started));
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PatientSummary>> ListPatientsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<JsonElement> studies = await QueryStudiesAsync("studies", cancellationToken)
            .ConfigureAwait(false);

        // QIDO-RS has no patient level query, so patients are the distinct
        // subjects of the studies the archive holds.
        return studies
            .Select(ToPatient)
            .Where(patient => patient.MedicalRecordNumber.Length > 0)
            .DistinctBy(patient => patient.MedicalRecordNumber, StringComparer.OrdinalIgnoreCase)
            .OrderBy(patient => patient.MedicalRecordNumber, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PatientRecord?> FindPatientAsync(
        string medicalRecordNumber,
        CancellationToken cancellationToken = default)
    {
        string trimmed = medicalRecordNumber.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        IReadOnlyList<JsonElement> studies = await QueryStudiesAsync(
                $"studies?PatientID={Uri.EscapeDataString(trimmed)}",
                cancellationToken)
            .ConfigureAwait(false);

        return studies.Count == 0
            ? null
            : new PatientRecord(ToPatient(studies[0]), ToStudies(studies));
    }

    /// <inheritdoc />
    public async Task<StudyContext?> FindStudyAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        string trimmed = studyInstanceUid.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        IReadOnlyList<JsonElement> matches = await QueryStudiesAsync(
                $"studies?StudyInstanceUID={Uri.EscapeDataString(trimmed)}",
                cancellationToken)
            .ConfigureAwait(false);

        if (matches.Count == 0)
        {
            return null;
        }

        PatientSummary patient = ToPatient(matches[0]);
        StudySummary selected = ToStudy(matches[0]);

        // The workspace navigator lists the patient's other studies, so resolve
        // them here rather than making the caller issue a second request that
        // could disagree with this one.
        PatientRecord? record = patient.MedicalRecordNumber.Length == 0
            ? null
            : await FindPatientAsync(patient.MedicalRecordNumber, cancellationToken)
                .ConfigureAwait(false);

        IReadOnlyList<StudySummary> siblings = record?.Studies ?? [selected];

        return new StudyContext(patient, siblings, selected);
    }

    private async Task<IReadOnlyList<JsonElement>> QueryStudiesAsync(
        string requestUri,
        CancellationToken cancellationToken)
    {
        using HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
        using HttpResponseMessage response = await client
            .GetAsync(requestUri, cancellationToken)
            .ConfigureAwait(false);

        // QIDO-RS answers 204 when a query matches nothing. That is a result,
        // not a failure, and the body is empty.
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();

        await using Stream body = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using JsonDocument document = await JsonDocument
            .ParseAsync(body, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        // Cloned because the elements outlive the JsonDocument they came from.
        return document.RootElement.EnumerateArray()
            .Select(element => element.Clone())
            .ToList();
    }

    private static PatientSummary ToPatient(JsonElement study) => new(
        MedicalRecordNumber: DicomJson.Text(study, DicomJson.PatientId),
        FullName: FallbackToMrn(
            DicomJson.PersonName(study, DicomJson.PatientName),
            DicomJson.Text(study, DicomJson.PatientId)),
        Gender: DicomJson.Gender(DicomJson.Text(study, DicomJson.PatientSex)),
        BirthDate: DicomJson.IsoDate(DicomJson.Text(study, DicomJson.PatientBirthDate)));

    private static IReadOnlyList<StudySummary> ToStudies(IEnumerable<JsonElement> studies) =>
        studies
            .Select(ToStudy)
            .Where(study => study.StudyInstanceUid.Length > 0)
            .OrderByDescending(study => study.PerformedOn, StringComparer.Ordinal)
            .ToList();

    private static StudySummary ToStudy(JsonElement study)
    {
        string description = DicomJson.Text(study, DicomJson.StudyDescription);
        string modality = DicomJson.Text(study, DicomJson.ModalitiesInStudy);

        return new StudySummary(
            StudyInstanceUid: DicomJson.Text(study, DicomJson.StudyInstanceUid),
            Modality: modality.Length == 0 ? "OT" : modality,
            Description: description.Length == 0 ? "Untitled study" : description,
            PerformedOn: DicomJson.IsoDate(DicomJson.Text(study, DicomJson.StudyDate)),

            // The archive holds it, so it is acquired. A worklist status would
            // come from a modality worklist query, which is a later concern.
            Status: "Completed",
            SeriesCount: DicomJson.Integer(study, DicomJson.SeriesCount),
            InstanceCount: DicomJson.Integer(study, DicomJson.InstanceCount));
    }

    private static string FallbackToMrn(string name, string medicalRecordNumber) =>
        name.Length > 0 ? name : medicalRecordNumber;
}
