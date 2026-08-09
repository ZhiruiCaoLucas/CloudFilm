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
internal sealed class OrthancDicomWebAdapter(
    IHttpClientFactory httpClientFactory,
    ILogger<OrthancDicomWebAdapter> logger) : IHospitalAdapter
{
    /// <summary>The named client carrying the Orthanc base address.</summary>
    public const string HttpClientName = "orthanc-dicomweb";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<OrthancDicomWebAdapter> _logger = logger;

    // Pre-compiled logging delegates. LoggerMessage.Define builds the message
    // template once, at type initialisation, instead of parsing and boxing on
    // every call - which is what CA1848 asks for. Templates and arguments are
    // identical to the direct calls they replace.
    private static readonly Action<ILogger, string, Uri?, int, int, Exception?> _logQidoResponse =
        LoggerMessage.Define<string, Uri?, int, int>(
            LogLevel.Information,
            new EventId(1001, "QidoResponse"),
            "QIDO {RequestUri} (absolute {AbsoluteUri}) -> {StatusCode}, response length={Length} bytes");

    private static readonly Action<ILogger, string, JsonValueKind, Exception?> _logQidoNotAnArray =
        LoggerMessage.Define<string, JsonValueKind>(
            LogLevel.Information,
            new EventId(1002, "QidoNotAnArray"),
            "QIDO response for {RequestUri} is {ValueKind}, not an array.");

    private static readonly Action<ILogger, string, int, Exception?> _logQidoCount =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(1003, "QidoCount"),
            "QIDO {RequestUri} returned {Count} result(s).");

    private static readonly Action<ILogger, string, string, string, Exception?> _logFirstStudy =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(1004, "QidoFirstStudy"),
            "First study: PatientID={PatientId}, PatientName={PatientName}, StudyUID={StudyUid}");

    private static readonly Action<ILogger, string, int, Exception?> _logFindPatientMatches =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(1005, "FindPatientMatches"),
            "FindPatient('{MedicalRecordNumber}') matched {Count} study/studies.");

    private static readonly Action<ILogger, int, Exception?> _logSeriesCount =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1006, "SeriesCount"),
            "Series query returned {Count} series for the requested study.");

    private static readonly Action<ILogger, Exception?> _logSeriesQueryFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1007, "SeriesQueryFailed"),
            "Series query failed; the study is returned without series detail.");

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

        _logFindPatientMatches(_logger, trimmed, studies.Count, null);

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

        // Resolved for this study alone. See QuerySeriesAsync.
        IReadOnlyList<SeriesSummary> series = await QuerySeriesAsync(trimmed, cancellationToken)
            .ConfigureAwait(false);

        // The workspace navigator lists the patient's other studies, so resolve
        // them here rather than making the caller issue a second request that
        // could disagree with this one.
        PatientRecord? record = patient.MedicalRecordNumber.Length == 0
            ? null
            : await FindPatientAsync(patient.MedicalRecordNumber, cancellationToken)
                .ConfigureAwait(false);

        IReadOnlyList<StudySummary> siblings = record?.Studies ?? [selected];

        return new StudyContext(patient, siblings, selected, series);
    }

    private async Task<IReadOnlyList<JsonElement>> QueryStudiesAsync(
        string requestUri,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<JsonElement> results = await ReadDicomJsonArrayAsync(requestUri, cancellationToken)
            .ConfigureAwait(false);

        if (results.Count > 0)
        {
            _logFirstStudy(
                _logger,
                DicomJson.Text(results[0], DicomJson.PatientId),
                Redact(DicomJson.PersonName(results[0], DicomJson.PatientName)),
                DicomJson.Text(results[0], DicomJson.StudyInstanceUid),
                null);
        }

        return results;
    }

    /// <summary>
    /// Lists the series of one study.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One QIDO-RS request against the study's own series resource. Instance
    /// counts come from Number of Series Related Instances, which the archive
    /// publishes here - CloudFilm never enumerates instances to count them.
    /// </para>
    /// <para>
    /// Issued only for the study actually being opened. Resolving series for a
    /// patient's whole study list would be one request per sibling study, for
    /// data no view renders.
    /// </para>
    /// <para>
    /// A failure is degraded, not propagated: the workspace can display a study
    /// without series detail, and the viewer reads its own pixels over its own
    /// DICOMweb connection either way. Losing the series list must not cost the
    /// clinician the image. The warning is logged so the degradation is visible.
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyList<SeriesSummary>> QuerySeriesAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<JsonElement> results;

        try
        {
            results = await ReadDicomJsonArrayAsync(
                    $"studies/{Uri.EscapeDataString(studyInstanceUid)}/series",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            _logSeriesQueryFailed(_logger, null);

            return [];
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The client's own 30 second timeout, not the caller giving up. A
            // real cancellation is deliberately left to propagate: the caller
            // has stopped caring about the answer, and swallowing it here would
            // make the request look like it had succeeded with no series.
            _logSeriesQueryFailed(_logger, null);

            return [];
        }

        List<SeriesSummary> series = results
            .Select(ToSeries)
            .Where(item => item.SeriesInstanceUid.Length > 0)
            .OrderBy(item => item.SeriesNumber)
            .ToList();

        _logSeriesCount(_logger, series.Count, null);

        return series;
    }

    private async Task<IReadOnlyList<JsonElement>> ReadDicomJsonArrayAsync(
        string requestUri,
        CancellationToken cancellationToken)
    {
        using HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
        using HttpResponseMessage response = await client
            .GetAsync(requestUri, cancellationToken)
            .ConfigureAwait(false);

        // Diagnostic only. The body is buffered into a string rather than
        // streamed so it can be logged before it is parsed; parsing a string and
        // parsing a stream produce the same document, so behaviour is unchanged.
        string bodyText = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        _logQidoResponse(
            _logger,
            requestUri,
            response.RequestMessage?.RequestUri,
            (int)response.StatusCode,
            bodyText.Length,
            null);

        // QIDO-RS answers 204 when a query matches nothing. That is a result,
        // not a failure, and the body is empty.
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();

        using JsonDocument document = JsonDocument.Parse(bodyText);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            _logQidoNotAnArray(_logger, requestUri, document.RootElement.ValueKind, null);

            return [];
        }

        int count = document.RootElement.GetArrayLength();

        _logQidoCount(_logger, requestUri, count, null);

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

    private static List<StudySummary> ToStudies(IEnumerable<JsonElement> studies) =>
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

            // Read from the study query already issued; no second request. Left
            // exactly as the archive reports it, empty included - an accession
            // number is an identifier another system assigned, so substituting
            // a placeholder would be inventing one.
            AccessionNumber: DicomJson.Text(study, DicomJson.AccessionNumber),

            PerformedOn: DicomJson.IsoDate(DicomJson.Text(study, DicomJson.StudyDate)),

            // The archive holds it, so it is acquired. A worklist status would
            // come from a modality worklist query, which is a later concern.
            Status: "Completed",
            SeriesCount: DicomJson.Integer(study, DicomJson.SeriesCount),
            InstanceCount: DicomJson.Integer(study, DicomJson.InstanceCount));
    }

    /// <summary>
    /// Projects one QIDO-RS series result.
    /// </summary>
    /// <remarks>
    /// Every reader is defensive by construction: <see cref="DicomJson.Text"/>
    /// yields an empty string and <see cref="DicomJson.Integer"/> yields zero
    /// for an attribute that is absent, empty or malformed. A series missing a
    /// number, a description or an institution is therefore projected rather
    /// than discarded, and cannot fail the study response around it.
    /// </remarks>
    private static SeriesSummary ToSeries(JsonElement series) => new(
        SeriesInstanceUid: DicomJson.Text(series, DicomJson.SeriesInstanceUid),
        SeriesNumber: DicomJson.Integer(series, DicomJson.SeriesNumber),
        Modality: DicomJson.Text(series, DicomJson.Modality),
        Description: DicomJson.Text(series, DicomJson.SeriesDescription),
        InstanceCount: DicomJson.Integer(series, DicomJson.SeriesInstanceCount),

        // Left exactly as the archive reports it. Absent stays absent: an
        // institution name is a fact about where imaging happened, and guessing
        // it from the deployment would put a false provenance on a clinical
        // record.
        InstitutionName: DicomJson.Text(series, DicomJson.InstitutionName));

    private static string FallbackToMrn(string name, string medicalRecordNumber) =>
        name.Length > 0 ? name : medicalRecordNumber;

    /// <summary>
    /// Describes a patient name without reproducing it.
    /// </summary>
    /// <remarks>
    /// CONTRIBUTING.md forbids logging patient data. Diagnosing a query that
    /// returns nothing needs to know whether the name is populated, not what it
    /// says, so the shape is logged and the value is not.
    /// </remarks>
    private static string Redact(string value) =>
        value.Length == 0 ? "(absent)" : $"(present, {value.Length} chars)";
}
