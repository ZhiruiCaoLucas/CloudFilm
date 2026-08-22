using System.Security.Claims;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auditing;
using CloudFilm.Application.Auth;
using CloudFilm.Application.Grants;
using CloudFilm.Application.Sharing;
using CloudFilm.Infrastructure.Sharing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CloudFilm.Api.Endpoints;

/// <summary>
/// Issuing and withdrawing links that lead to one study.
/// </summary>
/// <remarks>
/// <para>
/// Administrative, like access grants, and closed to a clinician for the same
/// reason: handing out a credential that reaches a patient's imaging is not
/// clinical work. Every route carries
/// <see cref="AuthorizationPolicies.RequiresAdministrator"/>.
/// </para>
/// <para>
/// Both permissions are issued. A view link used to be refused here, because the
/// viewer read pixels over a path that did not pass through CloudFilm and a link
/// that "granted viewing" would have been neither required to see the study nor
/// sufficient to stop anyone else seeing it. That path is now authorised for
/// every request, so the promise a view link makes is one the system keeps: it
/// opens the viewer on one study and it is refused by the download route.
/// </para>
/// <para>
/// The permission is stored as sent and enforced where it bites, by the routes a
/// holder actually calls. Nothing here decides what a link can do beyond
/// recording which of the two recognised permissions it carries.
/// </para>
/// <para>
/// Nothing here asks the archive whether a study exists. That would put a
/// Retrieve round trip on an administrative call and turn the endpoint into a
/// probe for which studies are held. A link to a study nobody holds leads
/// nowhere.
/// </para>
/// </remarks>
internal static class ShareLinkEndpoints
{
    private static readonly ShareLinkError _noSubject =
        new("The access token carries no usable subject.");

    /// <summary>
    /// The single answer to every rejected issue request.
    /// </summary>
    /// <remarks>
    /// One message for a missing identifier, one too long, an unusable
    /// permission and an impossible expiry. An administrator issuing a link
    /// already knows what they sent, and a response that separates the cases is
    /// a response that can be used to learn things by trying.
    /// </remarks>
    private const string _invalidRequest =
        "The share link could not be created. Check the study identifier, the permission and the expiry.";

    /// <summary>
    /// Maps the share link endpoints.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapShareLinkEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder links = app
            .MapGroup("/api/v1/share-links")
            .WithTags("Share links")

            // Group level, so a route added under this prefix later inherits the
            // rule rather than being forgotten.
            .RequireAuthorization(AuthorizationPolicies.RequiresAdministrator);

