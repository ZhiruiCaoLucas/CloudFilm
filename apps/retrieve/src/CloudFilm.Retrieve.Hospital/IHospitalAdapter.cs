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
    public string HospitalCode { get; }

    /// <summary>
    /// Gets the human readable hospital name, for operator facing diagnostics.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Verifies that the hospital imaging source is reachable and accepting requests.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the probe.</param>
    /// <returns>The outcome of the probe.</returns>
    public Task<HospitalConnectivity> ProbeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the patients this source holds imaging for.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>Every known patient, ordered by medical record number.</returns>
    public Task<IReadOnlyList<PatientSummary>> ListPatientsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a patient and their studies by Medical Record Number.
    /// </summary>
    /// <param name="medicalRecordNumber">The MRN to look up.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The record, or <see langword="null"/> when nothing matches.</returns>
    public Task<PatientRecord?> FindPatientAsync(
        string medicalRecordNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a study by its Study Instance UID, with the patient it belongs to.
    /// </summary>
    /// <param name="studyInstanceUid">The DICOM Study Instance UID.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The study in context, or <see langword="null"/> when nothing matches.</returns>
    public Task<StudyContext?> FindStudyAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default);
}
