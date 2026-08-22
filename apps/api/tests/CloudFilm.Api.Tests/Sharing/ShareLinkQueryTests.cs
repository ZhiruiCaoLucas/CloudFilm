using CloudFilm.Application.Grants;
using CloudFilm.Infrastructure.Sharing;
using FluentAssertions;

namespace CloudFilm.Api.Tests.Sharing;

/// <summary>
/// When a share link still works.
/// </summary>
/// <remarks>
/// <para>
/// Runs the real expression tree against an in-memory sequence, like the grant
/// queries: the predicate under test is the one EF Core translates, so a filter
/// deleted here fails a test rather than quietly turning an expired or revoked
/// link back on.
/// </para>
/// <para>
/// One caveat, stated rather than hidden: <c>byte[]</c> equality is reference
/// equality in LINQ to Objects and value equality in SQL. These tests therefore
/// pin down the expiry and revocation rules - which is where the security lives
/// - and pass the same array instance for a match. That a different array with
/// the same bytes matches in PostgreSQL is the provider's behaviour and is
/// verified against the live stack.
/// </para>
/// </remarks>
public sealed class ShareLinkQueryTests
{
    private const string _study = "1.2.840.113564.1";

    private static readonly DateTimeOffset _now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] _tokenHash = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();

    private static readonly byte[] _otherTokenHash = Enumerable.Repeat((byte)0xFF, 32).ToArray();

    private static ShareLink ALink(
        byte[]? tokenHash = null,
        DateTimeOffset? expiresAtUtc = null,
        DateTimeOffset? revokedAtUtc = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            StudyInstanceUid = _study,
            Permission = GrantPermissions.Download,
            TokenHash = tokenHash ?? _tokenHash,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAtUtc = _now.AddDays(-1),
            ExpiresAtUtc = expiresAtUtc ?? _now.AddDays(1),
            RevokedAtUtc = revokedAtUtc,
        };

    private static List<ShareLink> Run(IEnumerable<ShareLink> links, byte[]? presented = null) =>
        ShareLinkStore
            .BuildActiveQuery(links.AsQueryable(), presented ?? _tokenHash, _now)
            .ToList();

    [Fact]
    public void ResolvesALinkThatHasNotExpiredOrBeenWithdrawn() =>
        Run([ALink()]).Should().ContainSingle();

    [Fact]
    public void DoesNotResolveAnExpiredLink() =>
        Run([ALink(expiresAtUtc: _now.AddSeconds(-1))]).Should().BeEmpty();

    /// <summary>
    /// A link expiring exactly now has expired. A boundary in a bearer
    /// credential falls closed.
    /// </summary>
    [Fact]
    public void DoesNotResolveALinkExpiringAtThisVeryInstant() =>
        Run([ALink(expiresAtUtc: _now)]).Should().BeEmpty();

    /// <summary>
    /// Revocation is how a leaked URL is stopped, so it has to take effect on
    /// the next request rather than at the next expiry.
    /// </summary>
    [Fact]
    public void DoesNotResolveARevokedLinkEvenWhileItsExpiryIsInTheFuture() =>
        Run([ALink(expiresAtUtc: _now.AddYears(1), revokedAtUtc: _now.AddMinutes(-5))]).Should().BeEmpty();

    [Fact]
    public void DoesNotResolveADigestItWasNotAskedAbout() =>
        Run([ALink(tokenHash: _otherTokenHash)]).Should().BeEmpty();

    [Fact]
    public void ResolvesNothingWhenNoLinkHasBeenIssued() =>
        Run([]).Should().BeEmpty();

    /// <summary>
    /// Expired and revoked rows stay in the table as the record of what was
    /// shared. Only the live one resolves.
    /// </summary>
    [Fact]
    public void ResolvesOnlyTheLinkThatStillWorks()
    {
        List<ShareLink> resolved = Run(
        [
            ALink(expiresAtUtc: _now.AddSeconds(-1)),
            ALink(revokedAtUtc: _now.AddHours(-1)),
            ALink(expiresAtUtc: _now.AddDays(2)),
        ]);

        resolved.Should().ContainSingle();
        resolved[0].RevokedAtUtc.Should().BeNull();
        resolved[0].ExpiresAtUtc.Should().BeAfter(_now);
    }

    [Fact]
    public void CarriesTheStudyAndPermissionItWasIssuedFor()
    {
        ShareLink resolved = Run([ALink()]).Should().ContainSingle().Subject;

        resolved.StudyInstanceUid.Should().Be(_study);
        resolved.Permission.Should().Be(GrantPermissions.Download);
    }
}
