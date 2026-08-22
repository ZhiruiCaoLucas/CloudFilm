namespace CloudFilm.Application.Imaging;

/// <summary>
/// An open study download, still arriving.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the Retrieve Service's contract of the same name across the HTTP
/// boundary between the two processes, exactly as the summaries and records
/// already do.
/// </para>
/// <para>
/// Carries a live stream rather than the bytes, so a study never sits in this
/// process's memory. Owns whatever must stay open for the stream to keep
/// producing; disposing this disposes them, and disposing them earlier truncates
/// the download.
/// </para>
/// </remarks>
public sealed class StudyDownload : IAsyncDisposable
{
    private readonly IDisposable[] _owned;

    /// <summary>
    /// Initialises a new instance of the <see cref="StudyDownload"/> class.
    /// </summary>
    /// <param name="content">The stream the study arrives on.</param>
    /// <param name="contentType">The media type the source answered with.</param>
    /// <param name="owned">Everything that must outlive the read.</param>
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
    /// Passed through rather than assumed: a multipart body's content type
    /// carries the boundary that makes it parseable, and a substituted one makes
    /// it rubbish.
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
