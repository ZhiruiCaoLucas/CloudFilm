using CloudFilm.Application.Grants;
using CloudFilm.Infrastructure.Grants;
using FluentAssertions;

namespace CloudFilm.Api.Tests.Grants;

/// <summary>
/// What the grant query treats as live access, and what it refuses to.
/// </summary>
/// <remarks>
/// <para>
/// Runs the real expression tree against an in-memory sequence. That is why
/// <see cref="AccessGrantStore.BuildActiveQuery"/> is a static returning
/// <see cref="IQueryable{T}"/>: the predicate under test here is the same one
/// EF Core translates to SQL, so a filter deleted in production is a failing
/// test rather than a widening of who can see a patient's imaging.
/// </para>
/// <para>
/// What LINQ to Objects cannot vouch for is translation - that PostgreSQL
/// accepts this shape and compares timestamps identically. That is verified
/// against the live stack.
/// </para>
/// </remarks>
public sealed class AccessGrantQueryTests
{
    private static readonly Guid _grantee = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid _otherGrantee = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid _grantedBy = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private const string _study = "1.2.840.113564.145320946818674.1292.636566240007310666.706";
    private const string _otherStudy = "1.2.840.113564.999";

    private static readonly DateTimeOffset _now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private static AccessGrant AGrant(
        Guid? granteeUserId = null,
        string? studyInstanceUid = null,
        string? permission = null,
        DateTimeOffset? expiresAtUtc = null,
        DateTimeOffset? revokedAtUtc = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            StudyInstanceUid = studyInstanceUid ?? _study,
            GranteeUserId = granteeUserId ?? _grantee,
            GrantedByUserId = _grantedBy,
            Permission = permission ?? GrantPermissions.View,
            CreatedAtUtc = _now.AddDays(-1),
            ExpiresAtUtc = expiresAtUtc,
            RevokedAtUtc = revokedAtUtc,
        };

    private static List<AccessGrant> Run(
        IEnumerable<AccessGrant> grants,
        string permission = GrantPermissions.View,
        Guid? grantee = null,
        string? study = null) =>
        AccessGrantStore
            .BuildActiveQuery(
                grants.AsQueryable(),
                grantee ?? _grantee,
                study ?? _study,
                permission,
                _now)
            .ToList();

    // -----------------------------------------------------------------------
    // Live access.
    // -----------------------------------------------------------------------

    [Fact]
    public void FindsAGrantThatStandsAndHasNotExpired()
    {
        List<AccessGrant> found = Run([AGrant(expiresAtUtc: _now.AddHours(1))]);

        found.Should().ContainSingle();
    }

    /// <summary>
    /// No expiry means no expiry - not "expired" and not "unknown". A grant with
    /// no end date is a deliberate choice by whoever made it.
    /// </summary>
    [Fact]
    public void FindsAGrantWithNoExpiry()
    {
        List<AccessGrant> found = Run([AGrant(expiresAtUtc: null)]);

        found.Should().ContainSingle();
    }

    // -----------------------------------------------------------------------
    // Everything that must not count.
    // -----------------------------------------------------------------------

    [Fact]
    public void RefusesAGrantWhoseExpiryHasPassed()
    {
        List<AccessGrant> found = Run([AGrant(expiresAtUtc: _now.AddSeconds(-1))]);

        found.Should().BeEmpty();
    }

    /// <summary>
    /// An expiry of exactly now has passed. A boundary should fall closed.
    /// </summary>
    [Fact]
    public void RefusesAGrantExpiringAtThisVeryInstant()
    {
        List<AccessGrant> found = Run([AGrant(expiresAtUtc: _now)]);

        found.Should().BeEmpty();
    }

    [Fact]
    public void RefusesARevokedGrantEvenWhileItsExpiryIsInTheFuture()
    {
        List<AccessGrant> found = Run(
            [AGrant(expiresAtUtc: _now.AddYears(1), revokedAtUtc: _now.AddMinutes(-5))]);

        found.Should().BeEmpty();
    }

    [Fact]
    public void RefusesAGrantOnADifferentStudy()
    {
        List<AccessGrant> found = Run([AGrant(studyInstanceUid: _otherStudy)]);

        found.Should().BeEmpty();
    }

    [Fact]
    public void RefusesAGrantHeldByADifferentAccount()
    {
        List<AccessGrant> found = Run([AGrant(granteeUserId: _otherGrantee)]);

        found.Should().BeEmpty();
    }

    /// <summary>
    /// The two permissions are separate, not a ladder. Whether Download should
    /// also confer View is a policy question, and this layer deliberately holds
    /// no opinion on it.
    /// </summary>
    [Fact]
    public void RefusesAGrantForADifferentPermission()
    {
        List<AccessGrant> found = Run(
            [AGrant(permission: GrantPermissions.Download)],
            permission: GrantPermissions.View);

        found.Should().BeEmpty();

        List<AccessGrant> reversed = Run(
            [AGrant(permission: GrantPermissions.View)],
            permission: GrantPermissions.Download);

        reversed.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // What comes back.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(GrantPermissions.View)]
    [InlineData(GrantPermissions.Download)]
    public void PreservesThePermissionItWasGranted(string permission)
    {
        List<AccessGrant> found = Run([AGrant(permission: permission)], permission: permission);

        found.Should().ContainSingle().Which.Permission.Should().Be(permission);
    }

    [Fact]
    public void PreservesWhoGrantedItAndWhoHoldsIt()
    {
        AccessGrant granted = Run([AGrant(expiresAtUtc: _now.AddHours(1))])
            .Should().ContainSingle().Subject;

        granted.GranteeUserId.Should().Be(_grantee);
        granted.GrantedByUserId.Should().Be(_grantedBy);
        granted.StudyInstanceUid.Should().Be(_study);
        granted.RevokedAtUtc.Should().BeNull();
    }

    /// <summary>
    /// A revoked grant and a later live one may coexist for the same pair - that
    /// is why the table has a surrogate key and a partial index rather than a
    /// composite primary key. Only the live one may be found.
    /// </summary>
    [Fact]
    public void FindsOnlyTheLiveGrantWhenAnEarlierOneWasRevoked()
    {
        List<AccessGrant> found = Run(
        [
            AGrant(revokedAtUtc: _now.AddDays(-1)),
            AGrant(expiresAtUtc: _now.AddDays(1)),
        ]);

        found.Should().ContainSingle().Which.RevokedAtUtc.Should().BeNull();
    }

    [Fact]
    public void FindsNothingWhenNobodyHasBeenGrantedAnything()
    {
        List<AccessGrant> found = Run([]);

        found.Should().BeEmpty();
    }
}
