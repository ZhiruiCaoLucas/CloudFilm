using CloudFilm.Application.Abstractions;
using CloudFilm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudFilm.Infrastructure.Grants;

/// <summary>
/// Stores access grants in PostgreSQL.
/// </summary>
/// <remarks>
/// <para>
/// Shares the request's <see cref="CloudFilmDbContext"/>, like the audit writer
/// and the read-state store, so a grant and the audit record of it travel on one
/// connection and can be enclosed in one transaction by the caller. Nothing here
/// opens a transaction of its own.
/// </para>
/// <para>
/// Expiry and revocation are conditions of the query rather than checks a caller
/// performs on the result. That is the difference between "this account has a
/// grant" and "this account may reach this study now", and only the second one
/// is a safe thing to answer.
/// </para>
/// </remarks>
internal sealed class AccessGrantStore(CloudFilmDbContext context, IClock clock) : IAccessGrantStore
{
    private readonly CloudFilmDbContext _context = context;
    private readonly IClock _clock = clock;

    /// <summary>
    /// The grants that are live for one account, one study and one permission.
    /// </summary>
    /// <param name="grants">The set to query.</param>
    /// <param name="granteeUserId">The account being asked about.</param>
    /// <param name="studyInstanceUid">The study.</param>
    /// <param name="permission">The permission being asked about.</param>
    /// <param name="asOfUtc">The instant "live" is judged against.</param>
    /// <returns>Matching grants that are neither revoked nor expired.</returns>
    /// <remarks>
    /// <para>
    /// A static returning <see cref="IQueryable{T}"/> so the predicate can be
    /// exercised against an in-memory sequence: the same expression tree that
    /// ships to PostgreSQL is the one a test runs, so a filter deleted here fails
    /// a test rather than quietly widening access. The same shape
    /// <c>RecentPatientAccessQuery</c> uses.
    /// </para>
    /// <para>
    /// The instant is a parameter rather than read from the clock inside, so a
    /// test can stand either side of an expiry without waiting for one, and so
    /// one request judges every row against a single moment.
    /// </para>
    /// <para>
    /// Expiry is strictly in the future: a grant whose expiry is exactly now has
    /// expired. An off-by-one at a boundary should fall closed.
    /// </para>
    /// </remarks>
    public static IQueryable<AccessGrant> BuildActiveQuery(
        IQueryable<AccessGrant> grants,
        Guid granteeUserId,
        string studyInstanceUid,
        string permission,
        DateTimeOffset asOfUtc) =>
        grants.Where(grant =>
            grant.GranteeUserId == granteeUserId
            && grant.StudyInstanceUid == studyInstanceUid
            && grant.Permission == permission
            && grant.RevokedAtUtc == null
            && (grant.ExpiresAtUtc == null || grant.ExpiresAtUtc > asOfUtc));

    /// <inheritdoc />
    public Task<bool> IsGrantedAsync(
        Guid granteeUserId,
        string studyInstanceUid,
        string permission,
        CancellationToken cancellationToken = default) =>
        BuildActiveQuery(
                _context.AccessGrants.AsNoTracking(),
                granteeUserId,
                studyInstanceUid,
                permission,
                _clock.UtcNow)
            .AnyAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Looks before it writes, and the database is what makes that safe rather
    /// than the look: two requests granting the same pair at the same moment
    /// both find nothing, both insert, and the partial unique index rejects the
    /// loser. The check turns the ordinary case into a quiet no-op; the index
    /// turns the race into an error rather than a second live grant.
    /// </para>
    /// <para>
    /// An unrevoked row whose expiry has passed still occupies the slot the
    /// index reserves - the index cannot compare a timestamp to "now", so it
    /// keeps one unrevoked row per pair regardless of whether that row still
    /// permits anything. Such a row is retired here, in the same unit of work as
    /// the new grant, rather than left to block a legitimate re-grant. Nothing
    /// is deleted: the expired grant keeps its dates and gains a revocation
    /// instant, so the history still reads as what happened.
    /// </para>
    /// </remarks>
    public async Task<bool> GrantAsync(
        Guid granteeUserId,
        string studyInstanceUid,
        string permission,
        Guid grantedByUserId,
        DateTimeOffset? expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = _clock.UtcNow;

        AccessGrant? standing = await _context.AccessGrants
            .FirstOrDefaultAsync(
                grant => grant.GranteeUserId == granteeUserId
                    && grant.StudyInstanceUid == studyInstanceUid
                    && grant.RevokedAtUtc == null,
                cancellationToken)
            .ConfigureAwait(false);

        if (standing is not null)
        {
            bool stillLive = standing.ExpiresAtUtc is null || standing.ExpiresAtUtc > now;

            if (stillLive)
            {
                return false;
            }

            standing.RevokedAtUtc = now;
        }

        AccessGrant created = new()
        {
            Id = Guid.NewGuid(),
            StudyInstanceUid = studyInstanceUid,
            GranteeUserId = granteeUserId,
            GrantedByUserId = grantedByUserId,
            Permission = permission,
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc,
            RevokedAtUtc = null,
        };

        await _context.AccessGrants.AddAsync(created, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Stamps the row rather than deleting it, and only ever the live one - a
    /// grant that was already revoked keeps the instant it was revoked at, so
    /// the history reads as what happened rather than as when somebody last
    /// pressed the button.
    /// </remarks>
    public async Task<bool> RevokeAsync(
        Guid granteeUserId,
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        AccessGrant? active = await _context.AccessGrants
            .FirstOrDefaultAsync(
                grant => grant.GranteeUserId == granteeUserId
                    && grant.StudyInstanceUid == studyInstanceUid
                    && grant.RevokedAtUtc == null,
                cancellationToken)
            .ConfigureAwait(false);

        if (active is null)
        {
            return false;
        }

        active.RevokedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Loads the row rather than issuing a keyed update, because the caller has
    /// to be able to tell "no such grant" from "already revoked" and both answer
    /// <see langword="false"/> here. An already-revoked grant keeps the instant
    /// it was revoked at: the history reads as what happened, not as when
    /// somebody last pressed the button.
    /// </remarks>
    public async Task<bool> RevokeByIdAsync(
        Guid grantId,
        CancellationToken cancellationToken = default)
    {
        AccessGrant? grant = await _context.AccessGrants
            .FirstOrDefaultAsync(candidate => candidate.Id == grantId, cancellationToken)
            .ConfigureAwait(false);

        if (grant is null || grant.RevokedAtUtc is not null)
        {
            return false;
        }

        grant.RevokedAtUtc = _clock.UtcNow;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
