import type { Study } from '../src/index';
import type { PatientRecord, StudyContext } from './records';
import { SEEDED_RECORDS } from './seeded';

/**
 * The built-in fixture set.
 *
 * Used whenever nothing has been seeded into a local Orthanc, which includes
 * every fresh checkout and every environment without Docker. Its Study Instance
 * UIDs are synthetic — the viewer cannot open them, and is not expected to.
 *
 * UIDs use the DICOM-reserved `1.2.826.0.1.3680043.8.498.*` prefix, so nothing
 * here can collide with a real hospital's identifiers if a fixture ever escapes
 * into a live environment.
 */
const BUILTIN_RECORDS: readonly PatientRecord[] = [
  {
    patient: {
      medicalRecordNumber: '10001',
      fullName: 'Zhang San',
      gender: 'Male',
      birthDate: '1992-01-01',
    },
    studies: [
      {
        studyInstanceUid: '1.2.826.0.1.3680043.8.498.10001.20260701',
        modality: 'CBCT',
        description: 'Maxillofacial cone beam CT',
        performedOn: '2026-07-01',
        status: 'Completed',
        seriesCount: 2,
        instanceCount: 512,
      },
      {
        studyInstanceUid: '1.2.826.0.1.3680043.8.498.10001.20260610',
        modality: 'CT',
        description: 'Head CT without contrast',
        performedOn: '2026-06-10',
        status: 'Completed',
        seriesCount: 4,
        instanceCount: 240,
      },
      {
        studyInstanceUid: '1.2.826.0.1.3680043.8.498.10001.20260601',
        modality: 'PX',
        description: 'Full mouth panoramic radiograph',
        performedOn: '2026-06-01',
        status: 'Completed',
        seriesCount: 1,
        instanceCount: 1,
      },
    ],
  },
];

/**
 * The records the portal serves.
 *
 * Seeded data wins when it exists. That is the whole point: once real DICOM is
 * in Orthanc, the study list must show the UIDs Orthanc actually holds, or
 * "Open Study" hands the viewer an identifier it cannot resolve.
 */
const RECORDS: readonly PatientRecord[] =
  SEEDED_RECORDS.length > 0 ? SEEDED_RECORDS : BUILTIN_RECORDS;

/** Whether the portal is serving studies that really exist in a local Orthanc. */
export const IS_SERVING_SEEDED_DATA: boolean = SEEDED_RECORDS.length > 0;

/**
 * Finds a patient by Medical Record Number.
 *
 * Matching is trimmed and case-insensitive, mirroring how a hospital search
 * field behaves — a clinician typing a lowercase MRN should not get a
 * "not found".
 *
 * @param medicalRecordNumber The MRN to look up.
 * @returns The record, or `undefined` when nothing matches.
 */
export function findPatientRecord(medicalRecordNumber: string): PatientRecord | undefined {
  const normalised = medicalRecordNumber.trim().toLowerCase();

  return RECORDS.find((record) => record.patient.medicalRecordNumber.toLowerCase() === normalised);
}

/**
 * Finds a study by its Study Instance UID, with the patient it belongs to.
 *
 * @param studyInstanceUid The DICOM Study Instance UID.
 * @returns The study in context, or `undefined` when nothing matches.
 */
export function findStudyContext(studyInstanceUid: string): StudyContext | undefined {
  const normalised = studyInstanceUid.trim();

  for (const record of RECORDS) {
    const selectedStudy: Study | undefined = record.studies.find(
      (study) => study.studyInstanceUid === normalised,
    );

    if (selectedStudy !== undefined) {
      return { patient: record.patient, studies: record.studies, selectedStudy };
    }
  }

  return undefined;
}

/**
 * Every MRN currently available.
 *
 * Used by the not-found page to tell a developer what they can actually search
 * for, which changes as soon as real data is seeded.
 */
export const KNOWN_MEDICAL_RECORD_NUMBERS: readonly string[] = RECORDS.map(
  (record: PatientRecord): string => record.patient.medicalRecordNumber,
);
