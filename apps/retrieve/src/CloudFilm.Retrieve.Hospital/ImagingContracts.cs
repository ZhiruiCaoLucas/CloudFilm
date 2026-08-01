namespace CloudFilm.Retrieve.Hospital;

/// <summary>
/// A patient as an imaging source reports them.
/// </summary>
/// <param name="MedicalRecordNumber">Hospital-assigned Medical Record Number.</param>
/// <param name="FullName">Full name.</param>
/// <param name="Gender">Administrative gender.</param>
/// <param name="BirthDate">Date of birth as an ISO 8601 calendar date.</param>
public sealed record PatientSummary(
    string MedicalRecordNumber,
    string FullName,
    string Gender,
    string BirthDate);

/// <summary>
/// An imaging study as an imaging source reports it.
/// </summary>
/// <param name="StudyInstanceUid">DICOM Study Instance UID.</param>
/// <param name="Modality">DICOM modality code.</param>
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
/// <param name="Patient">The patient the study belongs to.</param>
/// <param name="Studies">Every study for that patient.</param>
/// <param name="SelectedStudy">The study that was requested.</param>
public sealed record StudyContext(
    PatientSummary Patient,
    IReadOnlyList<StudySummary> Studies,
    StudySummary SelectedStudy);
