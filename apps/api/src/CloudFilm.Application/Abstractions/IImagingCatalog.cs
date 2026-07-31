using CloudFilm.Application.Imaging;

namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Reads patients and their imaging studies.
/// </summary>
/// <remarks>
/// <para>
/// The port the portal's read path depends on. Its implementation is an
/// infrastructure detail: today a fixture set, and from the next sprint the
/// Retrieve Service querying a real archive. Swapping one for the other is a
/// single registration in the composition root, and no caller changes.
/// </para>
/// <para>
/// Deliberately read-only. Nothing in CloudFilm writes to an imaging archive;
/// the hospital's system remains the record of truth.
/// </para>
/// </remarks>
public interface IImagingCatalog
{
    /// <summary>
    /// Lists the patients this deployment can serve.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Every known patient, ordered by medical record number.</returns>
    Task<IReadOnlyList<PatientSummary>> ListPatientsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a patient and their studies by Medical Record Number.
    /// </summary>
    /// <param name="medicalRecordNumber">The MRN to look up.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The record, or <see langword="null"/> when nothing matches.</returns>
    Task<PatientRecord?> FindPatientAsync(
        string medicalRecordNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a study by its Study Instance UID, with the patient it belongs to.
    /// </summary>
    /// <param name="studyInstanceUid">The DICOM Study Instance UID.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The study in context, or <see langword="null"/> when nothing matches.</returns>
    Task<StudyContext?> FindStudyAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default);
}
