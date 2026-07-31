using CloudFilm.Application.Abstractions;
using CloudFilm.Infrastructure.Caching;
using CloudFilm.Infrastructure.Identity;
using CloudFilm.Infrastructure.Imaging;
using CloudFilm.Infrastructure.Persistence;
using CloudFilm.Infrastructure.Persistence.Interceptors;
using CloudFilm.Infrastructure.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CloudFilm.Infrastructure;

/// <summary>
/// Composition root helpers for the infrastructure layer.
/// </summary>
/// <remarks>
/// The host knows only this single entry point. Swapping PostgreSQL or Redis for a
/// different technology is a change confined to this project.
/// </remarks>
public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence, caching, identity storage and supporting services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection, to allow chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<InfrastructureOptions>()
            .Bind(configuration.GetSection(InfrastructureOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        InfrastructureOptions options = configuration
            .GetSection(InfrastructureOptions.SectionName)
            .Get<InfrastructureOptions>() ?? new InfrastructureOptions();

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<CloudFilmDbContext>((provider, builder) =>
            builder
                .UseNpgsql(
                    options.PostgresConnectionString,
                    npgsql => npgsql.MigrationsHistoryTable("__migrations", CloudFilmDbContext.Schema))
                .AddInterceptors(provider.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<CloudFilmDbContext>());

        services.AddIdentityCore<ApplicationUser>(identity =>
            {
                identity.User.RequireUniqueEmail = true;
                identity.Password.RequiredLength = 12;
                identity.Lockout.MaxFailedAccessAttempts = 5;
                identity.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<CloudFilmDbContext>()
            .AddDefaultTokenProviders();

        services.AddStackExchangeRedisCache(redis =>
        {
            redis.Configuration = options.RedisConnectionString;
            redis.InstanceName = options.RedisInstanceName;
        });
        services.AddSingleton<ICacheStore, RedisCacheStore>();

        // The single line the next sprint replaces: swap this registration for a
        // catalog backed by the Retrieve Service and no caller changes.
        services.AddSingleton<IImagingCatalog, MockImagingCatalog>();

        if (options.AutoMigrate)
        {
            services.AddHostedService<DatabaseMigrator>();
        }

        return services;
    }
}
