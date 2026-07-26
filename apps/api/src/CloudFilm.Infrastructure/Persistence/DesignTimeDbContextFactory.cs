using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CloudFilm.Infrastructure.Persistence;

/// <summary>
/// Supplies a <see cref="CloudFilmDbContext"/> to the EF Core command line tools.
/// </summary>
/// <remarks>
/// Without this factory <c>dotnet ef migrations add</c> would have to boot the whole
/// API host - including Redis and the Retrieve Service - just to read a connection string.
/// The connection string is taken from <c>CLOUDFILM_DB_CONNECTION</c> and falls back to
/// the local development default.
/// </remarks>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CloudFilmDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=cloudfilm;Username=cloudfilm;Password=cloudfilm";

    /// <inheritdoc />
    public CloudFilmDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("CLOUDFILM_DB_CONNECTION") ?? DefaultConnectionString;

        DbContextOptions<CloudFilmDbContext> options =
            new DbContextOptionsBuilder<CloudFilmDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__migrations", CloudFilmDbContext.Schema))
                .Options;

        return new CloudFilmDbContext(options);
    }
}
