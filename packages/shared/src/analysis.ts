/**
 * What the analysis service reports about one study.
 *
 * Mirrors the .NET `StudyAnalysis` record across the transport boundary, like
 * every other type in this package. Field for field and nothing more: a value
 * the API does not send is a value nothing can render.
 *
 * Carries no patient identity, and must not gain any. The analyser is told which
 * study it is looking at and nothing about whose it is, so a name or a record
 * number appearing here would mean the PHI boundary had moved without anybody
 * deciding to move it.
 */
export interface StudyAnalysis {
  /** The study the observations are about. */
  readonly studyInstanceUid: string;
  /** The model that produced them. */
  readonly modelName: string;
  /** The exact version of that model. Two results from different versions are not comparable. */
  readonly modelVersion: string;
  /** When the analysis finished, as an ISO 8601 instant. */
  readonly completedAtUtc: string;
  /** What was observed, in the order it was reported. An empty list is a real answer. */
  readonly findings: readonly AnalysisFinding[];
}

/**
 * One thing the analysis observed.
 *
 * A label and a number, and deliberately nothing else. There are no regions,
 * outlines or measurements in this contract because the service produces none;
 * a field promising geometry would be a promise the system cannot keep.
 *
 * Not a clinical finding. The current model characterises the file - its
 * modality, geometry, bit depth and a pixel statistic - and states each
 * observation as `name=value` with a score of 1 meaning "observed". Nothing here
 * is a diagnosis, and the interface must never present it as one.
 */
export interface AnalysisFinding {
  /** What was observed. Usually `name=value`; occasionally a bare name. */
  readonly label: string;
  /** The model's own number for this observation, from 0 to 1 inclusive. */
  readonly score: number;
}
