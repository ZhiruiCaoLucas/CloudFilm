using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CloudFilm.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations at start-up.
/// </summary>
/// <remarks>
/// <para>
/// Registered only when <c>Infrastructure:AutoMigrate</c> is set, which is true
/// for the Compose stack so the database provisions itself, and must be false in
/// a hospital deployment, where schema changes belong to a controlled release.
/// </para>
/// <para>
/// This previously called <c>EnsureCreatedAsync</c>. That creates the schema
/// straight from the model and writes no migrations history, so the schema could
/// never be evolved afterwards - the two mechanisms are mutually exclusive, and
/// EF refuses to migrate a database it did not migrate into existence. Identity,
/// the access audit and every later feature need a versioned schema, so the
/// migrator applies migrations and nothing else.
/// </para>
/// <para>
/// The history table is <c>cloudfilm.__migrations</c>, configured identically
/// here (through <c>AddCloudFilmInfrastructure</c>) and in
/// <see cref="DesignTimeDbContextFactory"/>, so the schema the tools generate and
/// the schema the application applies cannot diverge.
/// </para>
/// </remarks>
internal sealed class DatabaseMigrator(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseMigrator> logger) : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<DatabaseMigrator> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<CloudFilmDbContext>();

        _logger.LogInformation("Applying pending database migrations...");

        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Database is ready.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
