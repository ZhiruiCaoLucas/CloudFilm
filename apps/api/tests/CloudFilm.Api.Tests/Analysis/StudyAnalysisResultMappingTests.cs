using CloudFilm.Application.Analysis;
using CloudFilm.Infrastructure.Analysis;
using CloudFilm.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CloudFilm.Api.Tests.Analysis;

/// <summary>
/// The shape <see cref="StudyAnalysisResult"/> is stored in.
/// </summary>
/// <remarks>
/// Reads the EF Core model rather than a database - building the model opens no
/// connection. This is where the constraints the feature depends on are pinned
/// down: one current result per study, findings in a column PostgreSQL
/// understands, and no foreign key on the account that asked for the analysis.
/// </remarks>
public sealed class StudyAnalysisResultMappingTests
{
    private static IEntityType Entity()
    {
        DbContextOptions<CloudFilmDbContext> options =
            new DbContextOptionsBuilder<CloudFilmDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=cloudfilm;Username=cloudfilm;Password=cloudfilm")
                .Options;

        using CloudFilmDbContext context = new(options);

        return context.Model.FindEntityType(typeof(StudyAnalysisResult))!;
    }

    [Fact]
    public void IsStoredInTheCloudFilmSchema()
    {
        IEntityType entity = Entity();

        entity.GetTableName().Should().Be("StudyAnalysisResults");
        entity.GetSchema().Should().Be(CloudFilmDbContext.Schema);
    }

    [Fact]
    public void IsIdentifiedBySurrogateKey() =>
        Entity().FindPrimaryKey()!.Properties.Select(property => property.Name).Should().Equal("Id");

    /// <summary>
    /// The rule the whole feature rests on. Without it a rerun would leave two
    /// rows for one study and the endpoint would have to choose between them -
    /// and the store's <c>ON CONFLICT</c> would have no conflict target.
    /// </summary>
    [Fact]
    public void HoldsOneCurrentResultPerStudy()
    {
        IIndex? unique = Entity()
            .GetIndexes()
            .SingleOrDefault(index =>
                index.Properties.Count == 1
                && index.Properties[0].Name == nameof(StudyAnalysisResult.StudyInstanceUid));

        unique.Should().NotBeNull("a study's stored analysis is found by its identifier");
        unique!.IsUnique.Should().BeTrue("a study has one current result, and the database says so");
    }

    [Fact]
    public void BoundsTheValuesItStores()
    {
        IEntityType entity = Entity();

        IProperty study = entity.FindProperty(nameof(StudyAnalysisResult.StudyInstanceUid))!;
        study.GetMaxLength().Should().Be(AnalysisLimits.MaxStudyInstanceUidLength);
        study.GetMaxLength().Should().Be(128, "every other table storing a Study Instance UID uses 128");
        study.IsNullable.Should().BeFalse();

        IProperty status = entity.FindProperty(nameof(StudyAnalysisResult.Status))!;
        status.GetMaxLength().Should().Be(AnalysisLimits.MaxStatusLength);
        status.IsNullable.Should().BeFalse();

        IProperty modelName = entity.FindProperty(nameof(StudyAnalysisResult.ModelName))!;
        modelName.GetMaxLength().Should().Be(AnalysisLimits.MaxModelNameLength);
        modelName.IsNullable.Should().BeFalse();

        IProperty modelVersion = entity.FindProperty(nameof(StudyAnalysisResult.ModelVersion))!;
        modelVersion.GetMaxLength().Should().Be(AnalysisLimits.MaxModelVersionLength);
        modelVersion.IsNullable.Should().BeFalse();
    }

    /// <summary>
    /// The one <c>jsonb</c> column in CloudFilm. A findings list is a model's own
    /// vocabulary, differs in shape between models, and is read whole or not at
    /// all - so it is stored as the document it is rather than as a child table
    /// that would need a migration for every future model.
    /// </summary>
    [Fact]
    public void StoresFindingsAsJsonb()
    {
        IProperty findings = Entity().FindProperty(nameof(StudyAnalysisResult.Findings))!;

        findings.GetColumnType().Should().Be("jsonb");
        findings.IsNullable.Should().BeFalse();
        findings.GetMaxLength().Should().BeNull("a length limit would truncate a document into invalid JSON");
    }

    /// <summary>
    /// Who sent a patient's imaging to another process must outlive the account
    /// that did it, exactly as for the audit trail's actor and a share link's
    /// issuer. A foreign key would force a choice between blocking the deletion
    /// and erasing the record, and both are wrong.
    /// </summary>
    [Fact]
    public void DoesNotTieTheRequestingAccountToAForeignKey()
    {
        IEntityType entity = Entity();

        entity.GetForeignKeys().Should().BeEmpty();
        entity.GetNavigations().Should().BeEmpty();

        entity.FindProperty(nameof(StudyAnalysisResult.RequestedByUserId))!
            .IsNullable
            .Should()
            .BeTrue("a validated token carrying no usable subject is a state to record, not one to invent an account for");
    }

    /// <summary>
    /// Two timestamps, and they answer different questions: when CloudFilm first
    /// learned of an analysis for this study, and whether that result has since
    /// been superseded.
    /// </summary>
    [Fact]
    public void DistinguishesWhenAResultArrivedFromWhenItWasReplaced()
    {
        IEntityType entity = Entity();

        entity.FindProperty(nameof(StudyAnalysisResult.CreatedAtUtc))!.IsNullable.Should().BeFalse();
        entity.FindProperty(nameof(StudyAnalysisResult.UpdatedAtUtc))!.IsNullable.Should().BeTrue();
        entity.FindProperty(nameof(StudyAnalysisResult.CompletedAtUtc))!.IsNullable.Should().BeFalse();
    }
}
