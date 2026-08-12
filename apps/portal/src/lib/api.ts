import { redirect } from 'next/navigation';
import { ApiError, createApiClient, type ApiClient } from '@cloudfilm/core';
import type { CurrentUser } from '@cloudfilm/shared';
import { env } from './env';
import { currentPathname, readAccessToken, signInUrl } from './session';
import 'server-only';

/**
 * The API client used by server components.
 *
 * The portal's only route to application data. The access token is read from
 * the session cookie here, on the server, and handed to the client through the
 * `getAccessToken` hook it has always had - so the token reaches the API in an
 * `Authorization` header without ever being sent to the browser.
 *
 * Asynchronous because reading a cookie is: `cookies()` returns a promise in
 * this version of Next. Call sites await it.
 */
export async function serverApiClient(): Promise<ApiClient> {
  const token = await readAccessToken();
  const client = createApiClient({ baseUrl: env.apiUrl, getAccessToken: () => token });

  return withSignInOn401(client, await currentPathname());
}

/**
 * Who is signed in, for display purposes.
 *
 * @returns The user, or `null` when nobody is signed in *or* the question could
 * not be answered.
 * @remarks
 * Never throws. Showing a name in the navigation is the least important thing
 * on any page, and it must not be able to take one down: without this the
 * landing page would require a reachable API to render at all, which is exactly
 * the failure `env.ts` goes out of its way to avoid for a fresh clone with no
 * backend running.
 *
 * Only for rendering. Anything that decides access must ask the API, which
 * validates the token rather than trusting this.
 */
export async function currentUser(): Promise<CurrentUser | null> {
  try {
    const client = await serverApiClient();

    return await client.getCurrentUser();
  } catch {
    return null;
  }
}

/**
 * Sends the user to sign in when the API says they are not signed in.
 *
 * @param client The client to wrap.
 * @param returnUrl Where to come back to once they are.
 * @returns A client that redirects rather than throwing on 401.
 * @remarks
 * Without this a 401 propagates as an unhandled error and the reader gets an
 * error card that says something went wrong - which is true but useless, since
 * the fix is to sign in. `getPatient`, `getStudy` and `getCurrentUser` already
 * fold their own "not signed in" case into `null`; this covers the calls that
 * do not, and any that are added later.
 */
function withSignInOn401(client: ApiClient, returnUrl: string): ApiClient {
  async function guard<T>(operation: () => Promise<T>): Promise<T> {
    try {
      return await operation();
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        // Throws NEXT_REDIRECT, which Next unwinds to a redirect response. It
        // must not be caught here, and is not: only ApiError reaches this block.
        redirect(signInUrl(returnUrl));
      }

      throw error;
    }
  }

  return {
    getSystemInfo: (signal) => guard(() => client.getSystemInfo(signal)),
    listPatients: (signal) => guard(() => client.listPatients(signal)),
    getPatient: (medicalRecordNumber, signal) =>
      guard(() => client.getPatient(medicalRecordNumber, signal)),
    getStudy: (studyInstanceUid, signal) => guard(() => client.getStudy(studyInstanceUid, signal)),

    // Not guarded. Signing in is how a caller stops being unauthenticated, so a
    // 401 here is the answer, not a reason to bounce them somewhere else.
    login: (request, signal) => client.login(request, signal),
    getCurrentUser: (signal) => client.getCurrentUser(signal),
  };
}
