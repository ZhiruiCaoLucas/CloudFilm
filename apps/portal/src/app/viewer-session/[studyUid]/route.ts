import { NextResponse } from 'next/server';
import { buildViewerUrl } from '@cloudfilm/core';
import { serverApiClient } from '@/lib/api';
import { env } from '@/lib/env';
import { attachViewerSession } from '@/lib/viewer-session';
import type { ViewerSession } from '@cloudfilm/shared';

export const dynamic = 'force-dynamic';

/**
 * The door the workspace's viewer frame goes through.
 *
 * The frame points here rather than straight at the viewer, because entering the
 * viewer now requires a cookie and a page cannot set one while rendering - Next
 * allows that only in a Route Handler or a Server Action, which is the correct
 * restriction. So the frame loads this, which asks the API for a session scoped
 * to the study on screen, sets the cookie and redirects into the viewer.
 *
 * The cost is one extra round trip when a study is opened. The alternative was
 * to let the viewer reach the archive unauthorised, which is what this sprint
 * exists to end.
 *
 * The study identifier is not trusted: the API decides whether this caller may
 * see it, and answers "no such study" when they may not.
 */
export async function GET(
  _request: Request,
  { params }: { readonly params: Promise<{ readonly studyUid: string }> },
): Promise<Response> {
  const { studyUid } = await params;
  const studyInstanceUid = decodeURIComponent(studyUid);

  const client = await serverApiClient();
  const session: ViewerSession | null = await client.createViewerSession(studyInstanceUid);

  if (session === null) {
    // The same answer the study page gives. A distinct status here would tell a
    // caller that a study they may not see exists.
    return new NextResponse(null, { status: 404 });
  }

  const viewer = buildViewerUrl({ viewerBaseUrl: env.viewerUrl, studyInstanceUid });

  return attachViewerSession(NextResponse.redirect(viewer), session);
}
