using System.Security.Claims;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auditing;
using CloudFilm.Application.Auth;
using CloudFilm.Application.Grants;
using CloudFilm.Application.Sharing;
using CloudFilm.Infrastructure.Sharing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace CloudFilm.Api.Endpoints;

/// <summary>
/// Viewer sessions, and the gate the imaging proxy asks before it serves one.
/// </summary>
/// <remarks>
/// <para>
/// The viewer talks to Orthanc through nginx, not through this API, so nothing
/// on the API's own routes can protect it. These three endpoints close that gap
/// without moving the imaging traffic: two of them mint a short-lived session
/// bound to one study, and the third is what nginx calls - once per DICOMweb
/// request - to ask whether the session in the browser's cookie admits the
/// request it is holding.
/// </para>
/// <para>
/// Both kinds of viewer use the same session. A clinician entering the workspace
/// and a stranger opening a share link are told apart when the session is
/// created, where the difference actually matters, and are indistinguishable
/// afterwards. The gate therefore has one rule to enforce rather than two, which
/// is the only reason it is small enough to be sure of.
/// </para>
/// <para>
/// The gate never inspects a bearer token. Copying JWT validation into a route
/// that runs hundreds of times per study would be a second implementation of the
/// thing that decides who sees imaging, and two implementations of that drift.
/// </para>
/// </remarks>
internal static class ShareViewEndpoints
{
    /// <summary>
    /// The length of a share token in its encoded form.
    /// </summary>
    /// <remarks>
    /// 32 random bytes, Base64Url, unpadded. Duplicated from the download route
    /// rather than shared, because these two endpoints must be able to change
    /// their refusal behaviour independently.
    /// </remarks>
    private const int _encodedTokenLength = 43;

    /// <summary>
    /// The DICOMweb root the viewer's proxy exposes.
    /// </summary>
    /// <remarks>
    /// Matches the <c>location</c> block in the viewer's nginx configuration. If
    /// one moves the other must, and the gate fails closed if they disagree.
    /// </remarks>
    private const string _dicomWebPrefix = "/dicom-web/";

    /// <summary>
    /// How many times one link may be redeemed for viewing inside
    /// <see cref="_rateLimitWindow"/>.
    /// </summary>
    /// <remarks>
    /// The same ceiling the download route applies, for the same reason: it is
    /// not what stops a token being guessed - 256 bits of randomness is - but it
    /// bounds what a leaked link can be turned into before somebody notices and
    /// withdraws it. Each redemption also writes an audit row, so an unbounded
    /// route would let one URL fill the trail it exists to keep readable.
    /// </remarks>
    private const int _maxRedemptionsPerWindow = 20;

    private static readonly TimeSpan _rateLimitWindow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maps the viewer session and gateway endpoints.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapShareViewEndpoints(this IEndpointRouteBuilder app)
    {
        MapAnonymousRedemption(app);
        MapAuthenticatedSession(app);
        MapGateway(app);

        return app;
    }

