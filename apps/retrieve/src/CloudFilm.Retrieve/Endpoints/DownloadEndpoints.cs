using CloudFilm.Retrieve.Hospital;

namespace CloudFilm.Retrieve.Endpoints;

/// <summary>
/// Bulk retrieval of one study.
/// </summary>
/// <remarks>
/// <para>
/// The only route in CloudFilm that carries pixel data, and it carries it
/// without holding it: the archive's response is passed through as it arrives.
/// </para>
/// <para>
/// Like every other Retrieve route it is unauthenticated and reachable only on
/// the internal network. Whether this study may be sent to anybody has already
/// been decided by the API in front of it, which is the only component that
/// knows about share links, tokens and expiry. Nothing here can make that
/// decision and nothing here should try.
/// </para>
/// </remarks>
internal static class DownloadEndpoints
{
    /// <summary>
    /// Maps the study download endpoint.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapDownloadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/imaging/studies/{studyInstanceUid}/download", DownloadAsync)
            .WithName("RetrieveDownloadStudy")
            .WithTags("Imaging")
            .WithSummary("Streams one study's DICOM representation.")
            .Produces(StatusCodes.Status200OK, contentType: "multipart/related")
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status501NotImplemented);

        return app;
    }

    /// <summary>
    /// Opens the study and hands the stream to the response.
    /// </summary>
    /// <param name="studyInstanceUid">The study to retrieve.</param>
    /// <param name="registry">The registered hospital adapters.</param>
    /// <param name="context">The request, used to tie the download's lifetime to the response.</param>
    /// <param name="cancellationToken">A token to cancel the retrieval.</param>
    /// <returns>The streamed study, or a problem describing why not.</returns>
    /// <remarks>
    /// <para>
    /// A named static method rather than an inline lambda, so the decision can be
    /// exercised by a test without a web host or an archive.
    /// </para>
    /// <para>
    /// The download is registered for disposal with the response rather than
    /// disposed here: the bytes are still arriving when this method returns, and
    /// closing the stream now would truncate them.
    /// </para>
    /// </remarks>
    internal static async Task<IResult> DownloadAsync(
        string studyInstanceUid,
        IHospitalAdapterRegistry registry,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(context);

        IHospitalAdapter? adapter = registry.Adapters.FirstOrDefault();

        if (adapter is null)
        {
            return Results.NotFound();
        }

        if (adapter is not IStudyDownloadSource source)
        {
            return Results.Problem(
                title: "The configured imaging source does not support study retrieval.",
                statusCode: StatusCodes.Status501NotImplemented);
        }

        StudyDownload? download = await source
            .OpenStudyAsync(studyInstanceUid, cancellationToken)
            .ConfigureAwait(false);

        if (download is null)
        {
            return Results.NotFound();
        }

        context.Response.RegisterForDisposeAsync(download);

        return Results.Stream(download.Content, download.ContentType);
    }
}
