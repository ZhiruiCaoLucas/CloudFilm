'use server';

import { redirect } from 'next/navigation';
import type { LoginResponse } from '@cloudfilm/shared';
import { serverApiClient } from '@/lib/api';
import {
  NO_ROLE_PATH,
  SIGN_IN_PATH,
  clearSession,
  toInternalPath,
  writeSession,
} from '@/lib/session';
import type { SignInState } from './sign-in-state';

/**
 * Verifies credentials and starts a session.
 *
 * @param _state The previous form state, unused.
 * @param formData The submitted form.
 * @returns The next form state, or never - a successful sign-in redirects.
 * @remarks
 * A Server Action rather than a route handler or a client fetch, because the
 * token must be written to an `HttpOnly` cookie and the browser must never hold
 * it. The password crosses the network once, to this function, and is not
 * logged, stored or echoed back.
 */
export async function signIn(_state: SignInState, formData: FormData): Promise<SignInState> {
  const email = String(formData.get('email') ?? '').trim();
  const password = String(formData.get('password') ?? '');
  const returnUrl = toInternalPath(String(formData.get('returnUrl') ?? '/'));

  if (email.length === 0 || password.length === 0) {
    return { error: 'auth.missingFields' };
  }

  const client = await serverApiClient();
  let session: LoginResponse | null;

  try {
    session = await client.login({ email, password });
  } catch {
    // A transport failure, not a rejected credential. Saying so keeps the user
    // from retyping a password that was never the problem.
    return { error: 'auth.unreachable' };
  }

  if (session === null) {
    return { error: 'auth.invalidCredentials' };
  }

  await writeSession(session.accessToken, session.expiresAt);

  // The session is written either way: the account authenticated, and signing
  // in again would not give it a role. Where it lands differs.
  //
  // An account with no application role is sent somewhere that says so, rather
  // than to a start page that looks like everything worked - which it does,
  // because the panels on it tolerate a refusal and simply render empty. The
  // reader would go looking for a patient before discovering anything was
  // wrong.
  //
  // This is where the reader is taken, not what they are allowed to see. The
  // API refuses every protected call from such an account on its own, and keeps
  // refusing if this check is ever bypassed or if the role is withdrawn while
  // the token is still valid - which is why the 403 handling that already
  // routes here stays exactly as it is. Deleting one in favour of the other
  // would be a mistake in either direction.
  //
  // Both branches throw NEXT_REDIRECT and must stay outside the try above so
  // they are not caught.
  if (session.roles.length === 0) {
    redirect(NO_ROLE_PATH);
  }

  redirect(returnUrl);
}

/**
 * Ends the browser's session.
 *
 * @remarks
 * Removes the cookie. The token itself remains valid until it expires, because
 * CloudFilm issues no refresh token and holds no revocation list - there is
 * nothing server-side to invalidate. Said plainly here so nobody reads this as
 * a stronger guarantee than it is.
 */
export async function signOut(): Promise<void> {
  await clearSession();

  redirect(SIGN_IN_PATH);
}
