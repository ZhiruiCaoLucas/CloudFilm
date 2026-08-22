using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Analysis;
using CloudFilm.Application.Imaging;

namespace CloudFilm.Infrastructure.Analysis;

/// <summary>
/// Sends an authorised study to the analysis service and returns what it says.
/// </summary>
/// <remarks>
/// <para>
/// The whole of this adapter's job is to move bytes the caller already holds. It
/// takes no Study Instance UID it could resolve, builds no URL from one, and
/// makes exactly one outbound call - to the analysis service, with the study in
/// the request body. The analyser is therefore unable to reach imaging nobody
/// authorised, because it is never told where any imaging lives.
/// </para>
/// <para>
/// Nothing here reads a DICOM tag. The bytes pass through as bytes; the study
/// identifier that travels with them is the one the caller was authorised for,
/// not one recovered from the file. Infrastructure has no business knowing what a
/// patient is called, and a layer that parsed the dataset to find out would be a
/// second place PHI lives.
/// </para>
/// </remarks>
internal sealed class StudyAnalyser(HttpClient httpClient) : IStudyAnalyser
{
    /// <summary>The route on the analysis service.</summary>
    private const string _analysePath = "analyze";

    /// <summary>
    /// How the service's JSON is read.
    /// </summary>
    /// <remarks>
    /// Web defaults, so camelCase names bind to the contract's properties without
    /// a converter or an attribute on either side of the boundary.
    /// </remarks>
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient = httpClient;

    /// <inheritdoc />
    public async Task<StudyAnalysis?> AnalyseAsync(
        string studyInstanceUid,
        StudyDownload study,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(study);

        string trimmed = studyInstanceUid?.Trim() ?? string.Empty;

        if (trimmed.Length == 0 || trimmed.Length > AnalysisLimits.MaxStudyInstanceUidLength)
        {
            throw new ArgumentException(
                "A study identifier is required and must be within the length the contract allows.",
                nameof(studyInstanceUid));
        }

        // New for every call, and drawn from nothing. Not the study, not the
        // patient, not a share link or a token: a correlation value that carried
        // any of those would put them in the analyser's logs, which is the one
        // place this design keeps them out of.
        string requestId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        using DicomFormDataContent content = new(study, trimmed, requestId, NewBoundary());

        using HttpResponseMessage response = await _httpClient
            .PostAsync(_analysePath, content, cancellationToken)
            .ConfigureAwait(false);

        if (content.Instances == 0)
        {
            // The archive answered, and the answer held no instances. Nothing was
            // analysed because there was nothing to analyse - which is an answer,
            // not a fault.
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new AnalysisFailedException(response.StatusCode);
        }

        AnalysisResponse? reported = await response.Content
            .ReadFromJsonAsync<AnalysisResponse>(_json, cancellationToken)
            .ConfigureAwait(false);

        return Validated(reported, trimmed);
    }

    /// <summary>
    /// Turns what the service said into the contract, or refuses it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The study identifier is taken from what CloudFilm authorised, never from
    /// the response - and a response naming a different study is refused outright
    /// rather than relabelled. A finding filed against the wrong study is a
    /// finding filed against the wrong patient, and quietly correcting the label
    /// would hide the fact that something upstream is confused.
    /// </para>
    /// <para>
    /// The bounds the contract declares are checked here rather than trusted.
    /// This is the only place the analyser's output crosses into CloudFilm, so it
    /// is the only place that can hold the line - a score outside [0, 1] would
    /// reach a clinician's screen as a percentage nobody can interpret.
    /// </para>
    /// </remarks>
    private static StudyAnalysis Validated(AnalysisResponse? reported, string studyInstanceUid)
    {
        if (reported is null
            || string.IsNullOrWhiteSpace(reported.ModelName)
            || string.IsNullOrWhiteSpace(reported.ModelVersion))
        {
            throw new AnalysisFailedException("The analysis service returned no usable result.");
        }

        if (!string.IsNullOrWhiteSpace(reported.StudyInstanceUid)
            && !string.Equals(reported.StudyInstanceUid, studyInstanceUid, StringComparison.Ordinal))
        {
            throw new AnalysisFailedException("The analysis service answered about a different study.");
        }

        if (reported.ModelName.Length > AnalysisLimits.MaxModelNameLength
            || reported.ModelVersion.Length > AnalysisLimits.MaxModelVersionLength)
        {
            throw new AnalysisFailedException("The analysis service named a model this contract cannot carry.");
        }

        IReadOnlyList<AnalysisFinding> findings = Findings(reported.Findings);

        return new StudyAnalysis(
            studyInstanceUid,
            reported.ModelName,
            reported.ModelVersion,
            reported.CompletedAtUtc.ToUniversalTime(),
            findings);
    }

