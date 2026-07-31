using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Imaging;

namespace CloudFilm.Infrastructure.Imaging;

/// <summary>
/// Fixture-backed <see cref="IImagingCatalog"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the only place in CloudFilm that knows the imaging data is not real.
/// It exists so the portal can be built against a stable contract before the
/// Retrieve Service can answer queries; the next sprint replaces this class with
/// one that queries a real archive, and nothing above it changes.
/// </para>
/// <para>
/// Study Instance UIDs use the DICOM-reserved <c>1.2.826.0.1.3680043.8.498.*</c>
/// prefix, so nothing here can collide with a real hospital's identifiers.
/// </para>
/// </remarks>
internal sealed class MockImagingCatalog : IImagingCatalog
{
    private static readonly IReadOnlyList<PatientRecord> Records =
    [
        new PatientRecord(
            new PatientSummary(
                MedicalRecordNumber: "10001",
                FullName: "Zhang San",
                Gender: "Male",
                BirthDate: "1992-01-01"),
            [
                new StudySummary(
                    StudyInstanceUid: "1.2.826.0.1.3680043.8.498.10001.20260701",
                    Modality: "CBCT",
                    Description: "Maxillofacial cone beam CT",
                    PerformedOn: "2026-07-01",
                    Status: "Completed",
                    SeriesCount: 2,
                    InstanceCount: 512),
                new StudySummary(
                    StudyInstanceUid: "1.2.826.0.1.3680043.8.498.10001.20260610",
                    Modality: "CT",
                    Description: "Head CT without contrast",
                    PerformedOn: "2026-06-10",
                    Status: "Completed",
                    SeriesCount: 4,
                    InstanceCount: 240),
                new StudySummary(
                    StudyInstanceUid: "1.2.826.0.1.3680043.8.498.10001.20260601",
                    Modality: "PX",
                    Description: "Full mouth panoramic radiograph",
                    PerformedOn: "2026-06-01",
                    Status: "Completed",
                    SeriesCount: 1,
                    InstanceCount: 1),
            ]),
    ];

    /// <inheritdoc />
    public Task<IReadOnlyList<PatientSummary>> ListPatientsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PatientSummary> patients = Records
            .Select(record => record.Patient)
            .OrderBy(patient => patient.MedicalRecordNumber, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult(patients);
    }

    /// <inheritdoc />
    public Task<PatientRecord?> FindPatientAsync(
        string medicalRecordNumber,
        CancellationToken cancellationToken = default)
    {
        // Trimmed and case insensitive, mirroring how a hospital search field
        // behaves: a clinician typing a lowercase MRN should not get "not found".
        string normalised = medicalRecordNumber.Trim();

        PatientRecord? match = Records.FirstOrDefault(record =>
            string.Equals(
                record.Patient.MedicalRecordNumber,
                normalised,
                StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(match);
    }

    /// <inheritdoc />
    public Task<StudyContext?> FindStudyAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        string normalised = studyInstanceUid.Trim();

        foreach (PatientRecord record in Records)
        {
            StudySummary? selected = record.Studies.FirstOrDefault(study =>
                string.Equals(study.StudyInstanceUid, normalised, StringComparison.Ordinal));

            if (selected is not null)
            {
                return Task.FromResult<StudyContext?>(
                    new StudyContext(record.Patient, record.Studies, selected));
            }
        }

        return Task.FromResult<StudyContext?>(null);
    }
}
