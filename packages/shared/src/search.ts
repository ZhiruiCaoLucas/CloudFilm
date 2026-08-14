import type { Patient } from './patient';
import type { Study } from './study';

/**
 * What to look for when searching for studies.
 *
 * States the clinical question, never the archive's query language: there is no
 * wildcard here, no limit and no offset. The backend appends the one wildcard it
 * uses and turns the page into whatever the archive understands.
 *
 * Every field is optional, but the API refuses a search with no criteria at all
 * - an unfiltered search is a request for the archive itself, one page at a
 * time. Callers should keep the submit control disabled until at least one of
 * the four criteria is filled in.
 */
export interface StudySearchQuery {
  /**
   * Part of the patient's name.
   *
   * Matched as a prefix. Any `*` or `?` the user types is removed before the
   * query is built, because DICOM matching has no way to treat one literally.
   */
  readonly patientName?: string | undefined;

  /** The accession number or its leading characters, matched as a prefix. */
  readonly accessionNumber?: string | undefined;

  /** Earliest study date to include, inclusive, as `YYYY-MM-DD`. */
  readonly studyDateFrom?: string | undefined;

  /** Latest study date to include, inclusive, as `YYYY-MM-DD`. */
  readonly studyDateTo?: string | undefined;

  /** Zero-based page of results wanted. Defaults to the first. */
  readonly page?: number | undefined;

  /**
   * How many results one page holds, from 1 to 100.
   *
   * Defaults to {@link STUDY_SEARCH.defaultPageSize}. Values outside the range
   * are refused by the API rather than clamped, so a caller is told rather than
   * quietly given a different page.
   */
  readonly pageSize?: number | undefined;
}

/**
 * One search hit: a study, and the patient it belongs to.
 *
 * A search crosses patients, which is what makes it different from every other
 * study list in CloudFilm. On a patient page the patient is the page; in a
 * result set two rows may belong to two people, so a row that cannot say whose
 * study it is cannot be acted on safely. Both halves also give the reader
 * somewhere to go: the patient record, or the study itself.
 */
export interface StudySearchItem {
  /** The patient the study belongs to. */
  readonly patient: Patient;

  /** The matching study. */
  readonly study: Study;
}

/**
 * One page of matching studies.
 *
 * Carries no total, because the imaging archive reports none. {@link hasMore}
 * answers the only question a reader can act on - is there another page - and
 * the backend establishes it by asking for one result beyond the page and
 * reporting whether it arrived. So a result set is never silently truncated:
 * either everything matching is here, or the reader is told there is more and
 * can narrow the search.
 */
export interface StudySearchResult {
  /** The hits on this page. Never longer than {@link pageSize}. */
  readonly items: readonly StudySearchItem[];

  /** `true` when at least one further result exists beyond this page. */
  readonly hasMore: boolean;

  /** The zero-based page this result represents. */
  readonly page: number;

  /** The page size the search was executed with. */
  readonly pageSize: number;
}

/**
 * The bounds a study search is executed within.
 *
 * Mirrors `StudySearchLimits` on the server. Restated here so a form can refuse
 * an impossible page size before spending a request on it - never so the client
 * can decide the limit, which the API enforces regardless of what is sent.
 */
export const STUDY_SEARCH = {
  /** The smallest page the API accepts. */
  minPageSize: 1,

  /** The largest page the API accepts. */
  maxPageSize: 100,

  /** The page size used when a caller names none. */
  defaultPageSize: 25,
} as const;
