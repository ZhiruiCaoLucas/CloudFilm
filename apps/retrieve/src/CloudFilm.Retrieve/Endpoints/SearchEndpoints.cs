using CloudFilm.Retrieve.Hospital;

namespace CloudFilm.Retrieve.Endpoints;

/// <summary>
/// Study search, answered by the registered hospital adapter.
/// </summary>
/// <remarks>
/// <para>
/// A separate file from <see cref="ImagingEndpoints"/> rather than another route
/// in it: searching is a different capability, not every adapter has it, and the
/// endpoint has to establish that before it can answer.
/// </para>
/// <para>
/// The route is <c>/api/v1/imaging/studies/search</c>, which sits under the same
/// prefix as <c>/api/v1/imaging/studies/{studyInstanceUid}</c>. Routing prefers
/// the literal segment over the parameter, so the two cannot be confused - and a
/// Study Instance UID is digits and dots, so no real study could ever be named
/// <c>search</c>.
/// </para>
/// </remarks>
internal static class SearchEndpoints
{
    /// <summary>
    /// Maps the study search endpoint.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/imaging/studies/search", SearchAsync)
            .WithName("RetrieveSearchStudies")
            .WithTags("Imaging")
            .WithSummary("Finds studies matching a query.")
            .Produces<StudySearchResult>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status501NotImplemented);

        return app;
    }

    /// <summary>
    /// Validates a search request and delegates it to the adapter.
    /// </summary>
    /// <param name="patientName">Part of the patient's name, or nothing.</param>
    /// <param name="accessionNumber">The accession number or its leading characters, or nothing.</param>
    /// <param name="studyDateFrom">Earliest study date as <c>YYYY-MM-DD</c> or <c>YYYYMMDD</c>, or nothing.</param>
    /// <param name="studyDateTo">Latest study date in the same forms, or nothing.</param>
    /// <param name="page">The zero-based page wanted; defaults to the first.</param>
    /// <param name="pageSize">How many results one page holds; defaults to a screenful.</param>
    /// <param name="registry">The registered hospital adapters.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>One page of results, or a problem describing why the query was refused.</returns>
    /// <remarks>
    /// <para>
    /// A named static method rather than an inline lambda so the whole decision
    /// - parse, validate, refuse or delegate - can be exercised directly by a
    /// test, without a web host and without an archive.
    /// </para>
    /// <para>
    /// Validation happens here, before the adapter is reached. The API validates
    /// too, but this service has its own HTTP surface and a rule only the caller
    /// enforces is a rule this service does not have.
    /// </para>
    /// <para>
    /// A query with no criteria is not refused here. Whether an unfiltered search
    /// is allowed is a policy about who may see how much patient data, and it
    /// belongs to the authenticated, audited endpoint in front of this one.
    /// </para>
    /// </remarks>
    internal static async Task<IResult> SearchAsync(
        string? patientName,
        string? accessionNumber,
        string? studyDateFrom,
        string? studyDateTo,
        int? page,
        int? pageSize,
        IHospitalAdapterRegistry registry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registry);

        if (!StudySearchValidation.TryParseStudyDate(studyDateFrom, out DateOnly? from))
        {
            return Results.Problem(
                title: "studyDateFrom is not a date.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!StudySearchValidation.TryParseStudyDate(studyDateTo, out DateOnly? to))
        {
            return Results.Problem(
                title: "studyDateTo is not a date.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        StudySearchQuery query = new(
            patientName,
            accessionNumber,
            from,
            to,
            page ?? 0,
            pageSize ?? StudySearchLimits.DefaultPageSize);

        IReadOnlyList<string> problems = StudySearchValidation.Validate(query);
        if (problems.Count > 0)
        {
            return Results.Problem(
                title: string.Join(' ', problems),
                statusCode: StatusCodes.Status400BadRequest);
        }

        IHospitalAdapter? adapter = registry.Adapters.FirstOrDefault();

        // No source configured is an empty archive, not an error - the same
        // answer ListPatients gives.
        if (adapter is null)
        {
            return Results.Ok(new StudySearchResult([], HasMore: false, query.Page, query.PageSize));
        }

        // A source that cannot be searched is a real gap, and saying so is more
        // useful than an empty page that looks like "nothing matched".
        if (adapter is not IStudySearchSource search)
        {
            return Results.Problem(
                title: "The configured imaging source does not support study search.",
                statusCode: StatusCodes.Status501NotImplemented);
        }

        return Results.Ok(await search.SearchStudiesAsync(query, cancellationToken).ConfigureAwait(false));
    }
}
