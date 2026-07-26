using CloudFilm.Retrieve.Hospital;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CloudFilm.Retrieve.Health;

/// <summary>
/// Reports readiness of every registered hospital adapter.
/// </summary>
/// <remarks>
/// A deployment with no adapters configured is healthy but not yet useful; that
/// state is reported as <see cref="HealthStatus.Degraded"/> so an operator can see
/// the difference between "not configured" and "configured but broken".
/// </remarks>
internal sealed class HospitalAdapterHealthCheck(IHospitalAdapterRegistry registry) : IHealthCheck
{
    private readonly IHospitalAdapterRegistry _registry = registry;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_registry.Adapters.Count == 0)
        {
            return HealthCheckResult.Degraded("No hospital adapter is configured.");
        }

        Dictionary<string, object> data = [];
        List<string> unreachable = [];

        foreach (IHospitalAdapter adapter in _registry.Adapters)
        {
            HospitalConnectivity connectivity = await adapter
                .ProbeAsync(cancellationToken)
                .ConfigureAwait(false);

            data[adapter.HospitalCode] = connectivity.Detail;

            if (!connectivity.IsReachable)
            {
                unreachable.Add(adapter.HospitalCode);
            }
        }

        return unreachable.Count == 0
            ? HealthCheckResult.Healthy("All hospital adapters are reachable.", data)
            : HealthCheckResult.Unhealthy(
                $"Unreachable hospital adapters: {string.Join(", ", unreachable)}.",
                data: data);
    }
}
