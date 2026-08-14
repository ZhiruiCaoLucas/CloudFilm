using CloudFilm.Application.Abstractions;
using CloudFilm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudFilm.Infrastructure.ReadState;

/// <summary>
/// Stores read marks in PostgreSQL.
/// </summary>
/// <remarks>
/// <para>
/// Every operation is a single statement against the composite primary key, and
/// every one of them is safe to repeat. Nothing reads a row and then writes it
/// back, so there is no window between the two for a concurrent request to slip
/// into and no need for a version column, a row lock or a retry.
/// </para>
/// <para>
/// Shares the request's <see cref="CloudFilmDbContext"/>, like the audit writer,
/// so a mutation and its audit record travel on one connection and can be
/// enclosed in one transaction by the caller.
/// </para>
/// </remarks>
internal sealed class StudyReadStateStore(CloudFilmDbContext context, IClock clock) : IStudyReadStateStore
{
    private readonly CloudFilmDbContext _context = context;
    private readonly IClock _clock = clock;

    /// <inheritdoc />
    public Task<bool> IsReadAsync(
        Guid actorUserId,
        string studyInstanceUid,
        CancellationToken cancellationToken = default) =>
        _context.StudyReadStates
            .AsNoTracking()
            .AnyAsync(
                state => state.ActorUserId == actorUserId
                    && state.StudyInstanceUid == studyInstanceUid,
                cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// An upsert rather than "look, then insert". Two requests marking the same
    /// study at the same moment would both find nothing and both insert, and the
    /// loser would get a unique-violation that has to be caught and interpreted.
    /// <c>ON CONFLICT DO NOTHING</c> lets PostgreSQL settle it: exactly one
    /// request inserts a row, and the number of rows affected says which one
    /// that was - which is precisely the "did anything change" answer the caller
    /// needs in order to decide whether to write an audit event.
    /// </para>
    /// <para>
    /// Raw SQL because EF Core has no upsert. The values are parameters, not
    /// interpolated text: the interpolated-string overload sends
    /// <paramref name="actorUserId"/> and <paramref name="studyInstanceUid"/> to
    /// the server as bound parameters. The schema and table names are the only
    /// literal parts, and they must be kept in step with
    /// <see cref="CloudFilmDbContext.Schema"/> and
    /// <c>StudyReadStateConfiguration</c>.
    /// </para>
    /// </remarks>
    public async Task<bool> MarkReadAsync(
        Guid actorUserId,
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset markedAt = _clock.UtcNow;

        int affected = await _context.Database
            .ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO cloudfilm."StudyReadStates" ("ActorUserId", "StudyInstanceUid", "MarkedReadAtUtc")
                VALUES ({actorUserId}, {studyInstanceUid}, {markedAt})
                ON CONFLICT ("ActorUserId", "StudyInstanceUid") DO NOTHING
                """,
                cancellationToken)
            .ConfigureAwait(false);

        return affected > 0;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A keyed delete, which is idempotent by nature: deleting a row that is not
    /// there affects nothing and reports no transition. <c>ExecuteDeleteAsync</c>
    /// issues one statement and returns the row count without loading the entity,
    /// and it joins whatever transaction is open on the context.
    /// </remarks>
    public async Task<bool> MarkUnreadAsync(
        Guid actorUserId,
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        int affected = await _context.StudyReadStates
            .Where(state => state.ActorUserId == actorUserId
                && state.StudyInstanceUid == studyInstanceUid)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return affected > 0;
    }
}
