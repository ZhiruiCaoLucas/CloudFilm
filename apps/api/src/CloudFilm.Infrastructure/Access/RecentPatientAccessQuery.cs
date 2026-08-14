using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Access;
using CloudFilm.Application.Auditing;
using CloudFilm.Infrastructure.Auditing;
using CloudFilm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudFilm.Infrastructure.Access;

/// <summary>
/// Reads recent patient accesses out of the audit trail.
/// </summary>
/// <remarks>
/// The audit table is the only place CloudFilm records which patients a user has
/// opened, so it is the only place this can come from. That makes this the first
/// code that reads that table, and the filters below are the entire security
/// boundary: they are written once, here, and the port above exposes no way to
/// vary them.
/// </remarks>
internal sealed class RecentPatientAccessQuery(CloudFilmDbContext context) : IRecentPatientAccess
{
    private readonly CloudFilmDbContext _context = context;

    /// <summary>
    /// Builds the query, separately from executing it.
    /// </summary>
    /// <param name="auditLogs">The audit rows to search.</param>
    /// <param name="actorUserId">The only account whose rows may be read.</param>
    /// <param name="limit">The maximum number of patients to return.</param>
    /// <returns>Medical Record Numbers, most recently opened first.</returns>
    /// <remarks>
    /// <para>
    /// Split out so the filters can be tested without a database. The same
    /// expression tree runs against PostgreSQL through EF and against a list in
    /// the test suite, so a test that proves a sign-in row is excluded proves it
    /// about the query that actually ships.
    /// </para>
    /// <para>
    /// Four filters, each load-bearing:
    /// </para>
    /// <para>
    /// <c>ActorUserId == actorUserId</c> - one clinician must never see which
    /// patients another opened. This is the only scoping there is.
    /// </para>
    /// <para>
    /// <c>Action == VIEW_PATIENT</c> - excludes sign-in rows. A
    /// <c>LOGIN_FAILED</c> row carries a null actor precisely when no account
    /// matched the address, so letting those out of the table would rebuild the
    /// account-enumeration oracle the sign-in path is built to avoid.
    /// </para>
    /// <para>
    /// <c>ResourceType == Patient</c> - belt and braces with the action, and it
    /// keeps Study Instance UIDs out of a list the portal will render as patient
    /// links.
    /// </para>
    /// <para>
    /// <c>ResourceId != null</c> - a row with no resource cannot become a link.
    /// </para>
    /// <para>
    /// The grouping is what makes this a list of patients rather than a list of
    /// events: opening one patient five times should occupy one row, not five,
    /// and the ordering keeps the most recently opened first.
    /// </para>
    /// </remarks>
    public static IQueryable<string> BuildQuery(
        IQueryable<AuditLog> auditLogs,
        Guid actorUserId,
        int limit) =>
        auditLogs
            .Where(log => log.ActorUserId == actorUserId
                && log.Action == AuditAction.ViewPatient
                && log.ResourceType == AuditResourceType.Patient
                && log.ResourceId != null)
            .GroupBy(log => log.ResourceId!)
            .Select(group => new { MedicalRecordNumber = group.Key, LastOpened = group.Max(log => log.OccurredAtUtc) })
            .OrderByDescending(entry => entry.LastOpened)
            .Take(limit)
            .Select(entry => entry.MedicalRecordNumber);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecentPatient>> GetRecentPatientsAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        List<string> medicalRecordNumbers = await BuildQuery(
                _context.AuditLogs.AsNoTracking(),
                actorUserId,
                RecentPatientAccess.MaxResults)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return medicalRecordNumbers.Select(number => new RecentPatient(number)).ToList();
    }
}
