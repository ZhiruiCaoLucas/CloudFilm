/**
 * Describes the running CloudFilm API deployment.
 *
 * Mirrors `CloudFilm.Api.Endpoints.SystemInfoResponse`. The two are kept in step by
 * the contract tests introduced with the first feature module; until then this file
 * is the single source of truth for the browser.
 */
export interface SystemInfo {
  readonly name: string;
  readonly version: string;
  readonly environment: string;
  readonly serverTimeUtc: string;
}
