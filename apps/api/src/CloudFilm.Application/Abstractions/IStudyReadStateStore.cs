namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Reads and changes one user's read/unread mark on one study.
/// </summary>
/// <remarks>
/// <para>
/// Purpose-specific, like <see cref="IRecentPatientAccess"/> and for the same
/// reason. Every method takes the acting account as its first argument and there
/// is no overload that omits it, so there is no call - correct or mistaken -
/// that reads or writes a state belonging to somebody else. Nothing here accepts
/// a filter, a page size or a list of users, so this cannot grow into a general
/// query surface over the table.
/// </para>
/// <para>
/// The mutations report whether the state actually changed rather than returning
/// <see cref="Task"/>. That return value is what lets the caller record an audit
/// event for a real transition and stay silent for a repeat: a clinician
/// pressing the same button twice performed one state change, not two, and an
/// audit trail that says otherwise is describing HTTP traffic instead of
/// clinical activity.
/// </para>
/// <para>
/// Implementations must not open their own transaction and must not assume they
/// are the only writer in the unit of work. The caller composes the mutation and
/// its audit record into one transaction; see <see cref="IUnitOfWork"/>.
/// </para>
/// </remarks>
public interface IStudyReadStateStore
{
    /// <summary>
    /// Reports whether this user has marked this study read.
    /// </summary>
    /// <param name="actorUserId">The account asking, taken from the bearer token.</param>
    /// <param name="studyInstanceUid">The study, already trimmed and length checked.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when a mark exists for this pair.</returns>
    /// <remarks>
    /// A study nobody has marked is unread, so this answers <see langword="false"/>
    /// for an unknown study rather than failing. The store holds no opinion about
    /// whether the study exists - that is the archive's question, and asking it
    /// here would turn a private annotation into an existence oracle.
    /// </remarks>
    public Task<bool> IsReadAsync(
        Guid actorUserId,
        string studyInstanceUid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the study read for this user.
    /// </summary>
    /// <param name="actorUserId">The account acting, taken from the bearer token.</param>
    /// <param name="studyInstanceUid">The study, already trimmed and length checked.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when the study was previously unread and is now
    /// read; <see langword="false"/> when it was already read and nothing
    /// changed.
    /// </returns>
    /// <remarks>
    /// Idempotent. Calling it on an already-read study succeeds and reports no
    /// transition.
    /// </remarks>
    public Task<bool> MarkReadAsync(
        Guid actorUserId,
        string studyInstanceUid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the study unread for this user.
    /// </summary>
    /// <param name="actorUserId">The account acting, taken from the bearer token.</param>
    /// <param name="studyInstanceUid">The study, already trimmed and length checked.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when the study was previously read and is now
    /// unread; <see langword="false"/> when it was already unread.
    /// </returns>
    /// <remarks>Idempotent, for the same reason as <see cref="MarkReadAsync"/>.</remarks>
    public Task<bool> MarkUnreadAsync(
        Guid actorUserId,
        string studyInstanceUid,
        CancellationToken cancellationToken = default);
}
