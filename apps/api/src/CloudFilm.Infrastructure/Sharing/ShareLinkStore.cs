using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Sharing;
using CloudFilm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudFilm.Infrastructure.Sharing;

/// <summary>
/// Stores share links in PostgreSQL.
/// </summary>
/// <remarks>
/// <para>
/// Shares the request's <see cref="CloudFilmDbContext"/> with the audit writer,
/// so issuing or withdrawing a link and the record of it can be enclosed in one
/// transaction by the caller. Nothing here opens a transaction of its own.
/// </para>
/// <para>
/// Only digests are handled. No method takes or returns a token, so no code path
/// through this class can log one, and a caller that wanted to would have to go
/// and fetch it from somewhere that does not exist.
/// </para>
/// </remarks>
internal sealed class ShareLinkStore(CloudFilmDbContext context, IClock clock) : IShareLinkStore
{
    private readonly CloudFilmDbContext _context = context;
    private readonly IClock _clock = clock;

    /// <summary>
    /// The links that a digest resolves to at one instant.
    /// </summary>
    /// <param name="links">The set to query.</param>
    /// <param name="tokenHash">The digest presented by the caller.</param>
    /// <param name="asOfUtc">The instant "live" is judged against.</param>
    /// <returns>The matching link, if it is neither expired nor revoked.</returns>
    /// <remarks>
    /// <para>
    /// A static returning <see cref="IQueryable{T}"/> for the same reason
    /// <c>AccessGrantStore.BuildActiveQuery</c> is one: the predicate can be
    /// exercised against an in-memory sequence, so a filter deleted here fails a
    /// test rather than quietly turning an expired link back on.
    /// </para>
    /// <para>
    /// Expiry is strictly in the future - a link expiring exactly now has
    /// expired. A boundary in a bearer credential should fall closed.
    /// </para>
    /// </remarks>
    public static IQueryable<ShareLink> BuildActiveQuery(
        IQueryable<ShareLink> links,
        byte[] tokenHash,
        DateTimeOffset asOfUtc) =>
        links.Where(link =>
            link.TokenHash == tokenHash
            && link.RevokedAtUtc == null
            && link.ExpiresAtUtc > asOfUtc);

    /// <inheritdoc />
    public async Task<ShareLinkSummary> CreateAsync(
        string studyInstanceUid,
        string permission,
        byte[] tokenHash,
        Guid createdByUserId,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        ShareLink link = new()
        {
            Id = Guid.NewGuid(),
            StudyInstanceUid = studyInstanceUid,
            Permission = permission,
            TokenHash = tokenHash,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = _clock.UtcNow,
            ExpiresAtUtc = expiresAtUtc,
            RevokedAtUtc = null,
        };

        await _context.ShareLinks.AddAsync(link, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Project(link);
    }

    /// <inheritdoc />
    public async Task<ShareLinkTarget?> FindActiveAsync(
        byte[] tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        ShareLink? link = await BuildActiveQuery(
                _context.ShareLinks.AsNoTracking(),
                tokenHash,
                _clock.UtcNow)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return link is null
            ? null
            : new ShareLinkTarget(link.Id, link.StudyInstanceUid, link.Permission);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Loads the row rather than issuing a keyed update, because the caller has
    /// to tell "no such link" from "already withdrawn" and because the answer
    /// has to carry the study the link was for. The snapshot is taken before the
    /// stamp is applied, so a caller can see whether it won the transition. A
    /// link already revoked keeps the instant it was revoked at - the history
    /// reads as what happened, not as when somebody last pressed the button.
    /// </remarks>
    public async Task<ShareLinkSummary?> RevokeAsync(
        Guid shareLinkId,
        CancellationToken cancellationToken = default)
    {
        ShareLink? link = await _context.ShareLinks
            .FirstOrDefaultAsync(candidate => candidate.Id == shareLinkId, cancellationToken)
            .ConfigureAwait(false);

        if (link is null)
        {
            return null;
        }

        ShareLinkSummary before = Project(link);

        if (before.RevokedAtUtc is not null)
        {
            return before;
        }

        link.RevokedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return before;
    }

    /// <summary>
    /// Turns a stored row into what the application reports.
    /// </summary>
    /// <remarks>
    /// The projection is where the digest stops. Nothing above this layer needs
    /// it, and a shape that cannot carry it cannot leak it into a log or a
    /// response.
    /// </remarks>
    private static ShareLinkSummary Project(ShareLink link) => new(
        link.Id,
        link.StudyInstanceUid,
        link.Permission,
        link.CreatedAtUtc,
        link.ExpiresAtUtc,
        link.RevokedAtUtc);
}
