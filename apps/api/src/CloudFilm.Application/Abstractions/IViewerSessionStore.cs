using CloudFilm.Application.Sharing;

namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Issues and resolves viewer sessions.
/// </summary>
/// <remarks>
/// <para>
/// Sessions live in the distributed cache rather than the database. They are
/// short-lived, they are created on every workspace load, and they carry nothing
/// worth keeping - a store with mandatory expiry is exactly the right shape, and
/// it means a session cannot outlive its own time to live even if something
/// forgets to clean up.
/// </para>
/// <para>
/// The identifier is generated here and is opaque. It is not derived from the
/// study, the share link or the account, so holding one tells its holder nothing
/// and guessing one is as hard as guessing a share token.
/// </para>
/// </remarks>
public interface IViewerSessionStore
{
    /// <summary>
    /// Issues a session for one study.
    /// </summary>
    /// <param name="studyInstanceUid">The study the session admits.</param>
    /// <param name="permission">What the session permits, from <c>GrantPermissions</c>.</param>
    /// <param name="shareLinkId">
    /// The share link this came from, or <see langword="null"/> for a signed-in
    /// clinician.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The session, including the identifier to put in a cookie.</returns>
    public Task<ViewerSession> CreateAsync(
        string studyInstanceUid,
        string permission,
        Guid? shareLinkId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads what a session is bound to.
    /// </summary>
    /// <param name="sessionId">The identifier from the cookie.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// What the session admits, or <see langword="null"/> when there is no such
    /// session or it has expired.
    /// </returns>
    public Task<ViewerSessionState?> FindAsync(
        string sessionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Answers whether a share link still stands.
/// </summary>
/// <remarks>
/// A read-only companion to the share link store, kept separate because it is
/// consulted on a different path and at a very different rate: the imaging
/// gateway asks it on every DICOMweb request a viewer makes, which is dozens to
/// hundreds per study. One indexed lookup by primary key is what that path can
/// afford, and it is all this needs to do.
/// </remarks>
public interface IShareLinkStatus
{
    /// <summary>
    /// Reports whether a share link is still live.
    /// </summary>
    /// <param name="shareLinkId">The link to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> only when the link exists, has not been withdrawn
    /// and has not expired.
    /// </returns>
    /// <remarks>
    /// Asked on every request rather than once per session, so that withdrawing a
    /// link stops the next image rather than the next session. That is the
    /// difference between revocation and a fifteen minute delay.
    /// </remarks>
    public Task<bool> IsActiveAsync(Guid shareLinkId, CancellationToken cancellationToken = default);
}
