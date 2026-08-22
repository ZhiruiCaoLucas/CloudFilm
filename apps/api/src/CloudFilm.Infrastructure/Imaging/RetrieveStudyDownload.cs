using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Imaging;

namespace CloudFilm.Infrastructure.Imaging;

/// <summary>
/// Retrieves studies through the Retrieve Service.
/// </summary>
/// <remarks>
/// <para>
/// The API never speaks to an imaging source itself, and bulk retrieval is no
/// exception: this makes one HTTP call to Retrieve and passes the response
/// through. All imaging knowledge stays on the other side of that call, and
/// Orthanc's address and credential stay in the one component allowed to hold
/// them.
/// </para>
/// <para>
/// Nothing is buffered. The response is read headers-first and the body is
/// handed on as a stream, so a study crosses this process without ever being in
/// it.
/// </para>
/// </remarks>
internal sealed class RetrieveStudyDownload(HttpClient httpClient) : IStudyDownloadSource
{
    private readonly HttpClient _httpClient = httpClient;

    /// <inheritdoc />
    public async Task<StudyDownload?> OpenStudyAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        string trimmed = studyInstanceUid?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return null;
        }

        HttpResponseMessage? response = null;

        try
        {
            response = await _httpClient
                .GetAsync(
                    $"api/v1/imaging/studies/{Uri.EscapeDataString(trimmed)}/download",
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // A study the archive does not hold is an ordinary answer. The
                // caller turns it into the same response an unknown token gets.
                return null;
            }

            Stream content = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            string contentType = response.Content.Headers.ContentType?.ToString()
                ?? "multipart/related";

            // The typed client belongs to the factory and is not disposed here;
            // the response is, once the caller has finished reading.
            StudyDownload download = new(content, contentType, response);

            response = null;

            return download;
        }
        finally
        {
            response?.Dispose();
        }
    }
}
