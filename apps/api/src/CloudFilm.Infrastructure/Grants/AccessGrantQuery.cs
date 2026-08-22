using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Grants;
using CloudFilm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudFilm.Infrastructure.Grants;

/// <summary>
/// Reads access grants out of PostgreSQL.
/// </summary>
/// <remarks>
/// <para>
/// The read half of the grant table. It shares the request's
/// <see cref="CloudFilmDbContext"/> with the writing half and the audit writer,
/// so a read taken inside a transaction sees that transaction's writes.
/// </para>
/// <para>
/// Every query but <see cref="FindAsync"/> filters to live grants in the
/// database. Expired and revoked rows stay in the table as the record of who
/// could once see what; they are not access, and they never leave this class as
/// though they were.
/// </para>
/// </remarks>
internal sealed class AccessGrantQuery(CloudFilmDbContext context, IClock clock) : IAccessGrantQuery
{
    private readonly CloudFilmDbContext _context = context;
    private readonly IClock _clock = clock;

    /// <summary>
    /// The grants that stand for one study at one instant.
    /// </summary>
    /// <param name="grants">The set to query.</param>
    /// <param name="studyInstanceUid">The study.</param>
    /// <param name="asOfUtc">The instant "standing" is judged against.</param>
    /// <returns>Matching grants that are neither revoked nor expired.</returns>
    /// <remarks>
    /// A static returning <see cref="IQueryable{T}"/> for the same reason
    /// <see cref="AccessGrantStore.BuildActiveQuery"/> is one: the predicate can
    /// then be exercised against an in-memory sequence, so a filter deleted here
    /// fails a test rather than widening who appears to have access. Expiry is
    /// strictly in the future - a grant expiring exactly now has expired.
    /// </remarks>
    public static IQueryable<AccessGrant> BuildStudyQuery(
        IQueryable<AccessGrant> grants,
        string studyInstanceUid,
        DateTimeOffset asOfUtc) =>
        grants.Where(grant =>
            grant.StudyInstanceUid == studyInstanceUid
            && grant.RevokedAtUtc == null
            && (grant.ExpiresAtUtc == null || grant.ExpiresAtUtc > asOfUtc));

    /// <inheritdoc />
    public async Task<bool> HasAccessAsync(
        Guid granteeUserId,
        string studyInstanceUid,
        string requiredPermission,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> sufficient = GrantPermissions.Satisfying(requiredPermission);

        if (sufficient.Count == 0)
        {
            // A requirement this application does not define is satisfied by
            // nothing. Answering "true" for an unrecognised permission would be
            // the worst possible failure mode for this method.
            return false;
        }

        return await BuildActiveGrants()
            .Where(grant => grant.GranteeUserId == granteeUserId
                && grant.StudyInstanceUid == studyInstanceUid
                && sufficient.Contains(grant.Permission))
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudyAccessGrant>> ListForStudyAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        List<AccessGrant> grants = await BuildStudyQuery(
                _context.AccessGrants.AsNoTracking(),
                studyInstanceUid,
                _clock.UtcNow)

            // Oldest first, so the list reads as the order access was opened up.
            .OrderBy(grant => grant.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return grants.Select(Project).ToList();
    }

    /// <inheritdoc />
    public async Task<StudyAccessGrant?> FindActiveAsync(
        Guid granteeUserId,
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        AccessGrant? grant = await BuildActiveGrants()
            .FirstOrDefaultAsync(
                candidate => candidate.GranteeUserId == granteeUserId
                    && candidate.StudyInstanceUid == studyInstanceUid,
                cancellationToken)
            .ConfigureAwait(false);

        return grant is null ? null : Project(grant);
    }

    /// <inheritdoc />
    public async Task<StudyAccessGrant?> FindAsync(
        Guid grantId,
        CancellationToken cancellationToken = default)
    {
        AccessGrant? grant = await _context.AccessGrants
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == grantId, cancellationToken)
            .ConfigureAwait(false);

        return grant is null ? null : Project(grant);
    }

    private IQueryable<AccessGrant> BuildActiveGrants()
    {
        DateTimeOffset now = _clock.UtcNow;

        return _context.AccessGrants
            .AsNoTracking()
            .Where(grant => grant.RevokedAtUtc == null
                && (grant.ExpiresAtUtc == null || grant.ExpiresAtUtc > now));
    }

    /// <summary>
    /// Turns a stored row into what the API reports.
    /// </summary>
    /// <remarks>
    /// The projection is where <c>GrantedByUserId</c> stops. Who opened a study
    /// up to somebody else is recorded for an investigation to read out of the
    /// table; it is not part of the answer to "who may see this study", and an
    /// administrative list is not the place to publish it.
    /// </remarks>
    private static StudyAccessGrant Project(AccessGrant grant) => new(
        grant.Id,
        grant.StudyInstanceUid,
        grant.GranteeUserId,
        grant.Permission,
        grant.CreatedAtUtc,
        grant.ExpiresAtUtc,
        grant.RevokedAtUtc);
}
