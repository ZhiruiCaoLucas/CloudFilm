using CloudFilm.Application.Access;
using CloudFilm.Application.Auditing;
using CloudFilm.Infrastructure.Access;
using CloudFilm.Infrastructure.Auditing;
using FluentAssertions;

namespace CloudFilm.Api.Tests.Access;

/// <summary>
/// What the recent-patients query selects out of the audit table, and - more to
/// the point - what it refuses to select.
/// </summary>
/// <remarks>
/// <para>
/// Runs the real expression tree against an in-memory sequence. That is the
/// whole reason <see cref="RecentPatientAccessQuery.BuildQuery"/> is a static
/// returning <see cref="IQueryable{T}"/>: the predicates, grouping, ordering and
/// bound under test here are the same ones EF Core translates to SQL, so a
/// filter deleted in production is a failure here rather than a PHI leak found
/// in a hospital.
/// </para>
/// <para>
/// What LINQ to Objects cannot vouch for is translation - that PostgreSQL
/// accepts this shape at all, and that its collation orders identically. Those
/// are verified by running the endpoint against the live stack.
/// </para>
/// </remarks>
public sealed class RecentPatientQueryTests
{
    private static readonly Guid _actor = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid _otherActor = Guid.Parse("99999999-8888-7777-6666-555555555555");

    private static AuditLog AnEvent(
        Guid? actorUserId,
        string action,
        string resourceType,
        string? resourceId,
        int minutesAgo) =>
        new()
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            CorrelationId = "0HNNO6JEVCVNQ:00000001",
            OccurredAtUtc = DateTimeOffset.UnixEpoch.AddMinutes(-minutesAgo),
        };

    private static AuditLog AViewOf(string mrn, int minutesAgo, Guid? actorUserId = null) =>
        AnEvent(actorUserId ?? _actor, AuditAction.ViewPatient, AuditResourceType.Patient, mrn, minutesAgo);

    private static List<string> Run(IEnumerable<AuditLog> logs, Guid? actor = null) =>
        RecentPatientAccessQuery
            .BuildQuery(logs.AsQueryable(), actor ?? _actor, RecentPatientAccess.MaxResults)
            .ToList();

    [Fact]
    public void ReturnsOnlyThePatientsTheCallerOpened()
    {
        List<string> result = Run(
        [
            AViewOf("MINE", minutesAgo: 1),
            AViewOf("THEIRS", minutesAgo: 0, actorUserId: _otherActor),
        ]);

        result.Should().Equal("MINE");
    }

    /// <summary>
    /// A sign-in is not a patient the caller looked at, and its resource id is
    /// not an MRN. Returning one would both be wrong and disclose the audit
    /// table's other contents through an endpoint built for a shortcut list.
    /// </summary>
    [Fact]
    public void NeverReturnsAuthenticationEvents()
    {
        List<string> result = Run(
        [
            AnEvent(_actor, AuditAction.LoginSucceeded, AuditResourceType.Authentication, "admin@example.test", 1),
            AnEvent(_actor, AuditAction.LoginFailed, AuditResourceType.Authentication, "admin@example.test", 2),
        ]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void NeverReturnsStudyInstanceUids()
    {
        List<string> result = Run(
        [
            AnEvent(_actor, AuditAction.ViewStudy, AuditResourceType.Study, "1.2.840.113564.1", 1),
        ]);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// Both halves are asserted separately because either one alone would let a
    /// row through: an action rewritten without its resource type, or a resource
    /// type rewritten without its action.
    /// </summary>
    [Fact]
    public void RequiresTheActionAndTheResourceTypeToAgree()
    {
        List<string> result = Run(
        [
            AnEvent(_actor, AuditAction.ViewPatient, AuditResourceType.Study, "MISMATCHED", 1),
            AnEvent(_actor, AuditAction.ViewStudy, AuditResourceType.Patient, "ALSO-MISMATCHED", 2),
        ]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExcludesEventsWithNoResourceId()
    {
        List<string> result = Run(
        [
            AnEvent(_actor, AuditAction.ViewPatient, AuditResourceType.Patient, null, 1),
            AViewOf("REAL", minutesAgo: 2),
        ]);

        result.Should().Equal("REAL");
    }

    [Fact]
    public void ExcludesEventsWithNoActor()
    {
        List<string> result = Run(
        [
            AnEvent(null, AuditAction.ViewPatient, AuditResourceType.Patient, "ORPHANED", 1),
        ]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void OrdersByTheMostRecentTimeEachPatientWasOpened()
    {
        List<string> result = Run(
        [
            AViewOf("OLDEST", minutesAgo: 30),
            AViewOf("NEWEST", minutesAgo: 1),
            AViewOf("MIDDLE", minutesAgo: 10),
        ]);

        result.Should().Equal("NEWEST", "MIDDLE", "OLDEST");
    }

    /// <summary>
    /// A patient opened five times is one entry, positioned by the latest of
    /// those five - otherwise a single busy morning fills the whole list.
    /// </summary>
    [Fact]
    public void ListsEachPatientOnceAtItsMostRecentPosition()
    {
        List<string> result = Run(
        [
            AViewOf("REVISITED", minutesAgo: 60),
            AViewOf("REVISITED", minutesAgo: 40),
            AViewOf("REVISITED", minutesAgo: 1),
            AViewOf("OTHER", minutesAgo: 5),
        ]);

        result.Should().Equal("REVISITED", "OTHER");
    }

    /// <summary>
    /// The bound is applied in the database, not after materialising. A user who
    /// has worked for a year must not pull a year of audit rows into memory to
    /// render ten links.
    /// </summary>
    [Fact]
    public void ReturnsAtMostTheDeclaredMaximum()
    {
        List<AuditLog> logs = Enumerable
            .Range(0, RecentPatientAccess.MaxResults * 3)
            .Select(index => AViewOf($"MRN-{index}", minutesAgo: index))
            .ToList();

        List<string> result = Run(logs);

        result.Should().HaveCount(RecentPatientAccess.MaxResults);
        result.Should().Equal(Enumerable
            .Range(0, RecentPatientAccess.MaxResults)
            .Select(index => $"MRN-{index}"));
    }

    [Fact]
    public void ReturnsNothingForAUserWhoHasOpenedNobody()
    {
        List<string> result = Run([AViewOf("SOMEONE", minutesAgo: 1)], actor: _otherActor);

        result.Should().BeEmpty();
    }
}
