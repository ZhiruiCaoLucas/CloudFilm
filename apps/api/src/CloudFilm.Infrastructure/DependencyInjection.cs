using CloudFilm.Application.Abstractions;
using CloudFilm.Infrastructure.Analysis;
using CloudFilm.Infrastructure.Access;
using CloudFilm.Infrastructure.Auditing;
using CloudFilm.Infrastructure.Caching;
using CloudFilm.Infrastructure.Configuration;
using CloudFilm.Infrastructure.Grants;
using CloudFilm.Infrastructure.Identity;
using CloudFilm.Infrastructure.Imaging;
using CloudFilm.Infrastructure.Persistence;
using CloudFilm.Infrastructure.Persistence.Interceptors;
using CloudFilm.Infrastructure.Sharing;
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
    /// How long one analysis may take, end to end.
    /// </summary>
    /// <remarks>
    /// Five minutes: the request carries a whole study to the analyser and waits
    /// for an answer, so it is bounded by the upload rather than by the work. It
    /// is deliberately finite - the download client's infinite timeout exists
    /// because a body being streamed to a browser must not be cut off mid-study,
    /// and none of that applies to a call that either produces a result or does
    /// not.
    /// </remarks>
    private static readonly TimeSpan _analysisTimeout = TimeSpan.FromMinutes(5);

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

        // The stored analysis of a study. Registered unconditionally, unlike
        // IStudyAnalyser above it: a deployment that has since removed its
        // analysis service must still be able to show the results it produced
        // while it had one, and reading them needs no service to be running.
        services.AddScoped<IStudyAnalysisResultStore, StudyAnalysisResultStore>();

        // Resource-level access grants. Scoped for the same reason: a grant and
        // the audit record of it must be able to travel in one transaction.
        services.AddScoped<IAccessGrantStore, AccessGrantStore>();

        // The read half, registered separately so code that only asks "may this
        // account reach this study" never receives the ability to answer yes.
        services.AddScoped<IAccessGrantQuery, AccessGrantQuery>();

        // Turns roles and grants into "how much of this study may this caller
        // see", before anything is read from the archive.
        services.AddScoped<IStudyAccessPolicy, StudyAccessPolicy>();

        // Share links. Scoped like the rest, so issuing or withdrawing one and
        // the audit record of it can travel in a single transaction.
        services.AddScoped<IShareLinkStore, ShareLinkStore>();

        // Viewer sessions live in the cache, which is a singleton, but the store
        // is scoped like everything else so a future dependency on the request's
        // DbContext does not become a captive dependency.
        services.AddScoped<IViewerSessionStore, ViewerSessionStore>();

        // Read-only, and asked once per DICOMweb request by the imaging gateway.
        services.AddScoped<IShareLinkStatus, ShareLinkStatusQuery>();

        // Existence checks for the account a grant names. Scoped, because
        // UserManager is.
        services.AddScoped<IUserDirectory, IdentityUserDirectory>();

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

        // Bulk retrieval gets its own typed client, and deliberately not the
        // thirty second timeout the others carry: HttpClient.Timeout bounds the
        // body as well as the headers, so a study large enough to be worth
        // downloading would be cut off mid-transfer. This one is bounded by the
        // request's cancellation instead.
        services.AddHttpClient<IStudyDownloadSource, RetrieveStudyDownload>(client =>
        {
            client.BaseAddress = new Uri($"{retrieveServiceUrl}/");
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        // The analysis service, when a deployment has one. Registered only when it
        // is configured: a stack without an analyser is a complete CloudFilm, and
        // the alternative to absence would be a client pointed at nothing that
        // fails on the first study rather than at start-up. A caller that needs
        // this port asks for it optionally and reports that the capability is not
        // configured - it never falls back to a default address.
        string analysisServiceUrl =
            configuration["Downstream:AnalysisServiceUrl"]?.Trim().TrimEnd('/') ?? string.Empty;

        if (analysisServiceUrl.Length > 0)
        {
            services.AddHttpClient<IStudyAnalyser, StudyAnalyser>(client =>
            {
                client.BaseAddress = new Uri($"{analysisServiceUrl}/");

                // Longer than the thirty seconds a metadata call gets, and finite
                // unlike a download. This one request carries a whole study up to
                // the analyser and waits for it to be read: the upload alone can
                // take minutes on a hospital network. It is bounded anyway,
                // because an analyser that has stopped answering must not hold a
                // request open indefinitely - and because HttpClient.Timeout is
                // the only bound that covers the body as well as the headers.
                client.Timeout = _analysisTimeout;
            });
        }

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
