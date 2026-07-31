import type { Patient } from './patient';
import type { Study } from './study';

/**
 * A patient together with their imaging studies.
 *
 * Mirrors `CloudFilm.Application.Imaging.PatientRecord`. What the API returns
 * for one patient view.
 */
export interface PatientRecord {
  readonly patient: Patient;
  readonly studies: readonly Study[];
}

/**
 * One study, resolved together with the patient and siblings it belongs to.
 *
 * Mirrors `CloudFilm.Application.Imaging.StudyContext`. The workspace needs all
 * three at once, so the API resolves them in a single request rather than
 * leaving the page to make three that could disagree.
 */
export interface StudyContext {
  readonly patient: Patient;
  readonly studies: readonly Study[];
  readonly selectedStudy: Study;
}
