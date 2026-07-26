namespace CloudFilm.Retrieve.Hospital;

/// <summary>
/// The integration port for one hospital imaging source.
/// </summary>
/// <remarks>
/// <para>
/// Every hospital exposes its imaging archive differently: DICOMweb, DIMSE C-MOVE,
/// a vendor HTTP API, or a file share. An adapter hides that difference behind a
/// single contract, so the Retrieve Service pipeline never contains hospital
/// specific branching.
/// </para>
/// <para>
/// Adapters are registered with the dependency injection container as
/// <see cref="IHospitalAdapter"/> and resolved through <see cref="IHospitalAdapterRegistry"/>.
/// Adding a hospital therefore means adding an assembly, not editing existing code.
/// </para>
/// </remarks>
public interface IHospitalAdapter
{
    /// <summary>
    /// Gets the stable code identifying the hospital this adapter serves.
    /// </summary>
    /// <remarks>Codes are case insensitive and unique across a deployment.</remarks>
    string HospitalCode { get; }

    /// <summary>
    /// Gets the human readable hospital name, for operator facing diagnostics.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Verifies that the hospital imaging source is reachable and accepting requests.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the probe.</param>
    /// <returns>The outcome of the probe.</returns>
    Task<HospitalConnectivity> ProbeAsync(CancellationToken cancellationToken = default);
}
