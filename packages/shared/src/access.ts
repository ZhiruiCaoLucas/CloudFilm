/**
 * What the signed-in user has looked at.
 *
 * Mirrors `CloudFilm.Application.Access`. Nothing here describes an audit
 * record: the API answers "which patients did I open", and the client is not
 * told that an audit table is where the answer comes from.
 */

/** A patient the signed-in user opened recently. */
export interface RecentPatient {
  /**
   * The patient's hospital identifier.
   *
   * PHI, and the only field. Enough to build a link to `/patient/{mrn}` and
   * nothing more - no name, no date of birth, no timestamp, no actor.
   */
  readonly medicalRecordNumber: string;
}
