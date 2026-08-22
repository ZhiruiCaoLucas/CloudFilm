using CloudFilm.Application.Grants;
using CloudFilm.Infrastructure.Grants;
using CloudFilm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudFilm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="AccessGrant"/> onto <c>cloudfilm."AccessGrants"</c>.
/// </summary>
/// <remarks>
/// Discovered automatically by <c>ApplyConfigurationsFromAssembly</c>, like
/// <see cref="AuditLogConfiguration"/> and <see cref="StudyReadStateConfiguration"/>.
/// All mapping lives here rather than in attributes on the entity, which keeps
/// the persisted shape readable in one place.
/// </remarks>
internal sealed class AccessGrantConfiguration : IEntityTypeConfiguration<AccessGrant>
{
    /// <summary>
    /// The rule that makes an active grant unique.
    /// </summary>
    /// <remarks>
    /// Written in PostgreSQL's own quoting because it is emitted verbatim into
    /// the index definition. It is what turns "one live grant per account per
    /// study" from a convention every writer has to remember into something the
    /// database will not store twice - while leaving any number of revoked rows
    /// in place as history.
    /// </remarks>
    private const string _activeGrantFilter = "\"RevokedAtUtc\" IS NULL";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AccessGrant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AccessGrants");

        builder.HasKey(grant => grant.Id);

        builder.Property(grant => grant.StudyInstanceUid)
            .IsRequired()
            .HasMaxLength(GrantLimits.MaxStudyInstanceUidLength);

        builder.Property(grant => grant.Permission)
            .IsRequired()
            .HasMaxLength(GrantLimits.MaxPermissionLength);

        builder.Property(grant => grant.CreatedAtUtc).IsRequired();

        // A partial unique index, not a primary key: the same pair may appear
        // many times over the life of the table, and only one of those rows may
        // be live at a time.
        builder.HasIndex(grant => new { grant.StudyInstanceUid, grant.GranteeUserId })
            .IsUnique()
            .HasFilter(_activeGrantFilter);

        // A grant to an account that no longer exists permits nothing, so it
        // goes with the account - the same choice StudyReadState makes, and the
        // opposite of the audit trail's, which is evidence and must outlive it.
        //
        // GrantedByUserId gets no foreign key at all, for that second reason.
        //
        // No navigation property in either direction: the entity stays a plain
        // row, and ApplicationUser is not given a collection of grants that
        // something could load by accident.
        //
        // EF adds its own index for this foreign key, because GranteeUserId is
        // the trailing column of the unique index above and so is not covered by
        // it. That index is what makes a cascade delete cheap.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(grant => grant.GranteeUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
