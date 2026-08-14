import { redirect } from 'next/navigation';
import { ApiError, createApiClient, type ApiClient } from '@cloudfilm/core';
import type { CurrentUser, RecentPatient } from '@cloudfilm/shared';
import { env } from './env';
import { NO_ROLE_PATH, currentPathname, readAccessToken, signInUrl } from './session';
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

  return withAuthorizationRedirects(client, await currentPathname());
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
 * The patients the signed-in user opened most recently.
 *
 * @returns The list, or an empty one when nobody is signed in *or* the question
 * could not be answered.
 * @remarks
 * Never throws, for the same reason as {@link currentUser}. This is a
 * convenience panel on the landing page: it must not be able to redirect an
 * anonymous visitor away from a page they are allowed to see, and it must not
 * take the page down when the API is unreachable - which `env.ts` deliberately
 * keeps working for a fresh clone with no backend running.
 */
export async function recentPatients(): Promise<readonly RecentPatient[]> {
  try {
    const client = await serverApiClient();

    return await client.getRecentPatients();
  } catch {
    return [];
  }
}

/**
 * Turns the API's two refusals into somewhere the reader can go.
 *
 * @param client The client to wrap.
 * @param returnUrl Where to come back to after signing in.
 * @returns A client that redirects rather than throwing on 401 or 403.
 * @remarks
 * Without this both statuses propagate as unhandled errors and the reader gets
 * an error card that says something went wrong - true, useless, and in the 403
 * case actively misleading, because nothing went wrong at all.
 *
 * The two are answers to different questions and are handled differently:
 *
 * - **401** means nobody is signed in. Sign-in fixes it, so that is where the
 *   reader goes, with the current address to come back to.
 * - **403** means somebody is signed in and their account holds no application
 *   role. Signing in again would change nothing, so they go to a page that says
 *   what is missing and who can grant it. Their session is left alone.
 *
 * Everything else - a 500, a timeout, an unreachable API - is left to
 * propagate. Those are failures, and an error boundary is the honest place for
 * them; folding them in here would make a broken service look like a
 * permissions problem.
 *
 * This is presentation, not enforcement. The API refuses these calls on the
 * server whatever the browser is shown, and no redirect here grants anything.
 */
function withAuthorizationRedirects(client: ApiClient, returnUrl: string): ApiClient {
  async function guard<T>(operation: () => Promise<T>): Promise<T> {
    try {
      return await operation();
    } catch (error) {
      // Both branches throw NEXT_REDIRECT, which Next unwinds into a redirect
      // response. Neither must be caught here, and neither is: only an ApiError
      // reaches this block.
      if (error instanceof ApiError && error.status === 401) {
        redirect(signInUrl(returnUrl));
      }

      if (error instanceof ApiError && error.status === 403) {
        redirect(NO_ROLE_PATH);
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
    getRecentPatients: (signal) => guard(() => client.getRecentPatients(signal)),
    getStudyReadState: (studyInstanceUid, signal) =>
      guard(() => client.getStudyReadState(studyInstanceUid, signal)),
    markStudyRead: (studyInstanceUid, signal) =>
      guard(() => client.markStudyRead(studyInstanceUid, signal)),
    markStudyUnread: (studyInstanceUid, signal) =>
      guard(() => client.markStudyUnread(studyInstanceUid, signal)),
    searchStudies: (query, signal) => guard(() => client.searchStudies(query, signal)),

    // Not guarded. Signing in is how a caller stops being unauthenticated, so a
    // 401 here is the answer, not a reason to bounce them somewhere else.
    login: (request, signal) => client.login(request, signal),
    getCurrentUser: (signal) => client.getCurrentUser(signal),
  };
}
