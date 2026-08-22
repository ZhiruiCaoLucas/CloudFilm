import { NextResponse } from 'next/server';
import { buildViewerUrl, createApiClient } from '@cloudfilm/core';
import { env } from '@/lib/env';
import { attachViewerSession } from '@/lib/viewer-session';
import type { ViewerSession } from '@cloudfilm/shared';

export const dynamic = 'force-dynamic';

/**
 * Where a share link lands.
 *
 * The recipient has no account and is offered none. The token in the path is the
 * whole of their authority: it is exchanged, once, for a viewer session bound to
 * the one study the link names, and the browser is sent into the viewer holding
 * a cookie rather than a token.
 *
 * The token stops here. It is never put in the cookie, never reaches the viewer
 * and never appears in a URL the viewer loads, so it cannot leak through a
 * referrer header or a screenshot of an address bar taken after the study opens.
 *
 * Every refusal is the same empty 404 - unknown, expired, withdrawn, or a link
 * that does not permit viewing - because anything else would answer questions
 * about links to anyone willing to try them.
 */
export async function GET(
  _request: Request,
  { params }: { readonly params: Promise<{ readonly token: string }> },
): Promise<Response> {
  const { token } = await params;

  // A bare client, deliberately: the portal's usual one attaches the visitor's
  // session token if they happen to have one, and redirects on 401. Neither
  // belongs on a route whose entire premise is that the caller is anonymous.
  const client = createApiClient({ baseUrl: env.apiUrl });

  const session: ViewerSession | null = await client.redeemShareLink(token);

  if (session === null) {
    return new NextResponse(null, { status: 404 });
  }

  const viewer = buildViewerUrl({
    viewerBaseUrl: env.viewerUrl,
    studyInstanceUid: session.studyInstanceUid,
  });

  return attachViewerSession(NextResponse.redirect(viewer), session);
}
