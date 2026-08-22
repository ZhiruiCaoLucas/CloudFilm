'use server';

import { ApiError } from '@cloudfilm/core';
import type { StudyAnalysis } from '@cloudfilm/shared';
import { serverApiClient } from '@/lib/api';

/**
 * What asking for an analysis produced.
 *
 * A value rather than an exception, following the search page's `'refused'`
 * sentinel: the outcomes below are ordinary answers a reader can be shown, and a
 * client component that had to inspect thrown errors would be one that could
 * accidentally render a server message.
 */
export type AnalysisOutcome =
  | { readonly kind: 'analysed'; readonly analysis: StudyAnalysis }
  | { readonly kind: 'not-configured' }
  | { readonly kind: 'unavailable' }
  | { readonly kind: 'failed' };

/**
 * Sends one study to the analysis service.
 *
 * @param studyInstanceUid The study the workspace is showing.
 * @returns What happened, in terms the panel can render.
 * @remarks
 * A Server Action rather than a browser `fetch`, for the same reason the read
 * mark is one: the bearer token lives in an `HttpOnly` cookie and must never
 * reach the browser. `serverApiClient()` reads that cookie on the server and
 * attaches the token itself, so the browser sends only this study identifier.
 *
 * Takes no result from the caller and returns no server text. The API's own
 * statuses are translated into four outcomes here, and the panel chooses a
 * translated sentence for each - so nothing a downstream service said about a
 * patient's imaging can reach a screen.
 *
 * Unlike {@link setStudyReadState}, this returns its result instead of asking
 * the caller to refresh. The workspace does read the stored analysis during its
 * server render, so a refresh would now show the new result - but it would also
 * re-render the whole page, and the panel already has the answer in hand. The
 * return value is the same record the API stored, so the two paths cannot
 * disagree.
 */
export async function analyseStudy(studyInstanceUid: string): Promise<AnalysisOutcome> {
  const client = await serverApiClient();

  try {
    const analysis = await client.analyseStudy(studyInstanceUid);

    return { kind: 'analysed', analysis };
  } catch (error) {
    if (error instanceof ApiError) {
      // 401 and 403 are not ApiErrors by the time they arrive here: the portal's
      // client turns them into redirects, which throw NEXT_REDIRECT and pass
      // through this block untouched. That is deliberate - a caller who needs to
      // sign in must not be told that the analysis failed.
      if (error.status === 501) {
        return { kind: 'not-configured' };
      }

      if (error.status === 404) {
        // Denied, absent from the archive, and holding nothing to analyse are
        // one answer by design. The API refuses to distinguish them and neither
        // does this.
        return { kind: 'unavailable' };
      }

      return { kind: 'failed' };
    }

    throw error;
  }
}
