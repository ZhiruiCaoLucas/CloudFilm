using CloudFilm.Application.Sharing;
using CloudFilm.Infrastructure.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudFilm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="ShareLink"/> onto <c>cloudfilm."ShareLinks"</c>.
/// </summary>
/// <remarks>
/// Discovered automatically by <c>ApplyConfigurationsFromAssembly</c>, like
/// <see cref="AuditLogConfiguration"/>, <see cref="StudyReadStateConfiguration"/>
/// and <see cref="AccessGrantConfiguration"/>. All mapping lives here rather
/// than in attributes on the entity.
/// </remarks>
internal sealed class ShareLinkConfiguration : IEntityTypeConfiguration<ShareLink>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ShareLink> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ShareLinks");

        builder.HasKey(link => link.Id);

        builder.Property(link => link.StudyInstanceUid)
            .IsRequired()
            .HasMaxLength(ShareLinkLimits.MaxStudyInstanceUidLength);

        builder.Property(link => link.Permission)
            .IsRequired()
            .HasMaxLength(ShareLinkLimits.MaxPermissionLength);

        // Declared with its length even though PostgreSQL's bytea does not
        // enforce one. It documents what is written, and it is what a provider
        // with a fixed-length binary type would use.
        builder.Property(link => link.TokenHash)
            .IsRequired()
            .HasMaxLength(ShareLinkLimits.TokenHashLength);

        builder.Property(link => link.CreatedAtUtc).IsRequired();

        // Required, unlike an access grant's expiry. A link that never dies is
        // not something an operator should be able to create by omission.
        builder.Property(link => link.ExpiresAtUtc).IsRequired();

        // Unique across the whole table, not only among live links: a digest
        // collision must be rejected whatever state the older row is in, and a
        // revoked token must never be re-issuable.
        builder.HasIndex(link => link.TokenHash).IsUnique();

        // For listing the links issued against one study - which is how an
        // administrator finds the one to withdraw.
        builder.HasIndex(link => link.StudyInstanceUid);

        // CreatedByUserId gets no foreign key. Who opened a patient's imaging to
        // a URL must outlive the account that did it, exactly as with the audit
        // trail's actor and AccessGrant.GrantedByUserId.
    }
}
