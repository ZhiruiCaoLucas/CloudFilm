using System.Security.Claims;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auditing;
using CloudFilm.Application.Auth;
using CloudFilm.Application.ReadState;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CloudFilm.Api.Endpoints;

/// <summary>
/// Whether the signed-in user has read a study.
/// </summary>
/// <remarks>
/// <para>
/// CloudFilm's own workflow state, and the first thing in the system that is not
/// a projection of what the archive holds. Orthanc remains the source of truth
/// for images; this is one clinician's note to themselves about one study, and
/// two clinicians reading the same study will legitimately disagree.
/// </para>
/// <para>
/// Carries no authorisation metadata, like the imaging and access endpoints. The
/// fallback policy closes every endpoint that does not opt out, so these are
/// authenticated by default rather than by remembering to say so.
/// </para>
/// <para>
/// Nothing here asks the archive whether the study exists. The state is a
/// private annotation in CloudFilm's own table, so a mark against an unknown UID
/// is harmless, whereas checking would add a Retrieve round trip to every call
/// and turn the endpoint into an oracle for which studies the archive holds.
/// </para>
/// <para>
/// Mounted under <c>/api/v1/imaging/studies</c> rather than <c>/api/v1/studies</c>.
/// The latter is owned by <c>ImagingEndpoints</c>, which is frozen; a second
/// group on the same prefix would work but would split one path's routes across
/// two files that a reader has no reason to connect.
/// </para>
/// </remarks>
internal static class ReadStateEndpoints
{
    /// <summary>
    /// Returned when a validated token carries no usable subject.
    /// </summary>
    /// <remarks>
    /// Unreachable through the fallback policy, which stops unauthenticated
    /// requests before they arrive. It exists because the alternative - writing
    /// a read mark that belongs to nobody, or reading one - is worse than
    /// refusing, and because "no actor" must never be quietly treated as an
    /// actor here the way an anonymous audit entry can be.
    /// </remarks>
    private static readonly ReadStateError _noSubject = new("The access token carries no usable subject.");

    /// <summary>
    /// Maps the study read-state endpoints.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapReadStateEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder studies = app
            .MapGroup("/api/v1/imaging/studies")
            .WithTags("Read state")

            // Group level: the read and both mutations answer the same question
            // about the same private state, so they carry the same rule and a
            // future fourth route inherits it rather than being forgotten.
            .RequireAuthorization(AuthorizationPolicies.RequiresApplicationRole);

