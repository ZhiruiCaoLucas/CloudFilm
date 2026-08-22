using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auditing;
using CloudFilm.Application.Grants;
using CloudFilm.Application.Imaging;
using CloudFilm.Application.Sharing;
using CloudFilm.Infrastructure.Sharing;

namespace CloudFilm.Api.Endpoints;

/// <summary>
/// Redeeming a share link.
/// </summary>
/// <remarks>
/// <para>
/// The only anonymous route in CloudFilm that returns patient data, and the
/// token in its path is the whole of its authority. There is no account behind
/// the caller, so every question this endpoint asks - is the token real, has it
/// expired, was it withdrawn, does it permit this - is answered from the token
/// alone, and answered before the imaging tier is touched.
/// </para>
/// <para>
/// The study is never taken from the request. It comes from the share link the
/// token resolves to, so a holder of one link cannot ask for another study by
/// editing a URL: there is no parameter to edit.
/// </para>
/// <para>
/// Every refusal is the same 404 with the same body. A token that never existed,
/// one that expired, one that was withdrawn, one that permits only viewing and
/// one whose study the archive no longer holds are externally identical -
/// otherwise the endpoint would answer questions about links and studies to
/// anyone willing to try URLs.
/// </para>
/// <para>
/// This protects the download path and nothing else. The viewer reaches images
/// over a route that does not pass through this API, and a share link neither
/// opens nor closes it. See <c>SECURITY.md</c>.
/// </para>
/// </remarks>
internal static class ShareAccessEndpoints
{
    /// <summary>
    /// How many times one link may be redeemed inside <see cref="_rateLimitWindow"/>.
    /// </summary>
    /// <remarks>
    /// Generous, because this is not what stops a token being guessed - 256 bits
    /// of randomness is. It bounds what a leaked link can be used for before
    /// somebody notices and revokes it, and it stops one URL being turned into a
    /// download service.
    /// </remarks>
    private const int _maxRedemptionsPerWindow = 20;

    private static readonly TimeSpan _rateLimitWindow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The length of a token in its encoded form.
    /// </summary>
    /// <remarks>
    /// 32 random bytes in Base64Url without padding is 43 characters. Checking it
    /// first means an obviously malformed path costs neither a database query nor
    /// a cache round trip - and the caller cannot tell the difference, because the
    /// answer is the same 404 either way.
    /// </remarks>
    private const int _encodedTokenLength = 43;

