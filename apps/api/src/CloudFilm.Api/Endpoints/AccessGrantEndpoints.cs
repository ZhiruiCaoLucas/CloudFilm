using System.Security.Claims;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auditing;
using CloudFilm.Application.Auth;
using CloudFilm.Application.Grants;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CloudFilm.Api.Endpoints;

/// <summary>
/// Deciding who may reach a study they do not otherwise hold.
/// </summary>
/// <remarks>
/// <para>
/// Administrative, not clinical. Every route here carries
/// <see cref="AuthorizationPolicies.RequiresAdministrator"/>: a clinician doing
/// clinical work reads studies, and has no reason to decide who else may. This
/// is the first place CloudFilm's two roles differ, and the difference is about
/// granting access rather than having it.
/// </para>
/// <para>
/// Nothing here asks the archive whether a study exists. Checking would put a
/// Retrieve round trip on an administrative call, and it would turn this into a
/// probe that answers "does this archive hold that study?" for anyone allowed to
/// create a grant. A grant against a study nobody holds permits nothing.
/// </para>
/// <para>
/// Grants are their own resource rather than a sub-resource of a study, because
/// a revocation names one grant: the same account may have been granted access
/// to the same study several times over, and only the record somebody pointed at
/// should be withdrawn.
/// </para>
/// </remarks>
internal static class AccessGrantEndpoints
{
    private static readonly AccessGrantError _noSubject =
        new("The access token carries no usable subject.");

    /// <summary>
    /// The single answer to every rejected create.
    /// </summary>
    /// <remarks>
    /// One message for a malformed identifier, an unrecognised permission and an
    /// account that does not exist. Saying which would turn this endpoint into
    /// an account enumerator - the same disclosure the sign-in path collapses
    /// its four failure reasons to avoid - and an administrator creating a grant
    /// already knows which values they sent.
    /// </remarks>
    private const string _invalidRequest =
        "The grant could not be created. Check the study identifier, the permission and the account.";

    /// <summary>
    /// Maps the access-grant endpoints.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapAccessGrantEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder grants = app
            .MapGroup("/api/v1/access-grants")
            .WithTags("Access grants")

            // Group level: every route under this prefix decides or reports who
            // may see a patient's imaging, so they carry one rule and a future
            // route inherits it rather than being forgotten.
            .RequireAuthorization(AuthorizationPolicies.RequiresAdministrator);

