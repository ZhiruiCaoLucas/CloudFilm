using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auditing;
using CloudFilm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace CloudFilm.Infrastructure.Auditing;

/// <summary>
/// Writes audit events to PostgreSQL.
/// </summary>
/// <remarks>
/// <para>
/// Synchronous, one INSERT per event, no queue. That is the right first
/// implementation: CloudFilm records four event types at human speed - a
/// clinician opening a patient, not a device streaming frames - and a message
/// broker introduced for a load nobody has measured would add a way to lose
/// audit records in exchange for throughput nobody needs.
/// </para>
/// <para>
/// The timestamp and the correlation id are filled in here rather than accepted
/// from callers. Both are facts about the surrounding request, and letting five
/// call sites each derive them is five chances to derive them differently.
/// </para>
/// </remarks>
internal sealed class AuditLogService(
    CloudFilmDbContext context,
    IClock clock,
    IHttpContextAccessor httpContextAccessor) : IAuditLogService
{
    private readonly CloudFilmDbContext _context = context;
    private readonly IClock _clock = clock;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    /// <inheritdoc />
    public async Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        AuditLog log = new()
        {
            Id = Guid.NewGuid(),
            ActorUserId = entry.ActorUserId,
            Action = entry.Action,
            ResourceType = entry.ResourceType,
            ResourceId = entry.ResourceId,

            // The identifier Serilog stamps on the request log and the exception
            // handler returns to the caller. Null outside a request - a
            // background writer has no correlation to reuse, and inventing one
            // would be a second correlation scheme.
            CorrelationId = _httpContextAccessor.HttpContext?.TraceIdentifier,

            // IClock, never DateTime.UtcNow, so the recorded instant is the one
            // the rest of the system agrees on and tests can pin.
            OccurredAtUtc = _clock.UtcNow,
        };

        await _context.AuditLogs.AddAsync(log, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
