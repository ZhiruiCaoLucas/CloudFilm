using CloudFilm.Application.Sharing;

namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Stores and resolves share links.
/// </summary>
/// <remarks>
/// <para>
/// Three methods, because a share link has three moments: it is issued, it is
/// redeemed, and it is withdrawn. Nothing here lists links or reads one by
/// identifier - those belong to the management API and can be added when there
/// is an endpoint that needs them.
/// </para>
/// <para>
/// The plaintext token never reaches this port. A caller generates it, hands
/// over its digest, and is responsible for showing the token exactly once; the
/// store deals only in digests, so a leak of everything this layer holds does
/// not yield a usable link.
/// </para>
/// <para>
/// Expiry and revocation are conditions of the resolution query rather than
/// state a caller checks afterwards. A link that has run out or been withdrawn
/// resolves to nothing, which is the same answer a token nobody ever issued
/// gets - and that sameness is what stops the endpoint in front of it from
/// telling the two apart.
/// </para>
/// </remarks>
public interface IShareLinkStore
{
    /// <summary>
    /// Issues a share link.
    /// </summary>
    /// <param name="studyInstanceUid">The study the link leads to, already trimmed and length checked.</param>
    /// <param name="permission">What the link permits, from <c>GrantPermissions</c>.</param>
    /// <param name="tokenHash">
    /// The SHA-256 digest of the token, <see cref="ShareLinkLimits.TokenHashLength"/> bytes.
    /// </param>
    /// <param name="createdByUserId">The account issuing the link.</param>
    /// <param name="expiresAtUtc">When the link stops working. Required.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The link as stored, without the token.</returns>
    /// <remarks>
    /// Every link expires. There is no overload that omits an expiry, because a
    /// bearer credential for a patient's imaging that never dies is not
    /// something an operator should be able to create by leaving a field blank.
    /// </remarks>
    public Task<ShareLinkSummary> CreateAsync(
        string studyInstanceUid,
        string permission,
        byte[] tokenHash,
        Guid createdByUserId,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a token digest to the study it leads to.
    /// </summary>
    /// <param name="tokenHash">The SHA-256 digest of the presented token.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// What the link leads to, or <see langword="null"/> when no live link
    /// matches - unknown, expired and revoked are one answer.
    /// </returns>
    public Task<ShareLinkTarget?> FindActiveAsync(
        byte[] tokenHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws a share link.
    /// </summary>
    /// <param name="shareLinkId">The link to withdraw.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The link <em>as it stood before this call</em>, or <see langword="null"/>
    /// when no such link exists.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The previous state, not the new one, and deliberately: it is what lets one
    /// call answer all three questions the caller has. Null means there is no
    /// such link, so the answer is 404. A returned link whose
    /// <c>RevokedAtUtc</c> is null was standing and has now been withdrawn, so
    /// the withdrawal is recorded. A returned link that was already withdrawn
    /// changed nothing, so nothing is recorded and repeating the request stays
    /// safe.
    /// </para>
    /// <para>
    /// It also carries the study the link was for, which the caller needs in
    /// order to record what it did - the audit trail names a study, and by then
    /// the link is the only thing that knows which one.
    /// </para>
    /// <para>
    /// Stamps the row rather than deleting it, like an access grant. Which link
    /// was issued for which study, by whom, and until when is exactly what an
    /// investigation asks afterwards, and a deleted row answers nothing.
    /// </para>
    /// </remarks>
    public Task<ShareLinkSummary?> RevokeAsync(
        Guid shareLinkId,
        CancellationToken cancellationToken = default);
}
