namespace CloudFilm.Infrastructure.ReadState;

/// <summary>
/// A record that one user has marked one study as read.
/// </summary>
/// <remarks>
/// <para>
/// The row's existence is the state. There is no <c>IsRead</c> column: a row
/// means read, no row means unread, marking read inserts and marking unread
/// deletes. A boolean column would give "unread" two representations - no row,
/// and a row saying false - and every query would have to remember to handle
/// both. The history of the toggling lives in the audit trail, which is
/// append-only, so nothing is lost by deleting the row.
/// </para>
/// <para>
/// No surrogate key. The identity of this record is the pair
/// (<see cref="ActorUserId"/>, <see cref="StudyInstanceUid"/>), and making that
/// pair the primary key means the database itself forbids a user holding two
/// contradictory marks for one study. An <c>Id</c> column would need a separate
/// unique index to say the same thing, which is one more thing to get wrong.
/// </para>
/// <para>
/// Deliberately does not implement <c>IAuditableEntity</c>. That interface
/// maintains <c>CreatedAtUtc</c> and <c>ModifiedAtUtc</c>, and this row is never
/// modified - it is inserted or deleted - so the modification stamp would be
/// permanently null and the creation stamp would duplicate
/// <see cref="MarkedReadAtUtc"/>.
/// </para>
/// <para>
/// Shares a simple name with <c>CloudFilm.Application.ReadState.StudyReadState</c>,
/// which is the wire contract. They are never used in the same file: this type
/// stays inside persistence and the contract stays at the API boundary.
/// </para>
/// </remarks>
public sealed class StudyReadState
{
    /// <summary>
    /// Gets or sets the account that marked the study read.
    /// </summary>
    /// <remarks>
    /// Not nullable, unlike the audit trail's actor. An audit row with no actor
    /// still records that something happened; a mark with no owner belongs to
    /// nobody and could never be read back.
    /// </remarks>
    public Guid ActorUserId { get; set; }

    /// <summary>
    /// Gets or sets the DICOM Study Instance UID that was marked.
    /// </summary>
    /// <remarks>
    /// Stored exactly as the endpoint accepted it, after trimming. No case
    /// folding: a DICOM UID is digits and dots, so it has no case to fold.
    /// </remarks>
    public string StudyInstanceUid { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the study was marked read, in UTC.
    /// </summary>
    /// <remarks>
    /// The one fact the row's existence does not already state. Never returned
    /// by the API - it is kept because a future "recently read" ordering would
    /// need it, and because a mark with no time is harder to reason about during
    /// an investigation than one with.
    /// </remarks>
    public DateTimeOffset MarkedReadAtUtc { get; set; }
}
