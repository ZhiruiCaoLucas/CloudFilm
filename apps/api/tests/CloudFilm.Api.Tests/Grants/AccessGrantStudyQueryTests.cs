using CloudFilm.Application.Grants;
using CloudFilm.Infrastructure.Grants;
using FluentAssertions;

namespace CloudFilm.Api.Tests.Grants;

/// <summary>
/// Which grants count as in force on a study.
/// </summary>
/// <remarks>
/// Runs the real expression tree against an in-memory sequence, like the store's
/// own query tests: the predicate here is what decides whether a revoked or
/// expired grant is reported as access, and it is the one that ships to
/// PostgreSQL.
/// </remarks>
public sealed class AccessGrantStudyQueryTests
{
    private const string _study = "1.2.840.113564.1";
    private const string _otherStudy = "1.2.840.113564.999";

    private static readonly DateTimeOffset _now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private static AccessGrant AGrant(
        string? study = null,
        DateTimeOffset? expiresAtUtc = null,
        DateTimeOffset? revokedAtUtc = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            StudyInstanceUid = study ?? _study,
            GranteeUserId = Guid.NewGuid(),
            GrantedByUserId = Guid.NewGuid(),
            Permission = GrantPermissions.View,
            CreatedAtUtc = _now.AddDays(-2),
            ExpiresAtUtc = expiresAtUtc,
            RevokedAtUtc = revokedAtUtc,
        };

    private static List<AccessGrant> Run(IEnumerable<AccessGrant> grants) =>
        AccessGrantQuery.BuildStudyQuery(grants.AsQueryable(), _study, _now).ToList();

    [Fact]
    public void ReportsAGrantThatStands() =>
        Run([AGrant(expiresAtUtc: _now.AddDays(1))]).Should().ContainSingle();

    [Fact]
    public void ReportsAGrantWithNoExpiry() =>
        Run([AGrant(expiresAtUtc: null)]).Should().ContainSingle();

    [Fact]
    public void DoesNotReportAnExpiredGrant() =>
        Run([AGrant(expiresAtUtc: _now.AddSeconds(-1))]).Should().BeEmpty();

    /// <summary>A grant expiring exactly now has expired: the boundary falls closed.</summary>
    [Fact]
    public void DoesNotReportAGrantExpiringAtThisVeryInstant() =>
        Run([AGrant(expiresAtUtc: _now)]).Should().BeEmpty();

    [Fact]
    public void DoesNotReportARevokedGrant() =>
        Run([AGrant(revokedAtUtc: _now.AddHours(-1))]).Should().BeEmpty();

    [Fact]
    public void DoesNotReportAGrantOnAnotherStudy() =>
        Run([AGrant(study: _otherStudy)]).Should().BeEmpty();

    /// <summary>
    /// A revoked grant and a later live one may coexist for the same study. Only
    /// the live one is access.
    /// </summary>
    [Fact]
    public void ReportsOnlyWhatIsStillInForce()
    {
        List<AccessGrant> found = Run(
        [
            AGrant(revokedAtUtc: _now.AddDays(-1)),
            AGrant(expiresAtUtc: _now.AddSeconds(-1)),
            AGrant(expiresAtUtc: _now.AddDays(1)),
        ]);

        found.Should().ContainSingle().Which.RevokedAtUtc.Should().BeNull();
    }
}