    /// <summary>
    /// Exchanges a share token for a viewer session.
    /// </summary>
    /// <remarks>
    /// The one place a share link is turned into viewing, and so the one place
    /// viewing is recorded. Everything the browser does afterwards is checked
    /// against the session, not the link, and is not audited: opening a single
    /// study issues dozens to hundreds of DICOMweb requests, and a row for each
    /// would bury the fact that matters under the traffic it generated.
    /// </remarks>
    private static void MapAnonymousRedemption(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/share/{token}/viewer-session", async Task<IResult> (
                    string token,
                    IShareLinkStore shareLinks,
                    IViewerSessionStore viewerSessions,
                    IAuditLogService auditLog,
                    IUnitOfWork unitOfWork,
                    ICacheStore cache,
                    CancellationToken cancellationToken) =>
            {
                if (!LooksLikeToken(token))
                {
                    return Refuse();
                }

                byte[] tokenHash = ShareTokens.Hash(token);

                // Unknown, expired and withdrawn arrive here as one answer, and
                // leave as one answer.
                ShareLinkTarget? target = await shareLinks
                    .FindActiveAsync(tokenHash, cancellationToken)
                    .ConfigureAwait(false);

                if (target is null)
                {
                    return Refuse();
                }

                // Viewing, or something that covers it: a View link qualifies on
                // its own, a Download link qualifies through the ladder. Read from
                // what the link permits rather than from what we expect it to be,
                // so a permission this route has never seen fails closed.
                if (!GrantPermissions.Satisfies(target.Permission, GrantPermissions.View))
                {
                    return Refuse();
                }

                if (await IsRateLimitedAsync(cache, tokenHash, cancellationToken).ConfigureAwait(false))
                {
                    // Same answer as an invalid token. Telling a caller they have
                    // been throttled tells them the token was real.
                    return Refuse();
                }

                ViewerSession session = await viewerSessions
                    .CreateAsync(
                        target.StudyInstanceUid,
                        target.Permission,
                        target.ShareLinkId,
                        cancellationToken)
                    .ConfigureAwait(false);

                // After the session exists but before the caller is told of it,
                // in a transaction: if the record cannot be written the caller
                // gets an error and never learns the identifier, and the orphaned
                // session expires unused.
                await unitOfWork
                    .ExecuteAsync(
                        async operationToken =>
                        {
                            await auditLog
                                .RecordAsync(
                                    new AuditEntry(
                                        // A link is redeemed by whoever holds it.
                                        ActorUserId: null,
                                        AuditAction.ShareLinkViewed,
                                        AuditResourceType.Study,
                                        target.StudyInstanceUid),
                                    operationToken)
                                .ConfigureAwait(false);

                            return true;
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                return Results.Ok(session);
            })
            .WithName("RedeemShareLinkForViewing")
            .WithTags("Sharing")
            .WithSummary("Exchanges a share token for a short-lived viewer session.")
            .AllowAnonymous()
            .Produces<ViewerSession>()
            .Produces(StatusCodes.Status404NotFound);

    /// <summary>
    /// Issues a viewer session to a signed-in clinician.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately scoped to one study, even though the caller may be entitled
    /// to the whole catalogue. The session is a cookie in a browser that is about
    /// to load third-party viewer code; narrowing it to the study on screen means
    /// that cookie cannot be turned into a tour of the archive.
    /// </para>
    /// <para>
    /// No audit entry. Reading the study is already recorded, by the request that
    /// loaded it; this only converts an entitlement the caller already has into a
    /// form nginx can check.
    /// </para>
    /// </remarks>
    private static void MapAuthenticatedSession(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/imaging/studies/{studyInstanceUid}/viewer-session", async Task<IResult> (
                    string studyInstanceUid,
                    ClaimsPrincipal principal,
                    IStudyAccessPolicy accessPolicy,
                    IViewerSessionStore viewerSessions,
                    CancellationToken cancellationToken) =>
            {
                // The same decision the study endpoint makes, made again rather
                // than trusted from the client: the browser asking for a session
                // is not evidence that it was shown the study.
                StudyAccessScope scope = await accessPolicy
                    .AuthoriseAsync(principal, studyInstanceUid, GrantPermissions.View, cancellationToken)
                    .ConfigureAwait(false);

                if (scope == StudyAccessScope.Denied)
                {
                    return Results.NotFound();
                }

                ViewerSession session = await viewerSessions
                    .CreateAsync(
                        studyInstanceUid,
                        GrantPermissions.View,
                        // Not from a link, so nothing to revoke: this session
                        // stands on the caller's own entitlement for its fifteen
                        // minutes.
                        shareLinkId: null,
                        cancellationToken)
                    .ConfigureAwait(false);

                return Results.Ok(session);
            })
            .WithName("CreateViewerSession")
            .WithTags("Imaging")
            .WithSummary("Issues a viewer session for one study to a signed-in caller.")
            .RequireAuthorization(AuthorizationPolicies.RequiresApplicationRole)
            .Produces<ViewerSession>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

