using CloudFilm.Application.Abstractions;
using CloudFilm.Infrastructure.Access;
using CloudFilm.Infrastructure.Auditing;
using CloudFilm.Infrastructure.Caching;
using CloudFilm.Infrastructure.Configuration;
using CloudFilm.Infrastructure.Identity;
using CloudFilm.Infrastructure.Imaging;
using CloudFilm.Infrastructure.Persistence;
using CloudFilm.Infrastructure.Persistence.Interceptors;
using CloudFilm.Infrastructure.ReadState;
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

            // AddIdentityCore does not register a SignInManager, and the lockout
            // and confirmed-e-mail options above are enforced by the sign-in
            // manager alone. Without this they are configuration that never runs.
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // SignInManager takes an IHttpContextAccessor. AddSignInManager registers
        // one, but stating it here means a future change to that call cannot
        // remove the dependency silently.
        services.AddHttpContextAccessor();

        // Token settings are bound once, here, and validated on start-up. The API
        // project reads the same section to configure bearer validation, so the
        // key that signs a token and the key that validates it cannot drift.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SeedOptions>()
            .Bind(configuration.GetSection(SeedOptions.SectionName));

        // Scoped: it shares the request's DbContext, so an audit write joins the
        // same connection and the same unit of work as everything else in that
        // request rather than opening a second one.
        services.AddScoped<IAuditLogService, AuditLogService>();

        // Reads the audit trail; shares the request's DbContext like the writer.
        services.AddScoped<IRecentPatientAccess, RecentPatientAccessQuery>();

        // CloudFilm-owned clinical workflow state. Scoped, so it shares the
        // request's DbContext with the audit writer and both can be enclosed in
        // one transaction.
        services.AddScoped<IStudyReadStateStore, StudyReadStateStore>();

        // The transaction boundary itself. Scoped for the same reason: it must
        // open its transaction on the same context the writers are using, or it
        // would enclose nothing.
        services.AddScoped<IUnitOfWork, TransactionalUnitOfWork>();

        services.AddScoped<IAuthenticationService, IdentityAuthenticationService>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        services.AddStackExchangeRedisCache(redis =>
        {
            redis.Configuration = options.RedisConnectionString;
            redis.InstanceName = options.RedisInstanceName;
        });
        services.AddSingleton<ICacheStore, RedisCacheStore>();

        // Imaging reads go through the Retrieve Service, which is the only
        // component permitted to reach an imaging source. The address is read
        // from the existing Downstream section rather than a new setting, so no
        // deployment configuration changes.
        string retrieveServiceUrl =
            configuration["Downstream:RetrieveServiceUrl"]?.TrimEnd('/') ?? string.Empty;

        void ConfigureRetrieveClient(HttpClient client)
        {
            client.BaseAddress = new Uri($"{retrieveServiceUrl}/");
            client.Timeout = TimeSpan.FromSeconds(30);
        }

        services.AddHttpClient<IImagingCatalog, RetrieveImagingCatalog>(ConfigureRetrieveClient);

        // Registered as its own typed client rather than resolved by casting the
        // one above. Both are the same class over the same address, but a cast
        // would make search depend on which implementation happens to be
        // registered for IImagingCatalog - and a test that substitutes that port
        // would turn search into an InvalidCastException at run time.
        services.AddHttpClient<IStudySearchCatalog, RetrieveImagingCatalog>(ConfigureRetrieveClient);

        if (options.AutoMigrate)
        {
            services.AddHostedService<DatabaseMigrator>();
        }

        // Registered after the migrator: hosted services start in registration
        // order, and seeding a user into tables that do not exist yet would fail.
        services.AddHostedService<IdentitySeeder>();

        return services;
    }
}
