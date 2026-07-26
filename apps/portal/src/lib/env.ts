/**
 * Runtime configuration for the portal.
 *
 * Validated once, at module load, so that a missing variable surfaces as a clear
 * start up failure rather than an undefined URL deep inside a request.
 */

function required(name: string, value: string | undefined): string {
  if (value === undefined || value.trim().length === 0) {
    throw new Error(`Environment variable ${name} is required but was not set.`);
  }

  return value.replace(/\/+$/, '');
}

export const serverEnv = {
  /**
   * Internal address of the CloudFilm API.
   *
   * Server side only. The browser never learns this address, which is what keeps
   * the API and everything behind it off the public network.
   */
  apiUrl: required('CLOUDFILM_API_URL', process.env['CLOUDFILM_API_URL']),
} as const;

export const publicEnv = {
  /**
   * Browser reachable address of the OHIF Viewer deployment.
   *
   * Public by necessity: the viewer runs in an iframe in the user's browser.
   */
  viewerUrl: required('NEXT_PUBLIC_VIEWER_URL', process.env['NEXT_PUBLIC_VIEWER_URL']),
} as const;
