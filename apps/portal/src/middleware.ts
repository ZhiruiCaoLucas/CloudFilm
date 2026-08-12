import { NextResponse, type NextRequest } from 'next/server';
import { PATHNAME_HEADER } from '@/lib/request-headers';

/**
 * Copies the requested path onto the request headers.
 *
 * Deliberately does **not** gate anything. Enforcing authentication on pages
 * belongs with enforcing it on the PHI endpoints, and doing one without the
 * other would be theatre: the API still answers anonymously, so a redirect here
 * would inconvenience clinicians without protecting a single record.
 *
 * All this does is make the current URL visible to Server Components, which
 * Next otherwise does not expose, so that a component which discovers it needs
 * a session can build a `returnUrl`.
 */
export function middleware(request: NextRequest) {
  const headers = new Headers(request.headers);

  headers.set(PATHNAME_HEADER, `${request.nextUrl.pathname}${request.nextUrl.search}`);

  return NextResponse.next({ request: { headers } });
}

export const config = {
  // Static assets and the portal's own liveness probe gain nothing from this,
  // and the probe must stay as cheap as possible.
  matcher: ['/((?!_next/static|_next/image|favicon.ico|api/health).*)'],
};
