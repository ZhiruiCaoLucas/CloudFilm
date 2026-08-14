using System.Security.Claims;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auditing;
using CloudFilm.Application.Auth;
using CloudFilm.Application.Imaging;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CloudFilm.Api.Endpoints;

/// <summary>
/// Finding studies across patients.
/// </summary>
/// <remarks>
/// <para>
/// The only endpoint in CloudFilm that returns patient data the caller did not
/// already name. Every other read starts from an identifier someone already
/// had; this one answers "who is there", which is why it is the most carefully
/// bounded route in the system: authenticated by the fallback policy, refused
/// without criteria, capped at a hundred results, and audited before a single
/// row is returned.
/// </para>
/// <para>
/// A separate file from <c>ImagingEndpoints</c>, which is frozen. Routing
/// prefers the literal <c>search</c> segment over that file's
/// <c>/{studyInstanceUid}</c> parameter, and a Study Instance UID is digits and
/// dots, so no real study could ever collide with this path.
/// </para>
/// <para>
/// The archive's query language stays behind Retrieve. Nothing here accepts a
/// limit, an offset, an include field or a wildcard: a caller states what it is
/// looking for and which page it wants, and how that becomes a QIDO query is not
/// its business.
/// </para>
/// </remarks>
internal static class SearchEndpoints
{
    private static readonly SearchError _noSubject = new("The access token carries no usable subject.");

    /// <summary>
    /// Maps the study search endpoint.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/studies/search", async Task<IResult> (
                    string? patientName,
                    string? accessionNumber,
                    string? studyDateFrom,
                    string? studyDateTo,
                    int? page,
                    int? pageSize,
                    ClaimsPrincipal principal,
                    IStudySearchCatalog catalog,
                    IAuditLogService auditLog,
                    CancellationToken cancellationToken) =>
            {
                Guid? actorUserId = ActorUserId(principal);
                if (actorUserId is null)
                {
                    return Results.Json(_noSubject, statusCode: StatusCodes.Status401Unauthorized);
                }

                if (!StudySearchValidation.TryParseStudyDate(studyDateFrom, out DateOnly? from))
                {
                    return Refuse("studyDateFrom must be YYYY-MM-DD or YYYYMMDD.");
                }

                if (!StudySearchValidation.TryParseStudyDate(studyDateTo, out DateOnly? to))
                {
                    return Refuse("studyDateTo must be YYYY-MM-DD or YYYYMMDD.");
                }

                // Refused rather than answered. A search with no criteria is a
                // request for the archive itself, one page at a time, and no
                // clinical question looks like that.
                if (string.IsNullOrWhiteSpace(patientName)
                    && string.IsNullOrWhiteSpace(accessionNumber)
                    && from is null
                    && to is null)
                {
                    return Refuse("Supply at least one of patientName, accessionNumber, studyDateFrom or studyDateTo.");
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
                    return Refuse(string.Join(' ', problems));
                }

                StudySearchResult result = await catalog
                    .SearchStudiesAsync(query, cancellationToken)
                    .ConfigureAwait(false);

                // Before the response, exactly as the patient and study reads do.
                // A search discloses who exists, so an unrecorded search is an
                // unrecorded disclosure - and if this write fails the request
                // fails with it and no result leaves the process.
                //
                // One record for one search. Forty results are not forty
                // accesses, and no resource id: the terms someone searched for
                // are themselves identifying, and this trail has nowhere to put
                // them that would not turn it into a second copy of the data it
                // exists to protect.
                await auditLog
                    .RecordAsync(
                        new AuditEntry(
                            actorUserId.Value,
                            AuditAction.SearchStudies,
                            AuditResourceType.Study,
                            ResourceId: null),
                        cancellationToken)
                    .ConfigureAwait(false);

                return Results.Ok(result);
            })
            .WithName("SearchStudies")
            .WithTags("Studies")
            .WithSummary("Finds studies by patient name, accession number or study date.")
            .RequireAuthorization(AuthorizationPolicies.RequiresApplicationRole)
            .Produces<StudySearchResult>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }

    private static IResult Refuse(string title) =>
        Results.Problem(title: title, statusCode: StatusCodes.Status400BadRequest);

    /// <summary>
    /// The account behind the bearer token.
    /// </summary>
    /// <param name="principal">The validated caller.</param>
    /// <returns>The account identifier, or <see langword="null"/> if the token carries no usable subject.</returns>
    /// <remarks>
    /// Reads the registered <c>sub</c> claim, because the bearer handler sets
    /// <c>MapInboundClaims = false</c> and claims keep the names they were issued
    /// with. Null is refused rather than treated as an anonymous actor: a search
    /// that cannot be attributed to anyone must not be performed at all.
    /// </remarks>
    private static Guid? ActorUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid id)
            ? id
            : null;
}

/// <summary>
/// The body returned when a search cannot be attributed to a user.
/// </summary>
/// <param name="Error">A message that names no account and no search term.</param>
internal sealed record SearchError(string Error);
