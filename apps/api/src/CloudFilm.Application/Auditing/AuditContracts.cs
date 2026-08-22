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

    /// <summary>
    /// An account was given access to a study it does not otherwise hold.
    /// </summary>
    /// <remarks>
    /// Recorded against the study, because that is what this table's resource id
    /// column holds. Which account was granted access is not recorded here -
    /// there is one actor column and it belongs to whoever performed the act -
    /// so an investigation reads the grant table for the recipient and this
    /// trail for the fact that somebody opened the study up, and when.
    /// </remarks>
    public const string GrantCreated = "GRANT_CREATED";

    /// <summary>
    /// An access grant was withdrawn.
    /// </summary>
    /// <remarks>
    /// Recorded only when a standing grant was actually withdrawn. Revoking
    /// something already revoked changed nothing, and a trail that says
    /// otherwise is describing requests rather than access.
    /// </remarks>
    public const string GrantRevoked = "GRANT_REVOKED";

    /// <summary>
    /// A share link was issued for a study.
    /// </summary>
    /// <remarks>
    /// Recorded against the study, because that is what this table's resource id
    /// column holds. The token is not recorded and never will be: it is a live
    /// credential, and an audit trail that stores one is a second place to steal
    /// it from. Which link was issued is answered by the share link table, which
    /// holds a digest rather than the token itself.
    /// </remarks>
    public const string ShareLinkCreated = "SHARE_LINK_CREATED";

    /// <summary>
    /// A share link was withdrawn.
    /// </summary>
    /// <remarks>
    /// Recorded only when a standing link was actually withdrawn. Revoking
    /// something already revoked changed nothing, and a trail that says
    /// otherwise is describing requests rather than access.
    /// </remarks>
    public const string ShareLinkRevoked = "SHARE_LINK_REVOKED";

    /// <summary>
    /// A study's images were retrieved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first action that records pixel data leaving CloudFilm rather than
    /// metadata being read, which is why it is not folded into
    /// <see cref="ViewStudy"/>: an investigation asking "did this study leave the
    /// building" must not have to distinguish it from a clinician opening a
    /// worklist.
    /// </para>
    /// <para>
    /// Recorded with no actor when a share link was redeemed. The actor column is
    /// nullable precisely because some acts have no account behind them, and a
    /// link is redeemed by whoever holds it. The link itself is not recorded -
    /// neither its token nor its digest nor its identifier - because the resource
    /// column holds a study and nothing else, and a trail carrying a live
    /// credential is a second place to steal it from.
    /// </para>
    /// </remarks>
    public const string DownloadStudy = "DOWNLOAD_STUDY";

    /// <summary>
    /// A share link was redeemed to view a study.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Recorded once, when the link is exchanged for a viewer session - not on
    /// the requests that follow. Opening one study makes dozens to hundreds of
    /// DICOMweb calls, and a row for each would drown the trail in traffic while
    /// answering the same question the first row already answered: this link was
    /// used, at this time, for this study.
    /// </para>
    /// <para>
    /// Distinct from <see cref="DownloadStudy"/>, which records images leaving
    /// the system, and from <see cref="ViewStudy"/>, which records a signed-in
    /// clinician reading a record. This one has no actor: a link is redeemed by
    /// whoever holds it.
    /// </para>
    /// </remarks>
    public const string ShareLinkViewed = "SHARE_LINK_VIEWED";

    /// <summary>
    /// A study was sent to the analysis service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Its own action rather than a <see cref="ViewStudy"/>, because it answers a
    /// different question. A clinician asking for an analysis has not read the
    /// study; what has happened is that the study left CloudFilm for a processor,
    /// which is closer to <see cref="DownloadStudy"/> - and distinct from that
    /// too, because nothing was handed to a person.
    /// </para>
    /// <para>
    /// Recorded once per request, against the study the caller was authorised
    /// for, with the account that asked. What it cannot yet record is which model
    /// answered: <see cref="AuditEntry"/> has a column for the resource and none
    /// for the thing that acted on it. Until it does, "this study was analysed"
    /// is the honest extent of the claim, and the model and version live in the
    /// result returned to the caller.
    /// </para>
    /// </remarks>
    public const string AnalyseStudy = "ANALYSE_STUDY";
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