    /// <summary>
    /// Maps the share redemption endpoints.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapShareAccessEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/share/{token}/download", async Task<IResult> (
                    string token,
                    HttpContext context,
                    IShareLinkStore shareLinks,
                    IStudyDownloadSource downloads,
                    IAuditLogService auditLog,
                    IUnitOfWork unitOfWork,
                    ICacheStore cache,
                    CancellationToken cancellationToken) =>
            {
                // Cheapest check first, and indistinguishable from every other
                // refusal.
                if (!LooksLikeToken(token))
                {
                    return Refuse();
                }

                byte[] tokenHash = ShareTokens.Hash(token);

                // Unknown, expired and withdrawn all resolve to null. The store
                // makes them one answer, so this endpoint has no way to tell them
                // apart even if a future edit wanted to.
                ShareLinkTarget? target = await shareLinks
                    .FindActiveAsync(tokenHash, cancellationToken)
                    .ConfigureAwait(false);

                if (target is null)
                {
                    return Refuse();
                }

                // Download, or something that covers it. A view-only link is
                // refused exactly like an unknown one.
                if (!GrantPermissions.Satisfies(target.Permission, GrantPermissions.Download))
                {
                    return Refuse();
                }

                if (await IsRateLimitedAsync(cache, tokenHash, cancellationToken).ConfigureAwait(false))
                {
                    // Same answer as an invalid token: telling a caller they have
                    // been throttled tells them the token was real.
                    return Refuse();
                }

                // Authorisation is complete. Only now does anything reach the
                // imaging tier, and only for the study the link names.
                StudyDownload? download = await downloads
                    .OpenStudyAsync(target.StudyInstanceUid, cancellationToken)
                    .ConfigureAwait(false);

                if (download is null)
                {
                    return Refuse();
                }

                // Tied to the response, not disposed here: the body is still
                // arriving when this method returns.
                context.Response.RegisterForDisposeAsync(download);

                try
                {
                    // Before a byte is written. The response has not started, so
                    // a failure here becomes a 500 and the study stays inside.
                    await unitOfWork
                        .ExecuteAsync(
                            async operationToken =>
                            {
                                await auditLog
                                    .RecordAsync(
                                        new AuditEntry(
                                            // No account. A link is redeemed by
                                            // whoever holds it, and inventing an
                                            // actor would be a false entry.
                                            ActorUserId: null,
                                            AuditAction.DownloadStudy,
                                            AuditResourceType.Study,
                                            target.StudyInstanceUid),
                                        operationToken)
                                    .ConfigureAwait(false);

                                return true;
                            },
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // The stream is closed immediately rather than left to the
                    // response, so an unrecorded retrieval does not leave a
                    // connection open to the archive.
                    await download.DisposeAsync().ConfigureAwait(false);

                    throw;
                }

                // A constant filename. Anything derived from the study would put
                // a patient's name, record number or accession into the recipient's
                // downloads folder and into every log that records a filename.
                return Results.Stream(
                    download.Content,
                    download.ContentType,
                    fileDownloadName: "study.dcm");
            })
            .WithName("DownloadSharedStudy")
            .WithTags("Sharing")
            .WithSummary("Streams the study a share link leads to.")

            // The one route in CloudFilm that must be reachable without a token
            // of the other kind. Declared explicitly, because the fallback policy
            // would otherwise close it.
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK, contentType: "multipart/related")
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>
    /// The single answer to every refusal.
    /// </summary>
    /// <remarks>
    /// No body, no problem details, no correlation id: an empty 404 is the least
    /// that can be said, and everything else could be compared between attempts.
    /// </remarks>
    private static IResult Refuse() => Results.NotFound();

    /// <summary>
    /// Reports whether the path segment could be a token at all.
    /// </summary>
    /// <remarks>
    /// Shape only - length and alphabet. It is not a security check and does not
    /// need to be; it exists so that a scan of random URLs costs a string
    /// comparison rather than a database query.
    /// </remarks>
    private static bool LooksLikeToken(string? token)
    {
        if (token is null || token.Length != _encodedTokenLength)
        {
            return false;
        }

        foreach (char character in token)
        {
            bool allowed = char.IsAsciiLetterOrDigit(character) || character is '-' or '_';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
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
    /// Keyed on the digest, never the token: the key travels to Redis, appears in
    /// its keyspace and may be dumped by an operator, and none of those should
    /// yield a working credential.
    /// </para>
    /// <para>
    /// A read followed by a write, which is not atomic - two simultaneous
    /// requests can both read the same count. That is accepted rather than
    /// overlooked: the existing cache port offers get, set and remove, and an
    /// exact counter would need an atomic increment this application does not
    /// have. The consequence is that the ceiling can be exceeded slightly under
    /// concurrency, which is tolerable for a limit whose job is to bound abuse of
    /// a leaked link rather than to stop a guess.
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
        string key = $"cloudfilm:share:download:{Convert.ToHexString(tokenHash)}";

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
}

/// <summary>
/// How many times a link has been redeemed in the current window.
/// </summary>
/// <param name="Count">The number of redemptions counted so far.</param>
/// <remarks>
/// A class rather than a bare integer because the cache port stores reference
/// types. It holds a count and nothing identifying: the link it belongs to is in
/// the key, as a digest.
/// </remarks>
internal sealed record ShareRedemptionCount(int Count);
