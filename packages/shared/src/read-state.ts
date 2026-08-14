/**
 * Whether the signed-in user has read a study.
 *
 * Mirrors `CloudFilm.Application.ReadState.StudyReadState`. CloudFilm's own
 * workflow state, not DICOM and not Orthanc: it is one clinician's note to
 * themselves, so two clinicians looking at the same study will legitimately
 * disagree about it. Distinct from {@link Study.status}, which is the archive's
 * view of a study's lifecycle and means the same thing to everybody.
 */
export interface StudyReadState {
  /** `true` when the signed-in user has marked this study read. */
  readonly read: boolean;
}
