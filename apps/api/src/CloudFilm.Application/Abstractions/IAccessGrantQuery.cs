using CloudFilm.Application.Grants;

namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Reads access grants.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="IAccessGrantStore"/>, which writes them. The two
/// have different callers and different risks: writing is an administrative act
/// that must be audited and transactional, while reading answers "may this
/// person reach this study" on an ordinary request and must stay cheap. Keeping
/// them apart also means the read side can be handed to code that has no
/// business granting anything.
/// </para>
/// <para>
/// Every method reports the present, not the record: expired and revoked grants
/// are excluded by the query rather than returned for a caller to filter. A
/// caller that has to remember to check an expiry is a caller that will forget,
/// and forgetting here means showing a patient's imaging to somebody whose
/// access ended.
/// </para>
/// </remarks>
public interface IAccessGrantQuery
{
    /// <summary>
    /// Reports whether an account may currently do something with a study.
    /// </summary>
    /// <param name="granteeUserId">The account being asked about.</param>
    /// <param name="studyInstanceUid">The study.</param>
    /// <param name="requiredPermission">One of the <see cref="GrantPermissions"/> values.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when a live grant covers the requirement.
    /// </returns>
    /// <remarks>
    /// Applies the permission ladder: a <see cref="GrantPermissions.Download"/>
    /// grant answers <see langword="true"/> for
    /// <see cref="GrantPermissions.View"/>, and not the other way round. The
    /// ladder lives in <see cref="GrantPermissions.Satisfies"/>; this only asks.
    /// </remarks>
    public Task<bool> HasAccessAsync(
        Guid granteeUserId,
        string studyInstanceUid,
        string requiredPermission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the grants currently in force on a study.
    /// </summary>
    /// <param name="studyInstanceUid">The study.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The live grants, oldest first. Empty when nobody has been granted access.
    /// </returns>
    /// <remarks>
    /// Live only. Revoked and expired grants stay in the table as the record of
    /// who could once see what, but they are not access and do not appear here.
    /// </remarks>
    public Task<IReadOnlyList<StudyAccessGrant>> ListForStudyAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the grant an account currently holds on a study.
    /// </summary>
    /// <param name="granteeUserId">The account.</param>
    /// <param name="studyInstanceUid">The study.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The live grant, or <see langword="null"/> when there is none.</returns>
    /// <remarks>
    /// Exists so a caller that has just created a grant can report what was
    /// created without the writing side having to return it - the write answers
    /// whether anything changed, and this answers what now stands.
    /// </remarks>
    public Task<StudyAccessGrant?> FindActiveAsync(
        Guid granteeUserId,
        string studyInstanceUid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one grant by identifier, whatever state it is in.
    /// </summary>
    /// <param name="grantId">The grant's identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The grant, or <see langword="null"/> when no such record exists.</returns>
    /// <remarks>
    /// The one method that will return a revoked or expired grant, because a
    /// revocation names a record rather than a permission: the caller has to be
    /// able to tell "no such grant" from "already revoked", and it needs the
    /// study the grant was for in order to record what it did.
    /// </remarks>
    public Task<StudyAccessGrant?> FindAsync(
        Guid grantId,
        CancellationToken cancellationToken = default);
}
