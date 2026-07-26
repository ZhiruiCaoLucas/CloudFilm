using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CloudFilm.Infrastructure.Persistence;

/// <summary>
/// Applies outstanding EF Core migrations at start up when explicitly enabled.
/// </summary>
/// <remarks>
/// Enabled by <c>Database:AutoMigrate</c>. It is switched on for the Docker Compose
/// stack so that <c>docker compose up</c> yields a working database with no manual
/// step, and switched off for hospital production deployments where schema changes
/// are applied deliberately as part of a controlled release.
/// </remarks>
internal sealed class DatabaseMigrator(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseMigrator> logger) : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<DatabaseMigrator> _logger = logger;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        CloudFilmDbContext context = scope.ServiceProvider.GetRequiredService<CloudFilmDbContext>();

        _logger.LogInformation("Applying database migrations.");
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Database migrations applied.");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
