namespace CloudFilm.Application.Grants;

/// <summary>
/// What an access grant permits.
/// </summary>
/// <remarks>
/// <para>
/// Constants rather than an enum, because the value is written to the database
/// verbatim and read back by people running SQL during an investigation - the
/// same reasoning that made <c>AuditAction</c> a set of strings.
/// </para>
/// <para>
/// The two are separate permissions, not a ladder. Whether holding
/// <see cref="Download"/> should also confer <see cref="View"/> is a policy
/// question, and policy belongs with the endpoint that enforces it rather than
/// with the table that stores it. Nothing here assumes an answer: a grant is
/// checked against the permission the caller names, and if a hierarchy is wanted
/// later it is one rule in one place instead of an implication baked into every
/// row ever written.
/// </para>
/// </remarks>
public static class GrantPermissions
{
    /// <summary>Permits opening the study in CloudFilm.</summary>
    public const string View = "View";

    /// <summary>Permits retrieving the study's images.</summary>
    public const string Download = "Download";

    /// <summary>
    /// Every permission CloudFilm recognises.
    /// </summary>
    /// <remarks>
    /// The set of names that may be stored. There is deliberately no value that
    /// means "all of them": with <see cref="Download"/> already satisfying
    /// <see cref="View"/>, such a value would be a second spelling of
    /// <see cref="Download"/>, and every query would have to match both forever.
    /// </remarks>
    public static IReadOnlyList<string> All { get; } = [View, Download];

    /// <summary>
    /// Reports whether a permission somebody holds covers the one being asked for.
    /// </summary>
    /// <param name="held">The permission recorded on the grant.</param>
    /// <param name="required">The permission the caller needs.</param>
    /// <returns><see langword="true"/> when the grant is enough.</returns>
    /// <remarks>
    /// <para>
    /// One ladder, one rung: <see cref="Download"/> covers <see cref="View"/>,
    /// because being allowed to take a copy of a study and not being allowed to
    /// look at it is not a policy anybody meant to write. Nothing else implies
    /// anything - <see cref="View"/> does not confer <see cref="Download"/>.
    /// </para>
    /// <para>
    /// Stated here, once, rather than in the stored data. The database keeps
    /// what was granted; this decides what that is worth, so changing the ladder
    /// later is a change to one method instead of a migration over every row
    /// ever written.
    /// </para>
    /// <para>
    /// Unknown values satisfy nothing. A permission this application does not
    /// define permits nothing, whatever a row in the table says.
    /// </para>
    /// </remarks>
    public static bool Satisfies(string? held, string required) => held switch
    {
        Download => required is Download or View,
        View => required is View,
        _ => false,
    };

    /// <summary>
    /// The permissions that would satisfy a requirement.
    /// </summary>
    /// <param name="required">The permission the caller needs.</param>
    /// <returns>Every recognised permission for which <see cref="Satisfies"/> holds.</returns>
    /// <remarks>
    /// Lets a caller ask the store one question per candidate without restating
    /// the ladder, and keeps <see cref="Satisfies"/> the only place it is
    /// written down.
    /// </remarks>
    public static IReadOnlyList<string> Satisfying(string required) =>
        All.Where(permission => Satisfies(permission, required)).ToList();

    /// <summary>Reports whether a value may be stored as a permission.</summary>
    /// <param name="permission">The candidate value.</param>
    /// <returns><see langword="true"/> when CloudFilm defines it.</returns>
    public static bool IsRecognised(string? permission) =>
        permission is not null && All.Contains(permission, StringComparer.Ordinal);
}

/// <summary>
/// One access grant, as the API reports it.
/// </summary>
/// <remarks>
/// <para>
/// Carries identifiers and nothing else. The grantee is a <see cref="Guid"/>,
/// never an account record: an administrator managing grants has no need of
/// somebody's password hash, security stamp or lockout state, and a projection
/// that cannot hold them cannot leak them.
/// </para>
/// <para>
/// It names a study but no patient. A grant is about one imaging study, and
/// resolving whose it is would put a name and a medical record number into a
/// response that exists to answer an administrative question.
/// </para>
/// </remarks>
/// <param name="Id">The grant's identifier, and what a revocation names.</param>
/// <param name="StudyInstanceUid">The study the grant applies to.</param>
/// <param name="GranteeUserId">The account holding the grant.</param>
/// <param name="Permission">One of the <see cref="GrantPermissions"/> values.</param>
/// <param name="CreatedAtUtc">When the grant was made.</param>
/// <param name="ExpiresAtUtc">When it stops applying, or <see langword="null"/> for never.</param>
/// <param name="RevokedAtUtc">
/// When it was withdrawn, or <see langword="null"/> while it stands. Always
/// <see langword="null"/> in the active list; present so a history view can
/// reuse this shape unchanged.
/// </param>
public sealed record StudyAccessGrant(
    Guid Id,
    string StudyInstanceUid,
    Guid GranteeUserId,
    string Permission,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc);

/// <summary>
/// A request to grant an account access to a study.
/// </summary>
/// <remarks>
/// There is deliberately nowhere here to say who is granting. That is taken from
/// the bearer token on every request, so a client cannot attribute its own grant
/// to somebody else - the shape is the enforcement, not a check that could be
/// forgotten.
/// </remarks>
/// <param name="StudyInstanceUid">The study to open up.</param>
/// <param name="GranteeUserId">The account to open it to.</param>
/// <param name="Permission">One of the <see cref="GrantPermissions"/> values.</param>
/// <param name="ExpiresAtUtc">
/// When the grant should stop applying, or <see langword="null"/> for no expiry.
/// </param>
public sealed record CreateAccessGrantRequest(
    string StudyInstanceUid,
    Guid GranteeUserId,
    string Permission,
    DateTimeOffset? ExpiresAtUtc);

/// <summary>
/// The bounds the grant store enforces on the identifiers it accepts.
/// </summary>
public static class GrantLimits
{
    /// <summary>
    /// The longest Study Instance UID a grant will accept.
    /// </summary>
    /// <remarks>
    /// Matches <c>AuditLog.ResourceId</c> and <c>StudyReadState.StudyInstanceUid</c>,
    /// which already store these exact strings. DICOM caps a UID at 64
    /// characters; 128 leaves room without letting an unbounded string into a
    /// table that is indexed on it.
    /// </remarks>
    public const int MaxStudyInstanceUidLength = 128;

    /// <summary>The longest permission name the column holds.</summary>
    public const int MaxPermissionLength = 32;
}
