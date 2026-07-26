/**
 * An imaging study belonging to a patient.
 */
export interface Study {
  /**
   * DICOM Study Instance UID.
   *
   * The identifier the viewer is launched with.
   */
  readonly studyInstanceUid: string;

  /**
   * DICOM modality code, for example `CT`, `MR` or `PX`.
   *
   * A free-form string rather than a closed union: DICOM defines a large and
   * growing code set, and a study arriving from a real archive with a modality
   * CloudFilm has never heard of must still be listed rather than crash the page.
   * Use {@link formatModality} to render it.
   */
  readonly modality: string;

  /** Short clinical description of what was imaged. */
  readonly description: string;

  /** Date the study was performed, as an ISO 8601 calendar date (`YYYY-MM-DD`). */
  readonly performedOn: string;

  /** Where the study is in its lifecycle. */
  readonly status: StudyStatus;

  /** Number of series in the study. */
  readonly seriesCount: number;

  /**
   * Number of images across every series.
   *
   * Shown in the workspace status bar. The viewer reports the authoritative
   * count once a study is loaded; this is what the study record claims.
   */
  readonly instanceCount: number;
}

/** Where a study is in its lifecycle. */
export type StudyStatus = 'Completed' | 'InProgress' | 'Cancelled';

/**
 * Clinician-facing labels for the modality codes CloudFilm spells out.
 *
 * Not exhaustive over DICOM, and not meant to be — anything absent falls through
 * to the raw code, which is what a radiographer would recognise anyway.
 *
 * `CBCT` is not a DICOM code: archives record cone beam CT as `CT`. CloudFilm
 * keeps it as a distinct code because clinicians read the two differently, and
 * a hospital adapter can set it when it knows the acquisition was cone beam.
 */
const MODALITY_LABELS: Readonly<Record<string, string>> = {
  CBCT: 'CBCT',
  CT: 'CT',
  PX: 'Panoramic X-Ray',
  DX: 'X-Ray',
  CR: 'Computed Radiography',
  MR: 'MRI',
  PT: 'PET',
  NM: 'Nuclear Medicine',
  US: 'Ultrasound',
  MG: 'Mammography',
  XA: 'Angiography',
  SR: 'Structured Report',
  OT: 'Other',
};

/**
 * Renders a DICOM modality code for display.
 *
 * @param code The modality code as recorded in DICOM.
 * @returns A clinician-facing label, or the code itself when unrecognised.
 */
export function formatModality(code: string): string {
  const normalised = code.trim().toUpperCase();

  return MODALITY_LABELS[normalised] ?? (normalised.length > 0 ? normalised : 'Unknown');
}

/** Clinician-facing labels for {@link StudyStatus}. */
export const STUDY_STATUS_LABELS: Record<StudyStatus, string> = {
  Completed: 'Completed',
  InProgress: 'In progress',
  Cancelled: 'Cancelled',
};