        links.MapPost("/", async Task<IResult> (
                    CreateShareLinkRequest request,
                    ClaimsPrincipal principal,
                    IShareLinkStore store,
                    IAuditLogService auditLog,
                    IUnitOfWork unitOfWork,
                    IClock clock,
                    CancellationToken cancellationToken) =>
            {
                Guid? actorUserId = ActorUserId(principal);
                if (actorUserId is null)
                {
                    return Results.Json(_noSubject, statusCode: StatusCodes.Status401Unauthorized);
                }

                string studyInstanceUid = request.StudyInstanceUid?.Trim() ?? string.Empty;

                if (studyInstanceUid.Length == 0
                    || studyInstanceUid.Length > ShareLinkLimits.MaxStudyInstanceUidLength)
                {
                    return Refuse();
                }

                // One of the two stored permissions, exactly as spelled. The
                // comparison is ordinal and case sensitive on purpose: "download"
                // is not Download, and a link whose permission the ladder does not
                // recognise would be a link nothing downstream could evaluate.
                if (!GrantPermissions.IsRecognised(request.Permission))
                {
                    return Refuse();
                }

                DateTimeOffset now = clock.UtcNow;

                // Compared in UTC whatever offset the caller sent, so a client on
                // another clock cannot buy itself a longer link. A missing field
                // deserialises to default(DateTimeOffset), which is in the year
                // one and so fails the first test - absent and impossible are the
                // same answer.
                DateTimeOffset expiresAtUtc = request.ExpiresAtUtc.ToUniversalTime();

                if (expiresAtUtc <= now || expiresAtUtc > now + ShareLinkLimits.MaximumLifetime)
                {
                    return Refuse();
                }

                // Minted here and hashed immediately. The plaintext exists in this
                // method and in the response it returns; the store, the audit
                // trail and the database see only the digest.
                string token = ShareTokens.Create();
                byte[] tokenHash = ShareTokens.Hash(token);

                return await unitOfWork
                    .ExecuteAsync(
                        async operationToken =>
                        {
                            ShareLinkSummary link = await store
                                .CreateAsync(
                                    studyInstanceUid,

                                    // What was asked for, not a constant. The
                                    // redemption and download routes both read
                                    // this back through the permission ladder,
                                    // so a View link opens the viewer and is
                                    // refused a download, and a Download link
                                    // does both.
                                    request.Permission,
                                    tokenHash,

                                    // From the token, never from the body. The
                                    // request has nowhere to name an issuer.
                                    actorUserId.Value,
                                    expiresAtUtc,
                                    operationToken)
                                .ConfigureAwait(false);

                            // Before the response, like every other write. If it
                            // fails the link is rolled back with it: an
                            // unrecorded credential to a patient's imaging is
                            // worse than a failed request.
                            //
                            // The study, and nothing else. No token, no digest,
                            // no link identifier that could be mistaken for one.
                            await auditLog
                                .RecordAsync(
                                    new AuditEntry(
                                        actorUserId.Value,
                                        AuditAction.ShareLinkCreated,
                                        AuditResourceType.Study,
                                        studyInstanceUid),
                                    operationToken)
                                .ConfigureAwait(false);

                            CreatedShareLink created = new(
                                link.Id,
                                link.StudyInstanceUid,
                                link.Permission,
                                link.CreatedAtUtc,
                                link.ExpiresAtUtc,
                                token);

                            return Results.Created($"/api/v1/share-links/{link.Id}", created);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            })
            .WithName("CreateShareLink")
            .WithSummary("Issues a link that leads to one study.")
            .Produces<CreatedShareLink>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        links.MapDelete("/{shareLinkId:guid}", async Task<IResult> (
                    Guid shareLinkId,
                    ClaimsPrincipal principal,
                    IShareLinkStore store,
                    IAuditLogService auditLog,
                    IUnitOfWork unitOfWork,
                    CancellationToken cancellationToken) =>
            {
                Guid? actorUserId = ActorUserId(principal);
                if (actorUserId is null)
                {
                    return Results.Json(_noSubject, statusCode: StatusCodes.Status401Unauthorized);
                }

                return await unitOfWork
                    .ExecuteAsync(
                        async operationToken =>
                        {
                            // Returns the link as it stood before the call: null
                            // for no such link, a standing one for a withdrawal
                            // that just happened, an already-withdrawn one for a
                            // repeat.
                            ShareLinkSummary? before = await store
                                .RevokeAsync(shareLinkId, operationToken)
                                .ConfigureAwait(false);

                            if (before is null)
                            {
                                return Results.NotFound();
                            }

                            if (before.RevokedAtUtc is null)
                            {
                                await auditLog
                                    .RecordAsync(
                                        new AuditEntry(
                                            actorUserId.Value,
                                            AuditAction.ShareLinkRevoked,
                                            AuditResourceType.Study,
                                            before.StudyInstanceUid),
                                        operationToken)
                                    .ConfigureAwait(false);
                            }

                            // Same answer whether this call withdrew the link or
                            // found it already withdrawn, so repeating the
                            // request is safe and tells the caller nothing new.
                            return Results.NoContent();
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            })
            .WithName("RevokeShareLink")
            .WithSummary("Withdraws one share link.")
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
    /// anonymous actor: a credential nobody can be held responsible for must not
    /// be issued at all.
    /// </remarks>
    private static Guid? ActorUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid id)
            ? id
            : null;
}

/// <summary>
/// The body returned when a share link request cannot be attributed to a user.
/// </summary>
/// <param name="Error">A message that names no account, study or token.</param>
internal sealed record ShareLinkError(string Error);
