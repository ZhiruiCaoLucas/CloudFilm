using CloudFilm.Application.Analysis;
using CloudFilm.Infrastructure.Analysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudFilm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="StudyAnalysisResult"/> onto <c>cloudfilm."StudyAnalysisResults"</c>.
/// </summary>
/// <remarks>
/// Discovered automatically by <c>ApplyConfigurationsFromAssembly</c>. All
/// mapping lives here rather than in attributes on the entity, which keeps the
/// persisted shape readable in one place.
/// </remarks>
internal sealed class StudyAnalysisResultConfiguration : IEntityTypeConfiguration<StudyAnalysisResult>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<StudyAnalysisResult> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("StudyAnalysisResults");

        builder.HasKey(result => result.Id);

        builder.Property(result => result.StudyInstanceUid)
            .IsRequired()
            .HasMaxLength(AnalysisLimits.MaxStudyInstanceUidLength);

        // The rule that a study has one current result, expressed where the
        // database can enforce it. It is also what the store's upsert conflicts
        // on: without this index the ON CONFLICT clause has nothing to match and
        // the statement fails, so removing it breaks writes rather than quietly
        // admitting duplicates.
        builder.HasIndex(result => result.StudyInstanceUid).IsUnique();

        builder.Property(result => result.Status)
            .IsRequired()
            .HasMaxLength(AnalysisLimits.MaxStatusLength);

        builder.Property(result => result.ModelName)
            .IsRequired()
            .HasMaxLength(AnalysisLimits.MaxModelNameLength);

        builder.Property(result => result.ModelVersion)
            .IsRequired()
            .HasMaxLength(AnalysisLimits.MaxModelVersionLength);

        builder.Property(result => result.CompletedAtUtc).IsRequired();

        // The one jsonb column in CloudFilm. Unbounded by length on purpose: the
        // bound that matters is AnalysisLimits.MaxFindings, which is enforced
        // where the findings are produced, and a character limit here would
        // truncate a valid document into invalid JSON.
        builder.Property(result => result.Findings)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(result => result.CreatedAtUtc).IsRequired();

        // No foreign key on RequestedByUserId, and so no navigation property in
        // either direction: who sent a patient's imaging to another process must
        // outlive the account that did it, exactly as for the audit trail's
        // actor and a share link's issuer.
    }
}
