using CloudFilm.Application.Abstractions;
using CloudFilm.Infrastructure.Analysis;
using CloudFilm.Infrastructure.Auditing;
using CloudFilm.Infrastructure.Grants;
using CloudFilm.Infrastructure.Identity;
using CloudFilm.Infrastructure.Sharing;
using CloudFilm.Infrastructure.ReadState;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CloudFilm.Infrastructure.Persistence;

/// <summary>
/// The CloudFilm relational store.
/// </summary>
/// <remarks>
/// Holds ASP.NET Identity tables today. Feature modules add their own entity
/// configurations through <see cref="IEntityTypeConfiguration{TEntity}"/> classes that are
/// discovered automatically by <see cref="OnModelCreating"/>.
/// </remarks>
public sealed class CloudFilmDbContext(DbContextOptions<CloudFilmDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IApplicationDbContext
{
    /// <summary>
    /// The PostgreSQL schema that owns every CloudFilm table.
    /// </summary>
    public const string Schema = "cloudfilm";

    /// <summary>
    /// The clinical access audit trail.
    /// </summary>
    /// <remarks>
    /// Append-only by convention: nothing in CloudFilm updates or deletes an
    /// audit record. The convention is not enforced by the database yet, which
    /// is a known limitation rather than an oversight.
    /// </remarks>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// Which studies each user has marked as read.
    /// </summary>
    /// <remarks>
    /// CloudFilm's own workflow state, owned by no imaging system. A row means
    /// the user has read that study; its absence means they have not. Rows are
    /// inserted and deleted, never updated.
    /// </remarks>
    public DbSet<StudyReadState> StudyReadStates => Set<StudyReadState>();

    /// <summary>
    /// Who may reach which study.
    /// </summary>
    /// <remarks>
    /// Append-only in practice: a grant is stamped revoked rather than deleted,
    /// and its permission is never rewritten, so the table reads as the history
    /// of who was given access to a patient's imaging and by whom.
    /// </remarks>
    public DbSet<AccessGrant> AccessGrants => Set<AccessGrant>();

    /// <summary>
    /// Bearer links that lead to one study each.
    /// </summary>
    /// <remarks>
    /// Holds digests, never tokens. Rows are stamped revoked rather than
    /// deleted, so the table reads as the history of which studies were shared
    /// by URL, by whom, and until when.
    /// </remarks>
    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();

    /// <summary>
    /// The current automated characterisation of each analysed study.
    /// </summary>
    /// <remarks>
    /// One row per study, replaced when the study is analysed again. Unlike the
    /// tables above it holds no record of who was given access to anything, so
    /// nothing is lost by superseding a row - and the audit trail already writes
    /// one append-only entry per run.
    /// </remarks>
    public DbSet<StudyAnalysisResult> StudyAnalysisResults => Set<StudyAnalysisResult>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasDefaultSchema(Schema);
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(CloudFilmDbContext).Assembly);
    }
}
