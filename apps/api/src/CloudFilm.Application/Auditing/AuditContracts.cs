namespace CloudFilm.Application.Auditing;

/// <summary>
/// The clinical events CloudFilm records.
/// </summary>
/// <remarks>
/// Constants rather than an enum, because the value is written to the database
/// verbatim and read back by people running SQL during an incident. An enum
/// would store an integer whose meaning lives only in this assembly.
/// </remarks>
public static class AuditAction
{
    /// <summary>Credentials were accepted and a token was issued.</summary>
    public const string LoginSucceeded = "LOGIN_SUCCEEDED";

    /// <summary>
    /// Credentials were rejected.
    /// </summary>
    /// <remarks>
    /// One action for every rejection. An unknown account, a wrong password, an
    /// unconfirmed address and a locked-out account all record this and nothing
    /// more. Splitting them would rebuild, inside the audit table, exactly the
    /// account enumeration oracle the sign-in path is built to avoid.
    /// </remarks>
    public const string LoginFailed = "LOGIN_FAILED";

    /// <summary>A patient record was successfully returned to a caller.</summary>
    public const string ViewPatient = "VIEW_PATIENT";

    /// <summary>A study was successfully returned to a caller.</summary>
    public const string ViewStudy = "VIEW_STUDY";

    /// <summary>
    /// A user marked a study as read.
    /// </summary>
    /// <remarks>
    /// Recorded only when the state actually changed. A user who marks an
    /// already-read study read again has performed no state transition, and a
    /// trail that says otherwise is describing HTTP requests rather than
    /// clinical activity.
    /// </remarks>
    public const string MarkStudyRead = "MARK_STUDY_READ";

    /// <summary>
    /// A user marked a study as unread.
    /// </summary>
    /// <remarks>Recorded on transition only, like <see cref="MarkStudyRead"/>.</remarks>
    public const string MarkStudyUnread = "MARK_STUDY_UNREAD";

    /// <summary>
    /// A user searched the imaging catalogue and was shown the results.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One record per search, never one per result. A search that matched forty
    /// studies is one act by one person; forty <c>VIEW_PATIENT</c> rows would
    /// claim forty records were opened, which is false, and would bury the
    /// accesses that really happened.
    /// </para>
    /// <para>
    /// Recorded with no resource id. The terms someone searched for are
    /// themselves identifying - a name, an accession number - and this table has
    /// exactly one column that could hold them, documented as holding an MRN or
    /// a Study Instance UID. Writing a query string there would quietly turn the
    /// audit trail into a second store of the data it exists to protect.
    /// </para>
    /// </remarks>
    public const string SearchStudies = "SEARCH_STUDIES";
}

/// <summary>
/// The kind of thing an audited action was performed on.
/// </summary>
public static class AuditResourceType
{
    /// <summary>The authentication system itself, rather than a clinical record.</summary>
    public const string Authentication = "Authentication";

    /// <summary>A patient, identified by Medical Record Number.</summary>
    public const string Patient = "Patient";

    /// <summary>A study, identified by Study Instance UID.</summary>
    public const string Study = "Study";
}

/// <summary>
/// One thing that happened, as the caller describes it.
/// </summary>
/// <remarks>
/// <para>
/// Carries no timestamp and no correlation id. Both are facts about the
/// surrounding request rather than about the event, and letting callers supply
/// them would mean every call site could get them wrong in a different way. The
/// service fills them in from one place.
/// </para>
/// <para>
/// There is deliberately nowhere here to put a password, a token, a signing key,
/// a request body or a patient's name. The type is the enforcement: a caller
/// cannot record what the shape cannot hold.
/// </para>
/// </remarks>
/// <param name="ActorUserId">
/// The account that acted, or <see langword="null"/> when there was none - a
/// rejected sign-in for an address nobody owns has no actor.
/// </param>
/// <param name="Action">One of the <see cref="AuditAction"/> values.</param>
/// <param name="ResourceType">One of the <see cref="AuditResourceType"/> values.</param>
/// <param name="ResourceId">
/// The Medical Record Number or Study Instance UID acted on, or
/// <see langword="null"/> when the action names no resource.
/// </param>
public sealed record AuditEntry(
    Guid? ActorUserId,
    string Action,
    string ResourceType,
    string? ResourceId);
