/**
 * Identifies a hospital integration exposed by the Retrieve Service.
 *
 * Mirrors `CloudFilm.Retrieve.Endpoints.HospitalSummary`.
 */
export interface HospitalSummary {
  readonly code: string;
  readonly displayName: string;
}
