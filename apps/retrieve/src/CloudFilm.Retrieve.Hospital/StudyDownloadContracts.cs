namespace CloudFilm.Retrieve.Hospital;

/// <summary>
/// An open study download, still arriving.
/// </summary>
/// <remarks>
/// <para>
/// Carries a live network stream rather than the bytes. A study is routinely
/// hundreds of megabytes and occasionally gigabytes; materialising one would
/// put a hospital's imaging into this process's memory for as long as the slowest
/// client took to read it, and two concurrent downloads would do it twice.
/// </para>
/// <para>
/// Owns whatever has to stay alive for the stream to keep producing bytes - the
/// HTTP response, and the client it was made on. Disposing this disposes them in
/// the right order; disposing them earlier truncates the download.
/// </para>
/// </remarks>
public sealed class StudyDownload : IAsyncDisposable
{
    private readonly IDisposable[] _owned;

    /// <summary>
    /// Initialises a new instance of the <see cref="StudyDownload"/> class.
    /// </summary>
    /// <param name="content">The stream the study arrives on.</param>
    /// <param name="contentType">The media type the archive answered with.</param>
    /// <param name="owned">
    /// Everything that must outlive the read, innermost first.
    /// </param>
    public StudyDownload(Stream content, string contentType, params IDisposable[] owned)
    {
        Content = content;
        ContentType = contentType;
        _owned = owned ?? [];
    }

    /// <summary>Gets the stream the study arrives on.</summary>
    public Stream Content { get; }

    /// <summary>
    /// Gets the media type to pass on unchanged.
    /// </summary>
    /// <remarks>
    /// Taken from the archive rather than assumed. A multipart response carries a
    /// generated boundary in its content type, and a caller that substituted its
    /// own would hand the recipient a body it could not parse.
    /// </remarks>
    public string ContentType { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync().ConfigureAwait(false);

        foreach (IDisposable resource in _owned)
        {
            resource.Dispose();
        }
    }
}

/// <summary>
/// A hospital imaging source whose studies can be retrieved as DICOM.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="IHospitalAdapter"/> for the reason
/// <see cref="IStudySearchSource"/> is: not every archive exposes bulk
/// retrieval, and adding a method to the base adapter would force every
/// implementation, present and future, to answer a question it may not be able
/// to.
/// </para>
/// <para>
/// One study at a time, named by its Study Instance UID. There is deliberately
/// no method that takes a URL, a series, an instance or a query: this port
/// exists to serve a share link, and a share link names exactly one study.
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
    /// The open download, or <see langword="null"/> when the archive holds no
    /// such study. The caller disposes it.
    /// </returns>
    /// <remarks>
    /// Returns as soon as the archive has answered with headers, before the body
    /// has arrived. The caller is expected to pass the stream on rather than read
    /// it.
    /// </remarks>
    public Task<StudyDownload?> OpenStudyAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default);
}
