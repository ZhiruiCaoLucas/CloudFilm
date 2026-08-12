namespace CloudFilm.Infrastructure.Auditing;

/// <summary>
/// One recorded clinical access event.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately does not implement <c>IAuditableEntity</c>. That interface
/// maintains <c>CreatedAtUtc</c> and <c>ModifiedAtUtc</c>, and a modification
/// timestamp on an audit record is a contradiction: these rows are written once
/// and never updated. <see cref="OccurredAtUtc"/> also means something different
/// - when the event happened, not when the row was inserted.
/// </para>
/// <para>
/// <see cref="ActorUserId"/> carries no foreign key to <c>AspNetUsers</c> on
/// purpose. An account may be deleted; the record of what it did must outlive
/// it. A foreign key would force a choice between blocking the deletion and
/// destroying the history, and both are wrong.
/// </para>
/// </remarks>
public sealed class AuditLog
{
    /// <summary>Gets or sets the record identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the account that acted, or <see langword="null"/> when there
    /// was none.
    /// </summary>
    /// <remarks>
    /// Null for a sign-in rejected because no account matched the address. That
    /// nullability is itself an existence oracle, which is why nothing reads this
    /// table over HTTP.
    /// </remarks>
    public Guid? ActorUserId { get; set; }

    /// <summary>Gets or sets what happened.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind of resource acted on.</summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Medical Record Number or Study Instance UID acted on.
    /// </summary>
    /// <remarks>
    /// This is the field that makes the table worth keeping: an audit trail that
    /// records "somebody viewed a patient" without saying which patient answers
    /// no question anyone asks during an investigation. It is a PHI identifier -
    /// never a name, a date of birth or a diagnosis - and the table must be
    /// protected accordingly.
    /// </remarks>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the HTTP request that produced the event.
    /// </summary>
    /// <remarks>
    /// The same value Serilog stamps on the request log and the exception handler
    /// returns in a problem response, so one identifier ties an audit row, an
    /// application log line and the reference a user was shown together.
    /// </remarks>
    public string? CorrelationId { get; set; }

    /// <summary>Gets or sets when the event happened, in UTC.</summary>
    public DateTimeOffset OccurredAtUtc { get; set; }
}
