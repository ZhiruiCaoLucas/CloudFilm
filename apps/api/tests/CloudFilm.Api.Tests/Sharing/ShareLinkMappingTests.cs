using CloudFilm.Application.Sharing;
using CloudFilm.Infrastructure.Persistence;
using CloudFilm.Infrastructure.Sharing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CloudFilm.Api.Tests.Sharing;

/// <summary>
/// The shape <see cref="ShareLink"/> is stored in.
/// </summary>
/// <remarks>
/// Reads the EF Core model rather than a database - building the model opens no
/// connection. This is where the constraints that matter to a bearer credential
/// are pinned down: a unique digest, a mandatory expiry, and no foreign key on
/// the account that issued it.
/// </remarks>
public sealed class ShareLinkMappingTests
{
    private static IEntityType Entity()
    {
        DbContextOptions<CloudFilmDbContext> options =
            new DbContextOptionsBuilder<CloudFilmDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=cloudfilm;Username=cloudfilm;Password=cloudfilm")
                .Options;

        using CloudFilmDbContext context = new(options);

        return context.Model.FindEntityType(typeof(ShareLink))!;
    }

    [Fact]
    public void IsStoredInTheCloudFilmSchema()
    {
        IEntityType entity = Entity();

        entity.GetTableName().Should().Be("ShareLinks");
        entity.GetSchema().Should().Be(CloudFilmDbContext.Schema);
    }

    [Fact]
    public void IsIdentifiedBySurrogateKey() =>
        Entity().FindPrimaryKey()!.Properties.Select(property => property.Name).Should().Equal("Id");

    [Fact]
    public void BoundsTheValuesItStores()
    {
        IEntityType entity = Entity();

        IProperty study = entity.FindProperty(nameof(ShareLink.StudyInstanceUid))!;
        study.GetMaxLength().Should().Be(ShareLinkLimits.MaxStudyInstanceUidLength);
        study.GetMaxLength().Should().Be(128, "every other table storing a Study Instance UID uses 128");
        study.IsNullable.Should().BeFalse();

        IProperty permission = entity.FindProperty(nameof(ShareLink.Permission))!;
        permission.GetMaxLength().Should().Be(ShareLinkLimits.MaxPermissionLength);
        permission.IsNullable.Should().BeFalse();

        IProperty tokenHash = entity.FindProperty(nameof(ShareLink.TokenHash))!;
        tokenHash.GetMaxLength().Should().Be(ShareLinkLimits.TokenHashLength);
        tokenHash.GetMaxLength().Should().Be(32, "SHA-256 is 32 bytes");
        tokenHash.IsNullable.Should().BeFalse();
    }

    /// <summary>
    /// A link handed to whoever ends up holding the URL must die on its own. A
    /// nullable expiry would let one be created that never does.
    /// </summary>
    [Fact]
    public void RequiresAnExpiryAndAllowsARevocation()
    {
        IEntityType entity = Entity();

        entity.FindProperty(nameof(ShareLink.ExpiresAtUtc))!.IsNullable.Should().BeFalse();
        entity.FindProperty(nameof(ShareLink.CreatedAtUtc))!.IsNullable.Should().BeFalse();
        entity.FindProperty(nameof(ShareLink.RevokedAtUtc))!.IsNullable.Should().BeTrue();
    }

    /// <summary>
    /// Unique across the whole table, not only among live links: a revoked
    /// token must never become re-issuable, and a digest collision must be
    /// rejected whatever state the older row is in.
    /// </summary>
    [Fact]
    public void AllowsOneRowPerTokenDigest()
    {
        IIndex unique = Entity().GetIndexes().Should().ContainSingle(index => index.IsUnique).Subject;

        unique.Properties.Select(property => property.Name).Should().Equal(nameof(ShareLink.TokenHash));
        unique.GetFilter().Should().BeNull("a withdrawn token must stay unusable, so the rule covers every row");
    }

    [Fact]
    public void IndexesTheStudySoItsLinksCanBeListed()
    {
        IIndex studyIndex = Entity().GetIndexes()
            .Should().ContainSingle(index =>
                !index.IsUnique
                && index.Properties.Any(property => property.Name == nameof(ShareLink.StudyInstanceUid)))
            .Subject;

        studyIndex.Properties.Select(property => property.Name)
            .Should().Equal(nameof(ShareLink.StudyInstanceUid));
    }

    /// <summary>
    /// Who opened a patient's imaging to a URL must outlive the account that did
    /// it, so the issuing account is recorded without a foreign key - the same
    /// choice the audit trail's actor and AccessGrant.GrantedByUserId make.
    /// </summary>
    [Fact]
    public void KeepsWhoIssuedItEvenIfThatAccountIsDeleted()
    {
        IEntityType entity = Entity();

        entity.FindProperty(nameof(ShareLink.CreatedByUserId)).Should().NotBeNull();
        entity.GetForeignKeys().Should().BeEmpty("a share link belongs to nobody, and its issuer is a record");
    }
}
