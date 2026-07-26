using CloudFilm.Retrieve.Hospital;

namespace CloudFilm.Retrieve.Endpoints;

/// <summary>
/// Endpoints describing which hospital integrations this deployment can serve.
/// </summary>
internal static class HospitalEndpoints
{
    /// <summary>
    /// Maps the hospital inventory endpoints.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapHospitalEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app
            .MapGroup("/api/v1/hospitals")
            .WithTags("Hospitals");

        group.MapGet("/", (IHospitalAdapterRegistry registry) => Results.Ok(
                registry.Adapters
                    .Select(adapter => new HospitalSummary(adapter.HospitalCode, adapter.DisplayName))
                    .OrderBy(summary => summary.Code, StringComparer.Ordinal)
                    .ToArray()))
            .WithName("ListHospitals")
            .WithSummary("Lists the hospital adapters registered in this deployment.")
            .Produces<HospitalSummary[]>();

        return app;
    }
}

/// <summary>
/// Identifies a hospital integration.
/// </summary>
/// <param name="Code">The stable hospital code.</param>
/// <param name="DisplayName">The human readable hospital name.</param>
public sealed record HospitalSummary(string Code, string DisplayName);
