import 'server-only';
import { cookies, headers } from 'next/headers';
import { FORWARDED_PROTO_HEADER, PATHNAME_HEADER } from './request-headers';

/**
 * The browser's session cookie.
 *
 * Holds the access token issued by `POST /api/v1/auth/login`. It is `HttpOnly`,
 * so no script - including CloudFilm's own - can read it, and it is never
 * copied into `localStorage`, `sessionStorage` or a `NEXT_PUBLIC_` value. The
 * token exists in exactly two places: this cookie, and the `Authorization`
 * header the server attaches when it calls the API on the user's behalf.
 */
export const SESSION_COOKIE = 'cloudfilm_session';

/** Where an unauthenticated visitor is sent. */
export const SIGN_IN_PATH = '/signin';

/**
 * Where a caller is sent when the API refuses them for want of a role.
 *
 * Deliberately not the sign-in page. Signing in again would achieve nothing -
 * the account is already authenticated, and what it lacks is something only an
 * administrator can give it. Sending them round the sign-in loop would suggest
 * their password was the problem.
 */
export const NO_ROLE_PATH = '/no-role';

/**
 * Reads the access token, if the browser sent one.
 *
 * @returns The token, or `undefined` when there is no session.
 */
export async function readAccessToken(): Promise<string | undefined> {
  const store = await cookies();
  const value = store.get(SESSION_COOKIE)?.value?.trim();

  return value !== undefined && value.length > 0 ? value : undefined;
}

/**
 * Stores an access token in the session cookie.
 *
 * @param accessToken The signed JWT.
 * @param expiresAt When the token stops being accepted, as an ISO 8601 instant.
 * @remarks
 * The cookie's lifetime is derived from the token's own expiry rather than
 * configured separately, so the two cannot drift. A cookie that outlived its
 * token would leave the user apparently signed in and unable to do anything.
 *
 * Callable from Server Actions and Route Handlers only - Next forbids writing
 * cookies while rendering, which is the correct restriction.
 */
export async function writeSession(accessToken: string, expiresAt: string): Promise<void> {
  const store = await cookies();
  const expiry = Date.parse(expiresAt);

  // Falls back to nothing rather than to a guess: a cookie with no Max-Age is a
  // session cookie, discarded when the browser closes. That is the safe
  // direction if the API ever sends an expiry this cannot parse.
  const maxAge = Number.isNaN(expiry)
    ? undefined
    : Math.max(0, Math.floor((expiry - Date.now()) / 1000));

  store.set({
    name: SESSION_COOKIE,
    value: accessToken,
    httpOnly: true,
    secure: await isSecureRequest(),
    sameSite: 'lax',
    path: '/',
    ...(maxAge === undefined ? {} : { maxAge }),
  });
}

/**
 * Removes the session cookie.
 *
 * @remarks
 * The token itself stays valid until it expires - CloudFilm issues no refresh
 * token and keeps no server-side session to revoke, so signing out ends the
 * browser's access and nothing more. Shortening the token lifetime is the lever
 * that matters here, not this function.
 */
export async function clearSession(): Promise<void> {
  const store = await cookies();

  store.delete(SESSION_COOKIE);
}

/**
 * The path the browser asked for, for use as a `returnUrl`.
 *
 * @returns The requested path, or the site root when the middleware header is
 * absent.
 */
export async function currentPathname(): Promise<string> {
  const store = await headers();

  return store.get(PATHNAME_HEADER) ?? '/';
}

/**
 * Builds a sign-in URL that returns the user to where they were.
 *
 * @param returnUrl The path to come back to.
 * @returns A relative sign-in URL.
 */
export function signInUrl(returnUrl: string): string {
  const safe = toInternalPath(returnUrl);

  return safe === '/' ? SIGN_IN_PATH : `${SIGN_IN_PATH}?returnUrl=${encodeURIComponent(safe)}`;
}

/**
 * Reduces a caller-supplied return path to something safe to redirect to.
 *
 * @param value The candidate path, typically from a query string.
 * @returns The path when it is a same-site absolute path, otherwise `/`.
 * @remarks
 * `returnUrl` is attacker-controllable: it arrives in a link that anyone can
 * send. Accepting `https://evil.example` would turn the sign-in page into an
 * open redirect, and accepting `//evil.example` would do the same, because a
 * protocol-relative URL is not a local path however much it looks like one.
 * Only a single leading slash followed by a non-slash, non-backslash character
 * is a path on this site.
 */
export function toInternalPath(value: string | undefined): string {
  if (value === undefined) {
    return '/';
  }

  const trimmed = value.trim();

  if (!trimmed.startsWith('/') || trimmed.startsWith('//') || trimmed.startsWith('/\\')) {
    return '/';
  }

  return trimmed;
}

/**
 * Whether the browser reached the portal over HTTPS.
 *
 * @returns `true` when the request, or the proxy in front of it, used TLS.
 */
async function isSecureRequest(): Promise<boolean> {
  const store = await headers();
  const forwarded = store.get(FORWARDED_PROTO_HEADER)?.split(',')[0]?.trim().toLowerCase();

  return forwarded === 'https';
}
