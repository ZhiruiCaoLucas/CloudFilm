/**
 * An imaging study belonging to a patient.
 */
export interface Study {
  /**
   * DICOM Study Instance UID.
   *
   * The identifier the viewer is launched with, so it is part of the contract
   * from the first sprint even though nothing opens a study yet.
   */
  readonly studyInstanceUid: string;

  /** Imaging modality. */
  readonly modality: StudyModality;

  /** Short clinical description of what was imaged. */
  readonly description: string;

  /** Date the study was performed, as an ISO 8601 calendar date (`YYYY-MM-DD`). */
  readonly performedOn: string;

  /** Where the study is in its lifecycle. */
  readonly status: StudyStatus;

  /** Number of series in the study. */
  readonly seriesCount: number;
}

/**
 * Modalities CloudFilm presents today.
 *
 * `PX` follows the DICOM code for panoramic X-ray. `CBCT` does not have its own
 * DICOM modality code — archives record it as `CT` — but clinicians read the two
 * very differently, so the portal keeps them distinct.
 */
export type StudyModality = 'CBCT' | 'CT' | 'PX';

/** Where a study is in its lifecycle. */
export type StudyStatus = 'Completed' | 'InProgress' | 'Cancelled';

/**
 * Clinician-facing labels for {@link StudyModality}.
 *
 * Exhaustive by construction: adding a modality to the union will not compile
 * until its label is supplied here.
 */
export const MODALITY_LABELS: Record<StudyModality, string> = {
  CBCT: 'CBCT',
  CT: 'CT',
  PX: 'Panoramic X-Ray',
};

/** Clinician-facing labels for {@link StudyStatus}. */
export const STUDY_STATUS_LABELS: Record<StudyStatus, string> = {
  Completed: 'Completed',
  InProgress: 'In progress',
  Cancelled: 'Cancelled',
};
