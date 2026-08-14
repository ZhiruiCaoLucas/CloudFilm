using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Imaging;

namespace CloudFilm.Infrastructure.Imaging;

/// <summary>
/// Reads patients and studies through the Retrieve Service.
/// </summary>
/// <remarks>
/// <para>
/// The API never speaks to an imaging source itself. Retrieve is the only
/// component permitted to, so this adapter is deliberately thin: it makes an
/// HTTP call and deserialises the answer. All imaging knowledge lives on the
/// other side of that call.
/// </para>
/// <para>
/// The two services share no assemblies by design, so these contracts are
/// re-declared rather than referenced. The coupling is the wire format, which is
/// what allows Retrieve to be rewritten in another language.
/// </para>
/// </remarks>
internal sealed class RetrieveImagingCatalog(HttpClient httpClient)
    : IImagingCatalog, IStudySearchCatalog
{
    private readonly HttpClient _httpClient = httpClient;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PatientSummary>> ListPatientsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PatientSummary>? patients = await _httpClient
            .GetFromJsonAsync<IReadOnlyList<PatientSummary>>(
                "api/v1/imaging/patients",
                cancellationToken)
            .ConfigureAwait(false);

        return patients ?? [];
    }

    /// <inheritdoc />
    public Task<PatientRecord?> FindPatientAsync(
        string medicalRecordNumber,
        CancellationToken cancellationToken = default) =>
        GetOrNullAsync<PatientRecord>(
            $"api/v1/imaging/patients/{Uri.EscapeDataString(medicalRecordNumber)}",
            cancellationToken);

    /// <inheritdoc />
    public Task<StudyContext?> FindStudyAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default) =>
        GetOrNullAsync<StudyContext>(
            $"api/v1/imaging/studies/{Uri.EscapeDataString(studyInstanceUid)}",
            cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// One HTTP call to Retrieve, which makes one QIDO query. The API never
    /// reaches an archive itself and never assembles a page out of several
    /// requests: the whole page, patients included, comes back from the one call.
    /// </para>
    /// <para>
    /// Paging is expressed as page and size, exactly as the caller stated it.
    /// The translation into whatever the archive understands - an offset, a
    /// limit, one extra row to detect a further page - happens behind Retrieve,
    /// and none of that vocabulary crosses this call.
    /// </para>
    /// <para>
    /// Dates are sent in ISO form because that is what every other date in this
    /// system is written as. Retrieve accepts DICOM's form too, but there is no
    /// reason to introduce a second convention on the wire.
    /// </para>
    /// </remarks>
    public async Task<StudySearchResult> SearchStudiesAsync(
        StudySearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        List<string> parameters = [];

        AddIfPresent(parameters, "patientName", query.PatientName);
        AddIfPresent(parameters, "accessionNumber", query.AccessionNumber);
        AddIfPresent(parameters, "studyDateFrom", IsoDate(query.StudyDateFrom));
        AddIfPresent(parameters, "studyDateTo", IsoDate(query.StudyDateTo));

        parameters.Add("page=" + query.Page.ToString(CultureInfo.InvariantCulture));
        parameters.Add("pageSize=" + query.PageSize.ToString(CultureInfo.InvariantCulture));

        StudySearchResult? result = await _httpClient
            .GetFromJsonAsync<StudySearchResult>(
                "api/v1/imaging/studies/search?" + string.Join('&', parameters),
                cancellationToken)
            .ConfigureAwait(false);

        // A null body would mean Retrieve answered 204 or literal null. Neither
        // is a failure to search; both mean nothing was found.
        return result ?? new StudySearchResult([], HasMore: false, query.Page, query.PageSize);
    }

    private static void AddIfPresent(List<string> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add(name + "=" + Uri.EscapeDataString(value));
        }
    }

    private static string? IsoDate(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Issues a GET, treating 404 as an absent result rather than a failure.
    /// </summary>
    private async Task<T?> GetOrNullAsync<T>(string requestUri, CancellationToken cancellationToken)
        where T : class
    {
        using HttpResponseMessage response = await _httpClient
            .GetAsync(requestUri, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<T>(cancellationToken)
            .ConfigureAwait(false);
    }
}
