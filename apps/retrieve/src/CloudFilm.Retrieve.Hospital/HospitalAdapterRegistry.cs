using Microsoft.Extensions.DependencyInjection;

namespace CloudFilm.Retrieve.Hospital;

/// <summary>
/// The default <see cref="IHospitalAdapterRegistry"/>, built from every
/// <see cref="IHospitalAdapter"/> registered in the container.
/// </summary>
/// <remarks>
/// Duplicate hospital codes are rejected at construction time rather than producing
/// a non deterministic routing decision at request time.
/// </remarks>
internal sealed class HospitalAdapterRegistry : IHospitalAdapterRegistry
{
    private readonly Dictionary<string, IHospitalAdapter> _adaptersByCode;

    /// <summary>
    /// Initialises a new instance of the <see cref="HospitalAdapterRegistry"/> class.
    /// </summary>
    /// <param name="adapters">Every adapter registered in the container.</param>
    /// <exception cref="InvalidOperationException">Two adapters declare the same hospital code.</exception>
    public HospitalAdapterRegistry(IEnumerable<IHospitalAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        _adaptersByCode = new Dictionary<string, IHospitalAdapter>(StringComparer.OrdinalIgnoreCase);

        foreach (IHospitalAdapter adapter in adapters)
        {
            if (!_adaptersByCode.TryAdd(adapter.HospitalCode, adapter))
            {
                throw new InvalidOperationException(
                    $"More than one hospital adapter is registered for code '{adapter.HospitalCode}'.");
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IHospitalAdapter> Adapters => _adaptersByCode.Values;

    /// <inheritdoc />
    public IHospitalAdapter? Find(string hospitalCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hospitalCode);

        return _adaptersByCode.GetValueOrDefault(hospitalCode);
    }
}

/// <summary>
/// Registration helpers for hospital adapters.
/// </summary>
public static class HospitalAdapterServiceCollectionExtensions
{
    /// <summary>
    /// Registers the hospital adapter registry.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, to allow chaining.</returns>
    /// <remarks>
    /// No adapters are registered by default. A deployment adds the integrations it
    /// needs with <see cref="AddHospitalAdapter{TAdapter}"/>.
    /// </remarks>
    public static IServiceCollection AddHospitalAdapters(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IHospitalAdapterRegistry, HospitalAdapterRegistry>();

        return services;
    }

    /// <summary>
    /// Registers one hospital adapter implementation.
    /// </summary>
    /// <typeparam name="TAdapter">The adapter type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, to allow chaining.</returns>
    public static IServiceCollection AddHospitalAdapter<TAdapter>(this IServiceCollection services)
        where TAdapter : class, IHospitalAdapter
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IHospitalAdapter, TAdapter>();

        return services;
    }
}
