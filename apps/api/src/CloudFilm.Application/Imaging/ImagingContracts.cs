namespace CloudFilm.Application.Imaging;

/// <summary>
/// A patient as the portal displays them.
/// </summary>
/// <param name="MedicalRecordNumber">Hospital-assigned Medical Record Number.</param>
/// <param name="FullName">Full name as recorded by the hospital.</param>
/// <param name="Gender">Administrative gender.</param>
/// <param name="BirthDate">Date of birth as an ISO 8601 calendar date.</param>
public sealed record PatientSummary(
    string MedicalRecordNumber,
    string FullName,
    string Gender,
    string BirthDate);

/// <summary>
/// An imaging study belonging to a patient.
/// </summary>
/// <param name="StudyInstanceUid">DICOM Study Instance UID; the identifier the viewer is launched with.</param>
/// <param name="Modality">DICOM modality code, for example <c>CT</c>.</param>
/// <param name="Description">Short clinical description.</param>
/// <param name="PerformedOn">Date performed, as an ISO 8601 calendar date.</param>
/// <param name="Status">Where the study is in its lifecycle.</param>
/// <param name="SeriesCount">Number of series.</param>
/// <param name="InstanceCount">Number of images across every series.</param>
public sealed record StudySummary(
    string StudyInstanceUid,
    string Modality,
    string Description,
    string PerformedOn,
    string Status,
    int SeriesCount,
    int InstanceCount);

/// <summary>
/// A patient together with their imaging studies.
/// </summary>
/// <param name="Patient">The patient.</param>
/// <param name="Studies">Their studies, most recent first.</param>
public sealed record PatientRecord(PatientSummary Patient, IReadOnlyList<StudySummary> Studies);

/// <summary>
/// One study, resolved together with the patient and siblings it belongs to.
/// </summary>
/// <remarks>
/// The workspace needs all three at once: the header identifies the patient, the
/// navigator lists every study, and the viewer opens one of them. Resolving them
/// in a single call keeps the page from making three requests that could disagree.
/// </remarks>
/// <param name="Patient">The patient the study belongs to.</param>
/// <param name="Studies">Every study for that patient.</param>
/// <param name="SelectedStudy">The study that was requested.</param>
public sealed record StudyContext(
    PatientSummary Patient,
    IReadOnlyList<StudySummary> Studies,
    StudySummary SelectedStudy);
