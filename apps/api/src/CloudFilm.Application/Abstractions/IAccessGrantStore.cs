namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Records and answers who may reach a study they do not otherwise have access
/// to.
/// </summary>
/// <remarks>
/// <para>
/// Purpose-specific, like <see cref="IStudyReadStateStore"/> and
/// <see cref="IRecentPatientAccess"/>. Every method names both the account and
/// the study, and there is no overload that omits either, so there is no call -
/// correct or mistaken - that reads or writes a grant belonging to somebody
/// else. Nothing here accepts a filter, a page size or a list of accounts, so it
/// cannot grow into a general query surface over the table.
/// </para>
/// <para>
/// Grants are never carried in a token. A token is issued once and cannot be
/// withdrawn before it expires; a grant can be revoked at any moment and the
/// next request must respect that. So every answer here comes from the database,
/// and expiry and revocation are conditions of the query rather than something a
/// caller is trusted to check afterwards.
/// </para>
/// <para>
/// This layer stores and reports grants. It does not decide what a grant is
/// worth: no endpoint's behaviour changes because this exists, and nothing here
/// grants anything by default.
/// </para>
/// </remarks>
public interface IAccessGrantStore
{
    /// <summary>
    /// Reports whether an account currently holds a permission on a study.
    /// </summary>
    /// <param name="granteeUserId">The account being asked about.</param>
    /// <param name="studyInstanceUid">The study, already trimmed and length checked.</param>
    /// <param name="permission">One of the <c>GrantPermissions</c> values.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> only when a matching grant exists that has been
    /// neither revoked nor expired.
    /// </returns>
    /// <remarks>
    /// "Currently" is load-bearing: an expired or revoked grant answers
    /// <see langword="false"/> here rather than being returned and filtered by
    /// the caller. A caller that has to remember to check an expiry is a caller
    /// that will forget.
    /// </remarks>
    public Task<bool> IsGrantedAsync(
        Guid granteeUserId,
        string studyInstanceUid,
        string permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants an account a permission on a study.
    /// </summary>
    /// <param name="granteeUserId">The account receiving the grant.</param>
    /// <param name="studyInstanceUid">The study, already trimmed and length checked.</param>
    /// <param name="permission">One of the <c>GrantPermissions</c> values.</param>
    /// <param name="grantedByUserId">The account making the grant.</param>
    /// <param name="expiresAtUtc">
    /// When the grant stops applying, or <see langword="null"/> for no expiry.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when a grant was created; <see langword="false"/>
    /// when the account already holds an active grant on that study and nothing
    /// changed.
    /// </returns>
    /// <remarks>
    /// One active grant per account per study, whatever its permission - that is
    /// what the database enforces, so changing a permission is a revoke followed
    /// by a grant rather than an update. The alternative, silently rewriting an
    /// existing grant, would let a narrowing look identical to a widening in
    /// both the table and the audit trail.
    /// </remarks>
    public Task<bool> GrantAsync(
        Guid granteeUserId,
        string studyInstanceUid,
        string permission,
        Guid grantedByUserId,
        DateTimeOffset? expiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws an account's active grant on a study.
    /// </summary>
    /// <param name="granteeUserId">The account losing the grant.</param>
    /// <param name="studyInstanceUid">The study, already trimmed and length checked.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when an active grant was withdrawn;
    /// <see langword="false"/> when there was none.
    /// </returns>
    /// <remarks>
    /// Marks the row revoked rather than deleting it. Who was given access to a
    /// patient's imaging, by whom, and until when, is exactly the question an
    /// investigation asks afterwards - and a deleted row answers nothing.
    /// </remarks>
    public Task<bool> RevokeAsync(
        Guid granteeUserId,
        string studyInstanceUid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws one grant by identifier.
    /// </summary>
    /// <param name="grantId">The grant to withdraw.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when a standing grant was withdrawn;
    /// <see langword="false"/> when there is no such grant or it was already
    /// revoked.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A revocation names the record it withdraws, not the pair it happens to
    /// be about. The same account may have been granted access to the same study
    /// several times over, and only the row somebody pointed at should be
    /// touched.
    /// </para>
    /// <para>
    /// Marks the row revoked rather than deleting it, like its sibling, and
    /// reports whether anything changed so a caller can record a transition
    /// rather than a request.
    /// </para>
    /// </remarks>
    public Task<bool> RevokeByIdAsync(
        Guid grantId,
        CancellationToken cancellationToken = default);
}
