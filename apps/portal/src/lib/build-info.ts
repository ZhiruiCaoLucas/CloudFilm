import packageJson from '../../package.json';

/**
 * Identifies the running portal build.
 *
 * Commit and build time are optional: a developer running `pnpm dev` has
 * neither, and requiring them would break the promise that a fresh clone runs
 * with no configuration. A release pipeline supplies them; locally they read as
 * "local".
 */
export const buildInfo = {
  /** Portal version, from `apps/portal/package.json`. */
  version: packageJson.version,

  /** Short commit SHA the build came from. */
  commit: process.env['CLOUDFILM_BUILD_SHA']?.trim() || 'local',

  /** When the build was produced. */
  builtAt: process.env['CLOUDFILM_BUILD_TIME']?.trim() || 'development build',
} as const;