    /// <summary>
    /// The check nginx runs before it proxies a DICOMweb request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Answers only 204 or 401, because those are the only answers
    /// <c>auth_request</c> understands - anything else becomes a 500 for the
    /// person holding the browser. nginx turns the 401 into the same 404 the rest
    /// of the sharing surface returns.
    /// </para>
    /// <para>
    /// The request being judged is not this one. nginx passes the original URI in
    /// a header, and every decision is made from that string plus the cookie, so
    /// this endpoint must treat the header as hostile input and refuse anything
    /// it does not positively recognise.
    /// </para>
    /// </remarks>
    private static void MapGateway(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/share/dicom-web/authorize", async Task<IResult> (
                    HttpContext context,
                    IViewerSessionStore viewerSessions,
                    IShareLinkStatus shareLinks,
                    CancellationToken cancellationToken) =>
            {
                string? sessionId = context.Request.Cookies[ViewerSessions.CookieName];

                if (string.IsNullOrEmpty(sessionId))
                {
                    return Deny();
                }

                ViewerSessionState? session = await viewerSessions
                    .FindAsync(sessionId, cancellationToken)
                    .ConfigureAwait(false);

                if (session is null)
                {
                    return Deny();
                }

                if (!GrantPermissions.Satisfies(session.Permission, GrantPermissions.View))
                {
                    return Deny();
                }

                if (!Admits(context.Request.Headers["X-Original-URI"].ToString(), session.StudyInstanceUid))
                {
                    return Deny();
                }

                // Last, because it is the only check that touches the database.
                // Asked on every request rather than once per session: a
                // withdrawn link must stop the next image, not the next session.
                if (session.ShareLinkId is Guid shareLinkId)
                {
                    bool live = await shareLinks
                        .IsActiveAsync(shareLinkId, cancellationToken)
                        .ConfigureAwait(false);

                    if (!live)
                    {
                        return Deny();
                    }
                }

                return Results.NoContent();
            })
            .WithName("AuthoriseDicomWebRequest")
            .WithTags("Sharing")
            .WithSummary("Tells the imaging proxy whether a viewer session admits one DICOMweb request.")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized);

    /// <summary>
    /// Reports whether a DICOMweb request is about one permitted study.
    /// </summary>
    /// <param name="originalUri">The URI nginx received, path and query.</param>
    /// <param name="studyInstanceUid">The study the session admits.</param>
    /// <returns><see langword="true"/> only when the request cannot reach anything else.</returns>
    /// <remarks>
    /// <para>
    /// Allows two shapes and refuses everything else. A path under
    /// <c>/dicom-web/studies/{uid}</c> is admitted when the identifier in the path
    /// is the session's; a search under <c>studies</c>, <c>series</c> or
    /// <c>instances</c> is admitted only when its query names that same study.
    /// </para>
    /// <para>
    /// The unfiltered search is the case that matters. <c>/dicom-web/studies</c>
    /// with no <c>StudyInstanceUID</c> returns the archive's index - every study
    /// of every patient - which is why an absent filter is refused rather than
    /// treated as "no restriction requested".
    /// </para>
    /// <para>
    /// Deny by default, and no attempt to sanitise: a URI that does not match one
    /// of the two shapes exactly is refused, rather than trimmed or normalised
    /// into something that does.
    /// </para>
    /// </remarks>
    private static bool Admits(string originalUri, string studyInstanceUid)
    {
        if (string.IsNullOrEmpty(originalUri))
        {
            return false;
        }

        // Must be an absolute path. A scheme-relative value would make the
        // comparisons below say nothing about where the request goes.
        if (originalUri[0] != '/' || originalUri.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        int queryStart = originalUri.IndexOf('?', StringComparison.Ordinal);
        string path = queryStart < 0 ? originalUri : originalUri[..queryStart];
        string query = queryStart < 0 ? string.Empty : originalUri[queryStart..];

        string decodedPath = Uri.UnescapeDataString(path);

        // Refused rather than resolved. Traversal in a path that has already been
        // matched against a prefix is either an attack or a bug, and neither
        // should be served.
        if (decodedPath.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        if (!decodedPath.StartsWith(_dicomWebPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string resource = decodedPath[_dicomWebPrefix.Length..];

        const string studies = "studies/";

        if (resource.StartsWith(studies, StringComparison.Ordinal))
        {
            string remainder = resource[studies.Length..];
            int separator = remainder.IndexOf('/', StringComparison.Ordinal);
            string requested = separator < 0 ? remainder : remainder[..separator];

            // Ordinal, and whole. Study Instance UIDs are ASCII identifiers, and
            // a prefix match here would admit a sibling study whose identifier
            // begins with the permitted one.
            return string.Equals(requested, studyInstanceUid, StringComparison.Ordinal);
        }

        string collection = resource.TrimEnd('/');

        if (collection is not ("studies" or "series" or "instances"))
        {
            return false;
        }

        return NamesStudy(query, studyInstanceUid);
    }

    /// <summary>
    /// Reports whether a DICOMweb search is confined to one study.
    /// </summary>
    /// <remarks>
    /// Requires the filter to be present and requires every value of it to be the
    /// permitted study: repeating the parameter with a second identifier is a
    /// request for two studies, and is refused rather than partly honoured. Both
    /// the keyword and the tag number are accepted, because DICOMweb clients use
    /// either.
    /// </remarks>
    private static bool NamesStudy(string query, string studyInstanceUid)
    {
        Dictionary<string, StringValues> parameters = QueryHelpers.ParseQuery(query);

        bool named = false;

        foreach ((string key, StringValues values) in parameters)
        {
            bool isStudyFilter =
                string.Equals(key, "StudyInstanceUID", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "0020000D", StringComparison.OrdinalIgnoreCase);

            if (!isStudyFilter)
            {
                continue;
            }

            foreach (string? value in values)
            {
                if (!string.Equals(value, studyInstanceUid, StringComparison.Ordinal))
                {
                    return false;
                }

                named = true;
            }
        }

        return named;
    }

    /// <summary>
    /// Counts redemptions of one link within a fixed window.
    /// </summary>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="tokenHash">The digest of the presented token.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when this attempt is over the limit.</returns>
    /// <remarks>
    /// <para>
    /// Deliberately the same shape as the download route's limiter, down to the
    /// window and the ceiling, but on its own key namespace: viewing and
    /// downloading are separate acts and one must not consume the other's
    /// allowance. Keyed on the digest, never on the token, because the key
    /// travels to Redis, appears in its keyspace and may be read by an operator.
    /// </para>
    /// <para>
    /// Counted only after the link has resolved and its permission has been
    /// accepted, so an unknown token costs nothing and cannot be used to fill
    /// somebody else's window.
    /// </para>
    /// <para>
    /// A read followed by a write, which is not atomic - two simultaneous
    /// requests can read the same count. Accepted rather than overlooked: the
    /// existing cache port offers get, set and remove, and an exact counter would
    /// need an atomic increment this application does not have. The ceiling can
    /// therefore be exceeded slightly under concurrency, which is tolerable for a
    /// limit whose job is to bound abuse of a leaked link rather than to stop a
    /// guess.
    /// </para>
    /// <para>
    /// Cache failures are not swallowed. If the cache cannot answer, the request
    /// fails rather than proceeding unlimited: a security control that quietly
    /// switches itself off is worse than one that is visibly unavailable.
    /// </para>
    /// </remarks>
    private static async Task<bool> IsRateLimitedAsync(
        ICacheStore cache,
        byte[] tokenHash,
        CancellationToken cancellationToken)
    {
        string key = $"cloudfilm:share:view:{Convert.ToHexString(tokenHash)}";

        ShareRedemptionCount? recorded = await cache
            .GetAsync<ShareRedemptionCount>(key, cancellationToken)
            .ConfigureAwait(false);

        int attempt = (recorded?.Count ?? 0) + 1;

        if (attempt > _maxRedemptionsPerWindow)
        {
            return true;
        }

        await cache
            .SetAsync(key, new ShareRedemptionCount(attempt), _rateLimitWindow, cancellationToken)
            .ConfigureAwait(false);

        return false;
    }

    /// <summary>
    /// The single answer to every refused redemption.
    /// </summary>
    private static IResult Refuse() => Results.NotFound();

    /// <summary>
    /// The single answer to every refused DICOMweb request.
    /// </summary>
    /// <remarks>
    /// 401 because <c>auth_request</c> accepts nothing else as a denial. What the
    /// browser sees is a 404, produced by nginx, and identical for a missing
    /// cookie, an expired session, a withdrawn link and a request for another
    /// patient's study.
    /// </remarks>
    private static IResult Deny() => Results.Unauthorized();

    /// <summary>
    /// Reports whether the path segment could be a token at all.
    /// </summary>
    private static bool LooksLikeToken(string? token)
    {
        if (token is null || token.Length != _encodedTokenLength)
        {
            return false;
        }

        foreach (char character in token)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))
            {
                return false;
            }
        }

        return true;
    }
}
