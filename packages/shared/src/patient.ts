/**
 * A patient as the portal displays them.
 *
 * Deliberately a thin projection: CloudFilm is not the system of record for
 * demographics. The hospital archive is, and this carries only what a clinician
 * needs to confirm they are looking at the right person.
 */
export interface Patient {
  /**
   * Hospital-assigned Medical Record Number.
   *
   * A string, not a number: MRNs are identifiers, and hospitals issue
   * alphanumeric ones with leading zeros that numeric parsing would corrupt.
   */
  readonly medicalRecordNumber: string;

  /** Full name as recorded by the hospital. */
  readonly fullName: string;

  /** Administrative gender. */
  readonly gender: PatientGender;

  /** Date of birth as an ISO 8601 calendar date (`YYYY-MM-DD`). */
  readonly birthDate: string;
}

/**
 * Administrative gender.
 *
 * Spelled out rather than using the DICOM single-character codes (`M`, `F`,
 * `O`), because nothing in the portal round-trips these values to DICOM. The
 * mapping belongs with the adapter that first reads a real archive.
 */
export type PatientGender = 'Male' | 'Female' | 'Other' | 'Unknown';
