using CloudFilm.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudFilm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="AuditLog"/> onto <c>cloudfilm."AuditLogs"</c>.
/// </summary>
/// <remarks>
/// Discovered automatically by <c>ApplyConfigurationsFromAssembly</c> in
/// <see cref="CloudFilmDbContext.OnModelCreating"/> - the mechanism that project
/// has had since Sprint 0 and that nothing has used until now.
/// </remarks>
internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AuditLogs");

        builder.HasKey(log => log.Id);

        // Lengths are bounded so a malformed identifier cannot write an
        // unbounded string into a table that is never pruned. 128 comfortably
        // holds a DICOM UID, whose own limit is 64 characters.
        builder.Property(log => log.Action).IsRequired().HasMaxLength(64);
        builder.Property(log => log.ResourceType).IsRequired().HasMaxLength(64);
        builder.Property(log => log.ResourceId).HasMaxLength(128);
        builder.Property(log => log.CorrelationId).HasMaxLength(128);
        builder.Property(log => log.OccurredAtUtc).IsRequired();

        // Four indexes for the four questions an investigation actually asks:
        // what happened recently, what did this account do, who touched this
        // patient or study, and how often does this action occur. No composite
        // beyond (ResourceType, ResourceId), which are always queried together
        // because an id is meaningless without its type.
        builder.HasIndex(log => log.OccurredAtUtc);
        builder.HasIndex(log => log.ActorUserId);
        builder.HasIndex(log => new { log.ResourceType, log.ResourceId });
        builder.HasIndex(log => log.Action);
    }
}
