using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Sharing;
using CloudFilm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudFilm.Infrastructure.Sharing;

/// <summary>
/// Keeps viewer sessions in the distributed cache.
/// </summary>
/// <remarks>
/// <para>
/// The identifier is minted the same way a share token is - 32 bytes from a
/// cryptographic source, Base64Url - because it is the same kind of thing: a
/// bearer value that admits its holder to a study. It is stored in the cache as
/// the key and never written to the database.
/// </para>
/// <para>
/// The cache's mandatory expiry is doing real work here. A session cannot
/// outlive its lifetime even if nothing ever deletes it, which is the property
/// that makes a short-lived credential safe to hand to a browser.
/// </para>
/// </remarks>
internal sealed class ViewerSessionStore(ICacheStore cache, IClock clock) : IViewerSessionStore
{
    private readonly ICacheStore _cache = cache;
    private readonly IClock _clock = clock;

    /// <inheritdoc />
    public async Task<ViewerSession> CreateAsync(
        string studyInstanceUid,
        string permission,
        Guid? shareLinkId,
        CancellationToken cancellationToken = default)
    {
        string sessionId = ShareTokens.Create();

        await _cache
            .SetAsync(
                KeyFor(sessionId),
                new ViewerSessionState(studyInstanceUid, permission, shareLinkId),
                ViewerSessions.Lifetime,
                cancellationToken)
            .ConfigureAwait(false);

        return new ViewerSession(
            sessionId,
            studyInstanceUid,
            permission,
            _clock.UtcNow + ViewerSessions.Lifetime);
    }

    /// <inheritdoc />
    public Task<ViewerSessionState?> FindAsync(
        string sessionId,
        CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(sessionId)
            ? Task.FromResult<ViewerSessionState?>(null)
            : _cache.GetAsync<ViewerSessionState>(KeyFor(sessionId), cancellationToken);

    /// <summary>
    /// The cache key for one session.
    /// </summary>
    /// <remarks>
    /// The identifier is the key. It is opaque and unguessable, so nothing is
    /// gained by hashing it again, and keeping it whole means an operator reading
    /// the keyspace sees session identifiers rather than study identifiers.
    /// </remarks>
    private static string KeyFor(string sessionId) => $"cloudfilm:viewer-session:{sessionId}";
}

/// <summary>
/// Answers whether a share link still stands, straight from the table.
/// </summary>
/// <remarks>
/// Read-only and deliberately separate from <see cref="ShareLinkStore"/>: this is
/// consulted on the imaging gateway's path, once per DICOMweb request, and it
/// must never be able to change anything.
/// </remarks>
internal sealed class ShareLinkStatusQuery(CloudFilmDbContext context, IClock clock) : IShareLinkStatus
{
    private readonly CloudFilmDbContext _context = context;
    private readonly IClock _clock = clock;

    /// <inheritdoc />
    public Task<bool> IsActiveAsync(Guid shareLinkId, CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = _clock.UtcNow;

        return _context.ShareLinks
            .AsNoTracking()
            .AnyAsync(
                link => link.Id == shareLinkId
                    && link.RevokedAtUtc == null
                    && link.ExpiresAtUtc > now,
                cancellationToken);
    }
}