        studies.MapGet("/{studyInstanceUid}/read-state", async Task<IResult> (
                    string studyInstanceUid,
                    ClaimsPrincipal principal,
                    IStudyReadStateStore readState,
                    CancellationToken cancellationToken) =>
            {
                (IResult? failure, Guid actorUserId, string uid) = Validate(principal, studyInstanceUid);

                if (failure is not null)
                {
                    return failure;
                }

                bool read = await readState
                    .IsReadAsync(actorUserId, uid, cancellationToken)
                    .ConfigureAwait(false);

                return Results.Ok(new StudyReadState(read));
            })
            .WithName("GetStudyReadState")
            .WithSummary("Reports whether the signed-in user has marked this study read.")
            .Produces<StudyReadState>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        // PUT, not POST: the request states the complete desired state of a
        // single-valued resource, so sending it twice must mean the same as
        // sending it once. It carries no body - the verb and the route say
        // everything, and a body would be a second place for the client to
        // disagree with itself.
        studies.MapPut("/{studyInstanceUid}/read-state", async Task<IResult> (
                    string studyInstanceUid,
                    ClaimsPrincipal principal,
                    IStudyReadStateStore readState,
                    IAuditLogService auditLog,
                    IUnitOfWork unitOfWork,
                    CancellationToken cancellationToken) =>
            {
                (IResult? failure, Guid actorUserId, string uid) = Validate(principal, studyInstanceUid);

                if (failure is not null)
                {
                    return failure;
                }

                StudyReadState state = await unitOfWork
                    .ExecuteAsync(
                        async token =>
                        {
                            bool changed = await readState
                                .MarkReadAsync(actorUserId, uid, token)
                                .ConfigureAwait(false);

                            // Only a real transition is recorded. The audit write
                            // is inside the transaction, so if it fails the mark
                            // is rolled back with it and the trail never
                            // disagrees with the state.
                            if (changed)
                            {
                                await auditLog
                                    .RecordAsync(
                                        new AuditEntry(
                                            actorUserId,
                                            AuditAction.MarkStudyRead,
                                            AuditResourceType.Study,
                                            uid),
                                        token)
                                    .ConfigureAwait(false);
                            }

                            return new StudyReadState(true);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                return Results.Ok(state);
            })
            .WithName("MarkStudyRead")
            .WithSummary("Marks this study read for the signed-in user.")
            .Produces<StudyReadState>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        // DELETE removes the mark. It is the honest verb for a state whose
        // storage is the presence of a row, and it keeps the unread direction
        // idempotent for the same reason PUT is.
        studies.MapDelete("/{studyInstanceUid}/read-state", async Task<IResult> (
                    string studyInstanceUid,
                    ClaimsPrincipal principal,
                    IStudyReadStateStore readState,
                    IAuditLogService auditLog,
                    IUnitOfWork unitOfWork,
                    CancellationToken cancellationToken) =>
            {
                (IResult? failure, Guid actorUserId, string uid) = Validate(principal, studyInstanceUid);

                if (failure is not null)
                {
                    return failure;
                }

                StudyReadState state = await unitOfWork
                    .ExecuteAsync(
                        async token =>
                        {
                            bool changed = await readState
                                .MarkUnreadAsync(actorUserId, uid, token)
                                .ConfigureAwait(false);

                            if (changed)
                            {
                                await auditLog
                                    .RecordAsync(
                                        new AuditEntry(
                                            actorUserId,
                                            AuditAction.MarkStudyUnread,
                                            AuditResourceType.Study,
                                            uid),
                                        token)
                                    .ConfigureAwait(false);
                            }

                            return new StudyReadState(false);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                // 200 with the resulting state rather than 204. The client needs
                // to render "unread" either way, and a body it can read removes
                // the temptation to assume what the state became.
                return Results.Ok(state);
            })
            .WithName("MarkStudyUnread")
            .WithSummary("Marks this study unread for the signed-in user.")
            .Produces<StudyReadState>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    /// <summary>
    /// Establishes who is asking and about which study.
    /// </summary>
    /// <param name="principal">The validated caller.</param>
    /// <param name="studyInstanceUid">The identifier as it arrived in the route.</param>
    /// <returns>
    /// A failure response, or the account and the normalised identifier to act
    /// on.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The account comes from the token's <c>sub</c> claim and from nowhere else.
    /// No route, query or body parameter names a user, so there is no request
    /// that reads or writes somebody else's state.
    /// </para>
    /// <para>
    /// The identifier is trimmed, because the archive trims before it looks a
    /// study up and a padded UID would otherwise key a second row for the same
    /// study. It is then length checked against the column so an over-long value
    /// is refused with an explanation rather than by the database. Nothing is
    /// case folded: a DICOM UID is digits and dots and has no case.
    /// </para>
    /// </remarks>
    private static (IResult? Failure, Guid ActorUserId, string StudyInstanceUid) Validate(
        ClaimsPrincipal principal,
        string studyInstanceUid)
    {
        if (!Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid actorUserId))
        {
            return (
                Results.Json(_noSubject, statusCode: StatusCodes.Status401Unauthorized),
                Guid.Empty,
                string.Empty);
        }

        string trimmed = studyInstanceUid.Trim();

        if (trimmed.Length == 0)
        {
            return (
                Results.Problem(
                    title: "A Study Instance UID is required.",
                    statusCode: StatusCodes.Status400BadRequest),
                Guid.Empty,
                string.Empty);
        }

        if (trimmed.Length > StudyReadStateLimits.MaxStudyInstanceUidLength)
        {
            // Refused rather than truncated. A truncated UID identifies a
            // different study, and silently marking the wrong one read is worse
            // than refusing the request.
            return (
                Results.Problem(
                    title: "The Study Instance UID is too long.",
                    statusCode: StatusCodes.Status400BadRequest),
                Guid.Empty,
                string.Empty);
        }

        return (null, actorUserId, trimmed);
    }
}

/// <summary>
/// The body returned when a read-state request cannot be attributed to a user.
/// </summary>
/// <param name="Error">A message that names no account and no study.</param>
internal sealed record ReadStateError(string Error);
