using CloudFilm.Api.Configuration;
using CloudFilm.Infrastructure;

namespace CloudFilm.Api.Extensions;

/// <summary>
/// Liveness and readiness probes.
/// </summary>
/// <remarks>
/// Liveness answers "is this process healthy"; readiness additionally answers
/// "can it serve traffic", which requires PostgreSQL, Redis, the Retrieve Service
/// and Orthanc to be reachable. Container orchestrators use the two differently.
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
            .AddUrlGroup(
                new Uri($"{downstream.RetrieveServiceUrl.TrimEnd('/')}/health/live"),
                name: "retrieve-service",
                tags: readiness)
            .AddUrlGroup(
                new Uri($"{downstream.OrthancUrl.TrimEnd('/')}/system"),
                name: "orthanc",
                tags: readiness);

        return builder;
    }
}
