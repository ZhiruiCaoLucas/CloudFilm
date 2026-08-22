namespace CloudFilm.Infrastructure.Sharing;

/// <summary>
/// A bearer credential that leads to one study.
/// </summary>
/// <remarks>
/// <para>
/// Its own table rather than an access grant, and for a structural reason:
/// <c>AccessGrant.GranteeUserId</c> is not nullable and carries a foreign key to
/// <c>AspNetUsers</c>. A link is redeemed by whoever holds it, which is nobody
/// in particular, so there is no account to put there. Bending the grant table
/// to accept one would weaken a column every resource check depends on.
/// </para>
/// <para>
/// The token itself is not here and never will be. Only its SHA-256 digest is
/// stored, so an attacker who reads this table learns which studies have been
/// shared and until when, but cannot construct a working link. The plaintext
/// exists in the response that created it and in the recipient's URL, and
/// nowhere else.
/// </para>
/// <para>
/// Rows are never deleted and never rewritten. Withdrawing a link stamps
/// <see cref="RevokedAtUtc"/>; a link's study, permission and expiry are fixed
/// at issue. Who opened a patient's imaging to the holder of a URL, and until
/// when, is what an investigation reads, and an updated row answers only the
/// last version of it.
/// </para>
/// </remarks>
public sealed class ShareLink
{
    /// <summary>Gets or sets the record identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the DICOM Study Instance UID the link leads to.</summary>
    /// <remarks>
    /// One study per link. A token that could reach more than one study would be
    /// a credential whose scope nobody could read off the row it lives in.
    /// </remarks>
    public string StudyInstanceUid { get; set; } = string.Empty;

    /// <summary>Gets or sets what the link permits.</summary>
    /// <remarks>
    /// One of the <c>GrantPermissions</c> values, stored verbatim. The first
    /// release issues download links only; the column is general because the
    /// value is what a redemption checks, and a link that permits something this
    /// application does not define permits nothing.
    /// </remarks>
    public string Permission { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA-256 digest of the token.
    /// </summary>
    /// <remarks>
    /// Always 32 bytes, because SHA-256 always is. PostgreSQL's <c>bytea</c> has
    /// no fixed-length form, so that length is a property of what writes here
    /// rather than something the column enforces.
    /// </remarks>
    public byte[] TokenHash { get; set; } = [];

    /// <summary>Gets or sets the account that issued the link.</summary>
    /// <remarks>
    /// Deliberately not a foreign key, for the reason the audit trail's actor
    /// and <c>AccessGrant.GrantedByUserId</c> are not: who opened a patient's
    /// imaging to a URL must outlive the account that did it. A foreign key
    /// would force a choice between blocking the deletion and erasing the
    /// record, and both are wrong.
    /// </remarks>
    public Guid CreatedByUserId { get; set; }

    /// <summary>Gets or sets when the link was issued, in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the link stops working, in UTC.
    /// </summary>
    /// <remarks>
    /// Not nullable, unlike an access grant's expiry. A grant is given to a
    /// known account and may reasonably stand until somebody withdraws it; a
    /// link is handed to whoever ends up holding the URL, and one that never
    /// expires is a permanent, unattributable key to a patient's imaging.
    /// </remarks>
    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the link was withdrawn, in UTC, or
    /// <see langword="null"/> while it stands.
    /// </summary>
    /// <remarks>
    /// The column that separates a live link from a historical one, so it is
    /// what every resolution filters on. Revocation is how a leaked URL is
    /// stopped, and it has to take effect on the next request rather than at the
    /// next expiry.
    /// </remarks>
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
