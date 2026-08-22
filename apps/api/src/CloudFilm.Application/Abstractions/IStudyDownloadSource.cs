using CloudFilm.Application.Imaging;

namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Retrieves one study's DICOM representation.
/// </summary>
/// <remarks>
/// <para>
/// The only port in CloudFilm that yields pixel data. It takes a Study Instance
/// UID and nothing else - no URL, no series, no instance, no query - so the
/// caller's authority over one study cannot be turned into a request for
/// anything else.
/// </para>
/// <para>
/// Separate from <see cref="IImagingCatalog"/>, which answers questions about
/// studies in JSON. Bulk retrieval has a different shape, a different lifetime
/// and a different failure mode, and the endpoint that serves an anonymous share
/// link has no business holding a port that can also list patients.
/// </para>
/// </remarks>
public interface IStudyDownloadSource
{
    /// <summary>
    /// Opens a study for reading.
    /// </summary>
    /// <param name="studyInstanceUid">The study to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the retrieval.</param>
    /// <returns>
    /// The open download, or <see langword="null"/> when no such study is held.
    /// The caller disposes it.
    /// </returns>
    /// <remarks>
    /// Returns once headers have arrived, with the body still on the wire, so a
    /// caller can decide what to do - and can still refuse - before any of the
    /// study reaches its own client.
    /// </remarks>
    public Task<StudyDownload?> OpenStudyAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default);
}
