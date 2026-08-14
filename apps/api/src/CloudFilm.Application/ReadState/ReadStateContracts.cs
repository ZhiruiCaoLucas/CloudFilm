namespace CloudFilm.Application.ReadState;

/// <summary>
/// Whether the signed-in user has marked a study as read.
/// </summary>
/// <remarks>
/// <para>
/// One field, because there is one question. The record deliberately carries no
/// actor, no study identifier and no timestamp: the caller already knows which
/// study it asked about and can only ever ask about itself, so returning either
/// would be repeating the request back, and returning the timestamp would leak a
/// detail of how the state is stored for no reader that needs it.
/// </para>
/// <para>
/// This is CloudFilm's own workflow state. It is not DICOM, it is not in
/// Orthanc, and it is not <c>Study.Status</c> - that field is the archive's view
/// of a study's lifecycle and means the same thing to everybody. This one is per
/// user, and two clinicians looking at the same study will legitimately disagree
/// about it.
/// </para>
/// </remarks>
/// <param name="Read">
/// <see langword="true"/> when this user has marked the study read.
/// </param>
public sealed record StudyReadState(bool Read);

/// <summary>
/// Bounds the read-state feature enforces on the identifiers it accepts.
/// </summary>
public static class StudyReadStateLimits
{
    /// <summary>
    /// The longest Study Instance UID the read-state store will accept.
    /// </summary>
    /// <remarks>
    /// Matches <c>AuditLog.ResourceId</c>, which already stores these exact
    /// strings. DICOM caps a UID at 64 characters; 128 leaves room without
    /// letting an unbounded string into a table keyed on it. The endpoint
    /// rejects anything longer rather than truncating, because a truncated UID
    /// is a different study.
    /// </remarks>
    public const int MaxStudyInstanceUidLength = 128;
}
