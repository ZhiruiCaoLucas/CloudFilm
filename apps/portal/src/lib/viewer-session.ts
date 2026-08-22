import 'server-only';
import { headers } from 'next/headers';
import type { NextResponse } from 'next/server';
import { VIEWER_SESSION_COOKIE, type ViewerSession } from '@cloudfilm/shared';
import { FORWARDED_PROTO_HEADER } from './request-headers';

/**
 * Attaches a viewer session to a redirect.
 *
 * @param response The redirect the browser is about to follow.
 * @param session The session issued by the API.
 * @returns The same response, with the cookie set.
 * @remarks
 * Written onto the response rather than through the cookie store, because it is
 * always set alongside a redirect and the two must arrive together: the browser
 * follows the redirect into the viewer, and the viewer's very first DICOMweb
 * request must already carry the cookie.
 *
 * `HttpOnly`, so the third-party viewer code that runs beside it cannot read it.
 * Its lifetime comes from the session's own expiry, so a cookie cannot outlive
 * the permit it carries. `Lax` is sufficient and correct: the viewer is a
 * different port, not a different site, and cross-site requests have no business
 * carrying this.
 *
 * Host-only, with no `Domain`. That is the tightest scope available and it is
 * what makes the same-hostname deployment requirement real: cookies ignore
 * ports, so the viewer on another port of this host receives it, and a viewer on
 * another hostname does not. See `SECURITY.md`.
 */
export async function attachViewerSession(
  response: NextResponse,
  session: ViewerSession,
): Promise<NextResponse> {
  const expiry = Date.parse(session.expiresAtUtc);
  const maxAge = Number.isNaN(expiry)
    ? undefined
    : Math.max(0, Math.floor((expiry - Date.now()) / 1000));

  response.cookies.set({
    name: VIEWER_SESSION_COOKIE,
    value: session.sessionId,
    httpOnly: true,
    secure: await isSecureRequest(),
    sameSite: 'lax',
    path: '/',
    ...(maxAge === undefined ? {} : { maxAge }),
  });

  return response;
}

/**
 * Reports whether the browser reached the portal over HTTPS.
 *
 * @remarks
 * Duplicated from the session module, which keeps its copy private. The
 * alternative was to widen that module's surface so a second caller could reach
 * one three-line function, and the session cookie's rules are worth keeping
 * closed.
 */
async function isSecureRequest(): Promise<boolean> {
  const store = await headers();
  const forwarded = store.get(FORWARDED_PROTO_HEADER)?.split(',')[0]?.trim().toLowerCase();

  return forwarded === 'https';
}
