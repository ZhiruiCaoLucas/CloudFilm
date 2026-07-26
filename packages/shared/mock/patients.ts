import type { Patient, Study } from '../src/index';

/**
 * A patient together with their imaging studies.
 *
 * The shape the portal needs for one patient view. When the API arrives this is
 * what its response will deserialise into, which is why it lives in the contract
 * types rather than being invented by the page.
 */
export interface PatientRecord {
  readonly patient: Patient;
  readonly studies: readonly Study[];
}

/**
 * The fixture set backing the portal until the API exists.
 *
 * Study Instance UIDs use the DICOM-reserved `1.2.826.0.1.3680043.8.498.*`
 * prefix, so nothing here can collide with a real hospital's identifiers if a
 * fixture ever escapes into a live environment.
 */
const RECORDS: readonly PatientRecord[] = [
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
      },
      {
        studyInstanceUid: '1.2.826.0.1.3680043.8.498.10001.20260610',
        modality: 'CT',
        description: 'Head CT without contrast',
        performedOn: '2026-06-10',
        status: 'Completed',
        seriesCount: 4,
      },
      {
        studyInstanceUid: '1.2.826.0.1.3680043.8.498.10001.20260601',
        modality: 'PX',
        description: 'Full mouth panoramic radiograph',
        performedOn: '2026-06-01',
        status: 'Completed',
        seriesCount: 1,
      },
    ],
  },
];

/**
 * Finds a fixture patient by Medical Record Number.
 *
 * Matching is trimmed and case-insensitive, mirroring how a hospital search
 * field behaves — a clinician typing a lowercase MRN should not get a
 * "not found".
 *
 * @param medicalRecordNumber The MRN to look up.
 * @returns The record, or `undefined` when no fixture matches.
 */
export function findPatientRecord(medicalRecordNumber: string): PatientRecord | undefined {
  const normalised = medicalRecordNumber.trim().toLowerCase();

  return RECORDS.find((record) => record.patient.medicalRecordNumber.toLowerCase() === normalised);
}

/**
 * Every MRN the fixtures know about.
 *
 * Used by the not-found page to tell a developer what they can actually search
 * for, instead of leaving them guessing.
 */
export const KNOWN_MEDICAL_RECORD_NUMBERS: readonly string[] = RECORDS.map(
  (record) => record.patient.medicalRecordNumber,
);
