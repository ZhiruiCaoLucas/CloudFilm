import type { Patient, Study } from '../src/index';

/**
 * A patient together with their imaging studies.
 *
 * The shape the portal needs for one patient view. When the API arrives this is
 * what its response will deserialise into.
 */
export interface PatientRecord {
  readonly patient: Patient;
  readonly studies: readonly Study[];
}

/**
 * One study, resolved together with the patient and siblings it belongs to.
 *
 * The workspace needs all three at once: the header identifies the patient, the
 * navigator lists every study, and the viewer opens one of them.
 */
export interface StudyContext {
  readonly patient: Patient;
  readonly studies: readonly Study[];
  readonly selectedStudy: Study;
}
