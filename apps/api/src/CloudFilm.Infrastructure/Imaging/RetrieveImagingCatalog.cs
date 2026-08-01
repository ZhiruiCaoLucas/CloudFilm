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
internal sealed class RetrieveImagingCatalog(HttpClient httpClient) : IImagingCatalog
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
