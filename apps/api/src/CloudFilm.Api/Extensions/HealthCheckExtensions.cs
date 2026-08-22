using CloudFilm.Api.Configuration;
using CloudFilm.Infrastructure;

namespace CloudFilm.Api.Extensions;

/// <summary>
/// Liveness and readiness probes.
/// </summary>
/// <remarks>
/// Liveness answers "is this process healthy"; readiness additionally answers
/// "can it serve traffic", which requires PostgreSQL, Redis and the Retrieve
/// Service to be reachable - and, through Retrieve's own readiness, Orthanc.
/// Container orchestrators use the two differently.
/// </remarks>
internal static class HealthCheckExtensions
{
    /// <summary>
    /// The tag applied to checks that gate readiness.
    /// </summary>
    public const string ReadinessTag = "ready";

    /// <summary>
    /// Registers the CloudFilm health checks.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static WebApplicationBuilder AddCloudFilmHealthChecks(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<DownstreamOptions>()
            .Bind(builder.Configuration.GetSection(DownstreamOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        InfrastructureOptions infrastructure = builder.Configuration
            .GetSection(InfrastructureOptions.SectionName)
            .Get<InfrastructureOptions>() ?? new InfrastructureOptions();

        DownstreamOptions downstream = builder.Configuration
            .GetSection(DownstreamOptions.SectionName)
            .Get<DownstreamOptions>() ?? new DownstreamOptions();

        string[] readiness = [ReadinessTag];

        builder.Services.AddHealthChecks()
            .AddNpgSql(infrastructure.PostgresConnectionString, name: "postgres", tags: readiness)
            .AddRedis(infrastructure.RedisConnectionString, name: "redis", tags: readiness)
            // The Retrieve Service's readiness, not its liveness.
            //
            // /health/live registers no checks at all - it answers 200 as long as
            // the process is listening - so probing it proved only that a
            // container was up. /health/ready runs that service's Orthanc probe,
            // which carries the credential Retrieve holds.
            //
            // This is also why the API no longer probes Orthanc itself. It did,
            // anonymously, against /system; Orthanc answers 401 to that whenever
            // authentication is on, so API readiness could never be healthy in a
            // deployment that was not running the development overlay. The API
            // has no Orthanc credential and must not be given one - Retrieve is
            // the only component permitted to reach the imaging tier - so the
            // check belongs behind the service that owns the credential.
            //
            // Nothing is masked by the move. An unreachable Orthanc fails
            // Retrieve's adapter probe, which fails its readiness, which fails
            // this check.
            .AddUrlGroup(
                new Uri($"{downstream.RetrieveServiceUrl.TrimEnd('/')}/health/ready"),
                name: "retrieve-service",
                tags: readiness);

        return builder;
    }
}
