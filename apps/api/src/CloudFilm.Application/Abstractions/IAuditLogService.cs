using CloudFilm.Application.Auditing;

namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Records clinical access events.
/// </summary>
/// <remarks>
/// <para>
/// The port the API depends on. Its implementation owns the storage; nothing
/// above this interface knows an audit table exists, which is what keeps EF Core
/// out of the Application layer.
/// </para>
/// <para>
/// Deliberately write-only. CloudFilm has no endpoint that reads audit records
/// and no administrative UI, because a queryable audit trail is itself a
/// disclosure risk - the presence or absence of an actor on a failed sign-in
/// answers "does this account exist?". Reading is a DBA operation until there is
/// a role model to restrict it to.
/// </para>
/// </remarks>
public interface IAuditLogService
{
    /// <summary>
    /// Records one event.
    /// </summary>
    /// <param name="entry">What happened.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes once the event is durably stored.</returns>
    /// <remarks>
    /// <para>
    /// Throws if the event could not be stored. That is deliberate and it is the
    /// contract callers must handle explicitly, because the right response
    /// differs by call site:
    /// </para>
    /// <para>
    /// A caller about to return patient data must let the failure propagate. An
    /// unaudited disclosure of PHI is worse than a failed request, so the request
    /// fails and the data never leaves the process.
    /// </para>
    /// <para>
    /// A caller auditing a sign-in must catch it. Turning an audit outage into a
    /// changed authentication outcome would let anyone probe the audit database's
    /// health through the login endpoint, and could make a correct password look
    /// wrong.
    /// </para>
    /// </remarks>
    public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