    /// <summary>Checks each finding against the bounds the contract declares.</summary>
    private static List<AnalysisFinding> Findings(IReadOnlyList<ReportedFinding>? reported)
    {
        List<AnalysisFinding> findings = [];

        if (reported is null)
        {
            return findings;
        }

        if (reported.Count > AnalysisLimits.MaxFindings)
        {
            throw new AnalysisFailedException("The analysis service reported more findings than the contract allows.");
        }

        foreach (ReportedFinding finding in reported)
        {
            if (string.IsNullOrWhiteSpace(finding.Label) || finding.Label.Length > AnalysisLimits.MaxLabelLength)
            {
                throw new AnalysisFailedException("The analysis service reported a finding without a usable label.");
            }

            if (double.IsNaN(finding.Score)
                || finding.Score < AnalysisLimits.MinimumScore
                || finding.Score > AnalysisLimits.MaximumScore)
            {
                throw new AnalysisFailedException("The analysis service reported a score outside the range a finding may carry.");
            }

            findings.Add(new AnalysisFinding(finding.Label, finding.Score));
        }

        return findings;
    }

    /// <summary>
    /// A boundary no part of a DICOM study will contain.
    /// </summary>
    /// <remarks>
    /// Random, because a boundary that appeared inside an instance would split it
    /// - and DICOM is binary, so any fixed string could. 32 hexadecimal characters
    /// from <see cref="Guid.NewGuid"/> is far past the point where that is a
    /// practical concern, and the prefix keeps the value recognisable in a packet
    /// capture without saying anything about the study.
    /// </remarks>
    private static string NewBoundary() =>
        string.Create(CultureInfo.InvariantCulture, $"cloudfilm-{Guid.NewGuid():N}");
}

/// <summary>
/// The analysis service failed, or answered something unusable.
/// </summary>
/// <remarks>
/// Distinct from a null result. Null means the study was seen and not analysed;
/// this means the attempt did not produce an answer anybody should act on, and
/// the caller must be able to tell those apart - one is worth retrying and one is
/// not.
///
/// The message never quotes the service's response body. A body that failed
/// validation is a body nothing has checked, and it arrived from a process
/// holding a patient's imaging.
/// </remarks>
public sealed class AnalysisFailedException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="AnalysisFailedException"/> class.</summary>
    public AnalysisFailedException()
        : base("The analysis service did not produce a result.")
    {
    }

    /// <summary>Initialises a new instance of the <see cref="AnalysisFailedException"/> class.</summary>
    /// <param name="message">What went wrong, in terms that carry no patient data.</param>
    public AnalysisFailedException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="AnalysisFailedException"/> class.</summary>
    /// <param name="message">What went wrong, in terms that carry no patient data.</param>
    /// <param name="innerException">The failure underneath.</param>
    public AnalysisFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="AnalysisFailedException"/> class.</summary>
    /// <param name="statusCode">What the service answered.</param>
    /// <remarks>
    /// The status is recorded because it is the one part of a failed response that
    /// is safe to keep: it says whether the service refused the request, broke, or
    /// was not there, and it says nothing about the study.
    /// </remarks>
    public AnalysisFailedException(HttpStatusCode statusCode)
        : base(string.Create(CultureInfo.InvariantCulture, $"The analysis service answered {(int)statusCode}."))
        => StatusCode = statusCode;

    /// <summary>Gets what the service answered, when it answered at all.</summary>
    public HttpStatusCode? StatusCode { get; }
}

/// <summary>What the analysis service sends back, before anything has checked it.</summary>
/// <remarks>
/// Separate from the Application contract on purpose. The contract is what
/// CloudFilm guarantees; this is what arrived, which is a different thing until it
/// has been validated. Binding the service's JSON straight onto the contract would
/// mean an unchecked score or an unbounded label became one by construction.
/// </remarks>
internal sealed record AnalysisResponse(
    string? StudyInstanceUid,
    string? ModelName,
    string? ModelVersion,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<ReportedFinding>? Findings);

/// <summary>One finding as the service reported it.</summary>
internal sealed record ReportedFinding(string? Label, double Score);
