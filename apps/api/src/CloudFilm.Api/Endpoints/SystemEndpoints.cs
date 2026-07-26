using System.Reflection;
using CloudFilm.Application.Abstractions;

namespace CloudFilm.Api.Endpoints;

/// <summary>
/// Non clinical endpoints describing the running deployment.
/// </summary>
/// <remarks>
/// The portal calls this on start up to confirm which API build it is talking to,
/// which matters when several hospitals run different CloudFilm versions.
/// </remarks>
internal static class SystemEndpoints
{
    /// <summary>
    /// Maps the system information endpoints.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app
            .MapGroup("/api/v1/system")
            .WithTags("System");

        group.MapGet("/info", (IClock clock, IHostEnvironment environment) => Results.Ok(new SystemInfoResponse(
                Name: "CloudFilm API",
                Version: Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
                Environment: environment.EnvironmentName,
                ServerTimeUtc: clock.UtcNow)))
            .WithName("GetSystemInfo")
            .WithSummary("Returns the API build and environment.")
            .AllowAnonymous()
            .Produces<SystemInfoResponse>();

        return app;
    }
}

/// <summary>
/// Describes the running API deployment.
/// </summary>
/// <param name="Name">The service name.</param>
/// <param name="Version">The assembly version.</param>
/// <param name="Environment">The hosting environment name.</param>
/// <param name="ServerTimeUtc">The current server time in UTC.</param>
public sealed record SystemInfoResponse(
    string Name,
    string Version,
    string Environment,
    DateTimeOffset ServerTimeUtc);
