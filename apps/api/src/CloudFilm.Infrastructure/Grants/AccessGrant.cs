namespace CloudFilm.Infrastructure.Grants;

/// <summary>
/// A record that one account may reach one study.
/// </summary>
/// <remarks>
/// <para>
/// CloudFilm's first resource-level permission. Roles answer "may this person
/// use the system"; a grant answers "may this person see this study", which is
/// the question a hospital actually asks when a clinician needs a second
/// opinion on one patient.
/// </para>
/// <para>
/// Carries a surrogate key rather than the composite one
/// <c>StudyReadState</c> uses, because the pair
/// (<see cref="StudyInstanceUid"/>, <see cref="GranteeUserId"/>) is not unique
/// over the life of the table: a grant that is revoked stays, and the same
/// person may later be granted access again. Uniqueness applies only to the
/// active row, which a partial index expresses and a primary key cannot.
/// </para>
/// <para>
/// Rows are never deleted and never rewritten. Withdrawing access stamps
/// <see cref="RevokedAtUtc"/>; changing a permission is a revoke and a new
/// grant. Who could see a patient's imaging, granted by whom, and until when, is
/// the question an investigation asks afterwards - and an updated row answers
/// only the last version of it.
/// </para>
/// </remarks>
public sealed class AccessGrant
{
    /// <summary>Gets or sets the record identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the DICOM Study Instance UID the grant applies to.</summary>
    /// <remarks>
    /// The study, not the patient and not a series. It is the one identifier
    /// that exists at every layer of CloudFilm - the API route, the audit trail,
    /// the read-state table and the viewer's launch URL - and it is the unit a
    /// clinician actually shares.
    /// </remarks>
    public string StudyInstanceUid { get; set; } = string.Empty;

    /// <summary>Gets or sets the account the grant was given to.</summary>
    /// <remarks>
    /// Foreign key to <c>AspNetUsers</c>: a grant to an account that no longer
    /// exists permits nothing, so it goes when the account goes.
    /// </remarks>
    public Guid GranteeUserId { get; set; }

    /// <summary>Gets or sets the account that made the grant.</summary>
    /// <remarks>
    /// Deliberately not a foreign key, for the reason the audit trail's actor is
    /// not one: who opened a patient's imaging to somebody else must outlive the
    /// account that did it. A foreign key would force a choice between blocking
    /// the deletion and erasing the record, and both are wrong.
    /// </remarks>
    public Guid GrantedByUserId { get; set; }

    /// <summary>Gets or sets what the grant permits.</summary>
    /// <remarks>One of the <c>GrantPermissions</c> values, stored verbatim.</remarks>
    public string Permission { get; set; } = string.Empty;

    /// <summary>Gets or sets when the grant was made, in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the grant stops applying, in UTC, or
    /// <see langword="null"/> when it does not expire.
    /// </summary>
    /// <remarks>
    /// Null means no expiry, not "expired" and not "unknown". A grant with no
    /// end date is a deliberate choice by whoever made it, and it is still
    /// revocable.
    /// </remarks>
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the grant was withdrawn, in UTC, or
    /// <see langword="null"/> while it stands.
    /// </summary>
    /// <remarks>
    /// This column is the difference between an active grant and a historical
    /// one, so it is what the uniqueness rule and every read filter on.
    /// </remarks>
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
