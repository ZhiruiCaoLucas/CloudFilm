namespace CloudFilm.Retrieve.Hospital;

/// <summary>
/// Resolves the adapter responsible for a given hospital.
/// </summary>
public interface IHospitalAdapterRegistry
{
    /// <summary>
    /// Gets every registered adapter.
    /// </summary>
    public IReadOnlyCollection<IHospitalAdapter> Adapters { get; }

    /// <summary>
    /// Finds the adapter for a hospital code.
    /// </summary>
    /// <param name="hospitalCode">The hospital code, compared case insensitively.</param>
    /// <returns>The adapter, or <see langword="null"/> when no adapter is registered.</returns>
    public IHospitalAdapter? Find(string hospitalCode);
}
