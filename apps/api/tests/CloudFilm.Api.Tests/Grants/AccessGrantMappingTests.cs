using CloudFilm.Application.Grants;
using CloudFilm.Infrastructure.Grants;
using CloudFilm.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CloudFilm.Api.Tests.Grants;

/// <summary>
/// The shape <see cref="AccessGrant"/> is stored in.
/// </summary>
/// <remarks>
/// <para>
/// Reads the EF Core model rather than a database. Building the model is pure
/// computation - no connection is opened - so these assertions run in the same
/// suite as everything else and still describe the table that will exist.
/// </para>
/// <para>
/// This is where the uniqueness rule is verified as far as it can be without
/// PostgreSQL. That a second live grant is actually rejected is the database's
/// behaviour, and it is checked against the live stack; what is checked here is
/// that CloudFilm asked for the constraint at all - which is the part that could
/// be deleted in a refactor and never noticed.
/// </para>
/// </remarks>
public sealed class AccessGrantMappingTests
{
    private static readonly string[] _uniqueIndexColumns = ["StudyInstanceUid", "GranteeUserId"];

    private static IEntityType Entity()
    {
        DbContextOptions<CloudFilmDbContext> options =
            new DbContextOptionsBuilder<CloudFilmDbContext>()

                // A connection string, not a connection. The model is built from
                // the configuration and the entity classes; nothing here talks to
                // PostgreSQL.
                .UseNpgsql("Host=localhost;Port=5432;Database=cloudfilm;Username=cloudfilm;Password=cloudfilm")
                .Options;

        using CloudFilmDbContext context = new(options);

        return context.Model.FindEntityType(typeof(AccessGrant))!;
    }

    [Fact]
    public void IsStoredInTheCloudFilmSchema()
    {
        IEntityType entity = Entity();

        entity.GetTableName().Should().Be("AccessGrants");
        entity.GetSchema().Should().Be(CloudFilmDbContext.Schema);
    }

    /// <summary>
    /// A surrogate key, because the pair is not unique over the life of the
    /// table: revoked grants stay, and the same person may be granted access
    /// again later.
    /// </summary>
    [Fact]
    public void IsIdentifiedBySurrogateKey() =>
        Entity().FindPrimaryKey()!.Properties.Select(property => property.Name)
            .Should().Equal("Id");

    [Fact]
    public void BoundsTheIdentifiersItStores()
    {
        IEntityType entity = Entity();

        IProperty study = entity.FindProperty(nameof(AccessGrant.StudyInstanceUid))!;
        study.GetMaxLength().Should().Be(GrantLimits.MaxStudyInstanceUidLength);
        study.GetMaxLength().Should().Be(128, "the audit trail and the read-state table already use 128");
        study.IsNullable.Should().BeFalse();

        IProperty permission = entity.FindProperty(nameof(AccessGrant.Permission))!;
        permission.GetMaxLength().Should().Be(GrantLimits.MaxPermissionLength);
        permission.IsNullable.Should().BeFalse();
    }

    /// <summary>
    /// Null means "no expiry" and "still standing" respectively, so both columns
    /// have to be able to hold it.
    /// </summary>
    [Fact]
    public void LetsAGrantHaveNoExpiryAndNoRevocation()
    {
        IEntityType entity = Entity();

        entity.FindProperty(nameof(AccessGrant.ExpiresAtUtc))!.IsNullable.Should().BeTrue();
        entity.FindProperty(nameof(AccessGrant.RevokedAtUtc))!.IsNullable.Should().BeTrue();
        entity.FindProperty(nameof(AccessGrant.CreatedAtUtc))!.IsNullable.Should().BeFalse();
    }

    /// <summary>
    /// One live grant per account per study, enforced by the database rather
    /// than by every writer remembering to check - while any number of revoked
    /// rows remain as history.
    /// </summary>
    [Fact]
    public void AllowsOnlyOneLiveGrantPerAccountPerStudy()
    {
        IIndex unique = Entity().GetIndexes().Should().ContainSingle(index => index.IsUnique).Subject;

        unique.Properties.Select(property => property.Name).Should().Equal(_uniqueIndexColumns);
        unique.GetFilter().Should().Be("\"RevokedAtUtc\" IS NULL");
    }

    /// <summary>
    /// A grant to an account that no longer exists permits nothing, so it goes
    /// with the account.
    /// </summary>
    [Fact]
    public void BelongsToTheAccountItWasGrantedTo()
    {
        IForeignKey foreignKey = Entity().GetForeignKeys().Should().ContainSingle().Subject;

        foreignKey.Properties.Select(property => property.Name)
            .Should().Equal(nameof(AccessGrant.GranteeUserId));
        foreignKey.PrincipalEntityType.GetTableName().Should().Be("AspNetUsers");
        foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    /// <summary>
    /// Who opened a patient's imaging to somebody else must outlive the account
    /// that did it, so the granting account is recorded without a foreign key -
    /// the same choice the audit trail's actor makes.
    /// </summary>
    [Fact]
    public void KeepsWhoGrantedItEvenIfThatAccountIsDeleted()
    {
        IEntityType entity = Entity();

        entity.FindProperty(nameof(AccessGrant.GrantedByUserId)).Should().NotBeNull();

        entity.GetForeignKeys()
            .SelectMany(foreignKey => foreignKey.Properties)
            .Select(property => property.Name)
            .Should().NotContain(nameof(AccessGrant.GrantedByUserId));
    }
}
