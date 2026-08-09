import type { Patient } from './patient';
import type { Series } from './series';
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

  /**
   * The series of {@link StudyContext.selectedStudy}, and of that study only.
   *
   * Deliberately here and not on {@link Study}. Series are resolved by a second
   * archive query against one Study Instance UID; hanging them off every entry
   * in `studies` would turn opening one study into one query per sibling — an
   * N+1 against a hospital archive — to populate a field the navigator never
   * reads. Empty when the archive reports no series, or could not be asked.
   */
  readonly series: readonly Series[];
}
