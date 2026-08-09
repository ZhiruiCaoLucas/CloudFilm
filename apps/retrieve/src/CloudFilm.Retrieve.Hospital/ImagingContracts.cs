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
/// <param name="AccessionNumber">DICOM Accession Number, or empty when the archive holds none.</param>
/// <param name="PerformedOn">Date performed, as an ISO 8601 calendar date.</param>
/// <param name="Status">Where the study is in its lifecycle.</param>
/// <param name="SeriesCount">Number of series.</param>
/// <param name="InstanceCount">Number of images across every series.</param>
public sealed record StudySummary(
    string StudyInstanceUid,
    string Modality,
    string Description,
    string AccessionNumber,
    string PerformedOn,
    string Status,
    int SeriesCount,
    int InstanceCount);

/// <summary>
/// One series within an imaging study, as an imaging source reports it.
/// </summary>
/// <param name="SeriesInstanceUid">DICOM Series Instance UID.</param>
/// <param name="SeriesNumber">DICOM Series Number, or zero when the source reports none.</param>
/// <param name="Modality">DICOM modality code for this series.</param>
/// <param name="Description">DICOM Series Description, or empty when absent.</param>
/// <param name="InstanceCount">Number of images in this series, or zero when not reported.</param>
/// <param name="InstitutionName">DICOM Institution Name, or empty when absent.</param>
public sealed record SeriesSummary(
    string SeriesInstanceUid,
    int SeriesNumber,
    string Modality,
    string Description,
    int InstanceCount,
    string InstitutionName);

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
/// <param name="Series">
/// The series of <paramref name="SelectedStudy"/>, and of that study only.
/// Empty when the source reports no series, or could not be asked.
/// </param>
public sealed record StudyContext(
    PatientSummary Patient,
    IReadOnlyList<StudySummary> Studies,
    StudySummary SelectedStudy,
    IReadOnlyList<SeriesSummary> Series);
