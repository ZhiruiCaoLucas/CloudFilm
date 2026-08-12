'use server';

import { redirect } from 'next/navigation';
import type { LoginResponse } from '@cloudfilm/shared';
import { serverApiClient } from '@/lib/api';
import { SIGN_IN_PATH, clearSession, toInternalPath, writeSession } from '@/lib/session';
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

  // Throws NEXT_REDIRECT; must stay outside the try above so it is not caught.
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
