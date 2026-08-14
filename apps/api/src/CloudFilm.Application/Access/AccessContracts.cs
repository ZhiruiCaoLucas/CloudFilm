namespace CloudFilm.Application.Access;

/// <summary>
/// A patient the signed-in user opened recently.
/// </summary>
/// <remarks>
/// <para>
/// One field, and it is the only one the portal needs to render a link. There is
/// deliberately nowhere here for the audit row's identifier, its actor, its
/// correlation id or its timestamp: the caller is asking "which patients did I
/// open", not "show me my audit records", and the response should not teach a
/// client that an audit table exists.
/// </para>
/// <para>
/// Named for what the value is rather than for where it came from. The audit
/// table calls this column <c>ResourceId</c> because it also holds Study
/// Instance UIDs; at this boundary it is always a Medical Record Number, and
/// saying so keeps the storage detail out of the contract.
/// </para>
/// <para>
/// The Medical Record Number is PHI. Everything that returns this type is
/// therefore an authenticated, per-user endpoint.
/// </para>
/// </remarks>
/// <param name="MedicalRecordNumber">The patient's hospital identifier.</param>
public sealed record RecentPatient(string MedicalRecordNumber);

/// <summary>
/// Bounds on the recent-patient list.
/// </summary>
public static class RecentPatientAccess
{
    /// <summary>
    /// How many distinct patients the list returns at most.
    /// </summary>
    /// <remarks>
    /// A fixed server-side ceiling, not a client parameter. The audit table grows
    /// without bound and is never pruned, so a caller-supplied page size would
    /// eventually let one request read a clinician's entire access history - a
    /// far larger disclosure than the shortcut this list exists to provide.
    /// </remarks>
    public const int MaxResults = 10;
}
