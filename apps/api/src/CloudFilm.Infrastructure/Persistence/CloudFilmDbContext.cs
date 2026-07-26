using CloudFilm.Application.Abstractions;
using CloudFilm.Infrastructure.Identity;
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

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasDefaultSchema(Schema);
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(CloudFilmDbContext).Assembly);
    }
}