        grants.MapPost("/", async Task<IResult> (
                    CreateAccessGrantRequest request,
                    ClaimsPrincipal principal,
                    IAccessGrantStore store,
                    IAccessGrantQuery query,
                    IUserDirectory users,
                    IAuditLogService auditLog,
                    IUnitOfWork unitOfWork,
                    CancellationToken cancellationToken) =>
            {
                Guid? actorUserId = ActorUserId(principal);
                if (actorUserId is null)
                {
                    return Results.Json(_noSubject, statusCode: StatusCodes.Status401Unauthorized);
                }

                string studyInstanceUid = request.StudyInstanceUid?.Trim() ?? string.Empty;

                if (studyInstanceUid.Length == 0
                    || studyInstanceUid.Length > GrantLimits.MaxStudyInstanceUidLength
                    || !GrantPermissions.IsRecognised(request.Permission))
                {
                    return Refuse();
                }

                // Checked, but never reported. The answer decides whether the
                // request is valid; disclosing it would answer "does this
                // account exist?" to anyone who may create a grant.
                if (!await users.ExistsAsync(request.GranteeUserId, cancellationToken).ConfigureAwait(false))
                {
                    return Refuse();
                }

                return await unitOfWork
                    .ExecuteAsync(
                        async token =>
                        {
                            bool created = await store
                                .GrantAsync(
                                    request.GranteeUserId,
                                    studyInstanceUid,
                                    request.Permission,

                                    // From the token, never from the body. The
                                    // request has nowhere to name a granting
                                    // account, so no client can attribute its own
                                    // grant to somebody else.
                                    actorUserId.Value,
                                    request.ExpiresAtUtc,
                                    token)
                                .ConfigureAwait(false);

                            if (!created)
                            {
                                // One live grant per account per study. Widening
                                // or narrowing is a revoke and a new grant, so
                                // that both appear in the record rather than one
                                // silently replacing the other.
                                return Results.Problem(
                                    title: "That account already holds an access grant on this study.",
                                    statusCode: StatusCodes.Status409Conflict);
                            }

                            // Before the response, like every other write in
                            // CloudFilm. If this fails the grant is rolled back
                            // with it: an unrecorded widening of who can see a
                            // patient's imaging is worse than a failed request.
                            await auditLog
                                .RecordAsync(
                                    new AuditEntry(
                                        actorUserId.Value,
                                        AuditAction.GrantCreated,
                                        AuditResourceType.Study,
                                        studyInstanceUid),
                                    token)
                                .ConfigureAwait(false);

                            StudyAccessGrant? grant = await query
                                .FindActiveAsync(request.GranteeUserId, studyInstanceUid, token)
                                .ConfigureAwait(false);

                            return grant is null
                                ? Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
                                : Results.Created($"/api/v1/access-grants/{grant.Id}", grant);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            })
            .WithName("CreateAccessGrant")
            .WithSummary("Grants an account access to a study.")
            .Produces<StudyAccessGrant>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict);

        grants.MapGet("/", async Task<IResult> (
                    string? studyInstanceUid,
                    IAccessGrantQuery query,
                    CancellationToken cancellationToken) =>
            {
                string study = studyInstanceUid?.Trim() ?? string.Empty;

                if (study.Length == 0 || study.Length > GrantLimits.MaxStudyInstanceUidLength)
                {
                    return Results.Problem(
                        title: "A study identifier is required.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                // Live grants only. Revoked and expired rows stay in the table as
                // the record of who could once see what; they are not access, and
                // a list of who may see a study must not imply they are.
                return Results.Ok(
                    await query.ListForStudyAsync(study, cancellationToken).ConfigureAwait(false));
            })
            .WithName("ListAccessGrants")
            .WithSummary("Lists the grants currently in force on a study.")
            .Produces<IReadOnlyList<StudyAccessGrant>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        grants.MapDelete("/{grantId:guid}", async Task<IResult> (
                    Guid grantId,
                    ClaimsPrincipal principal,
                    IAccessGrantStore store,
                    IAccessGrantQuery query,
                    IAuditLogService auditLog,
                    IUnitOfWork unitOfWork,
                    CancellationToken cancellationToken) =>
            {
                Guid? actorUserId = ActorUserId(principal);
                if (actorUserId is null)
                {
                    return Results.Json(_noSubject, statusCode: StatusCodes.Status401Unauthorized);
                }

                // Read first, because the audit record has to name the study the
                // grant was for, and because "no such grant" and "already
                // revoked" are different answers.
                StudyAccessGrant? grant = await query
                    .FindAsync(grantId, cancellationToken)
                    .ConfigureAwait(false);

                if (grant is null)
                {
                    return Results.NotFound();
                }

                return await unitOfWork
                    .ExecuteAsync(
                        async token =>
                        {
                            bool revoked = await store
                                .RevokeByIdAsync(grantId, token)
                                .ConfigureAwait(false);

                            // Only a real withdrawal is recorded. Revoking
                            // something already revoked changed nothing, and the
                            // answer is the same either way so that repeating the
                            // request is safe.
                            if (revoked)
                            {
                                await auditLog
                                    .RecordAsync(
                                        new AuditEntry(
                                            actorUserId.Value,
                                            AuditAction.GrantRevoked,
                                            AuditResourceType.Study,
                                            grant.StudyInstanceUid),
                                        token)
                                    .ConfigureAwait(false);
                            }

                            return Results.NoContent();
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            })
            .WithName("RevokeAccessGrant")
            .WithSummary("Withdraws one access grant.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static IResult Refuse() =>
        Results.Problem(title: _invalidRequest, statusCode: StatusCodes.Status400BadRequest);

    /// <summary>
    /// The account behind the bearer token.
    /// </summary>
    /// <param name="principal">The validated caller.</param>
    /// <returns>The account identifier, or <see langword="null"/> if the token carries no usable subject.</returns>
    /// <remarks>
    /// Reads the registered <c>sub</c> claim, because the bearer handler sets
    /// <c>MapInboundClaims = false</c>. Null is refused rather than treated as an
    /// anonymous actor: a grant nobody can be held responsible for must not be
    /// created at all.
    /// </remarks>
    private static Guid? ActorUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid id)
            ? id
            : null;
}

/// <summary>
/// The body returned when a grant request cannot be attributed to a user.
/// </summary>
/// <param name="Error">A message that names no account and no study.</param>
internal sealed record AccessGrantError(string Error);
