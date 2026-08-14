using CloudFilm.Infrastructure.Identity;
using CloudFilm.Infrastructure.ReadState;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using StudyReadStateLimits = CloudFilm.Application.ReadState.StudyReadStateLimits;

/// <summary>
/// Maps <see cref="StudyReadState"/> onto <c>cloudfilm."StudyReadStates"</c>.
/// </summary>
/// <remarks>
/// Discovered automatically by <c>ApplyConfigurationsFromAssembly</c>.
/// All mapping lives here rather than in attributes on the entity, which keeps
/// the persisted shape readable in one place.
/// </remarks>
internal sealed class StudyReadStateConfiguration : IEntityTypeConfiguration<StudyReadState>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<StudyReadState> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("StudyReadStates");

        // The composite key is the uniqueness rule, not merely an identifier:
        // one user cannot hold two marks for one study because the database will
        // not store them. ActorUserId leads because every query filters on it -
        // "has this user read this study" - so the primary key index serves the
        // only access path there is.
        builder.HasKey(state => new { state.ActorUserId, state.StudyInstanceUid });

        builder.Property(state => state.StudyInstanceUid)
            .IsRequired()
            .HasMaxLength(StudyReadStateLimits.MaxStudyInstanceUidLength);

        builder.Property(state => state.MarkedReadAtUtc).IsRequired();

        // Marks belong to an account and mean nothing without it, so deleting the
        // account deletes them. This is the opposite of the audit trail's choice,
        // and deliberately: an audit record is evidence of what an account did
        // and must outlive it, whereas a read mark is a private convenience that
        // nobody will ever consult again once the account is gone.
        //
        // No navigation property in either direction. The entity stays a plain
        // row, and ApplicationUser is not given a collection of read marks that
        // something could load by accident.
        //
        // EF adds no index for this foreign key: ActorUserId is the leading
        // column of the primary key, so the key's own index already covers the
        // lookups a cascade delete performs.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(state => state.ActorUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
