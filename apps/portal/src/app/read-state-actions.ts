'use server';

import { serverApiClient } from '@/lib/api';

/**
 * Records whether the signed-in user has read a study.
 *
 * @param studyInstanceUid The study being marked.
 * @param read Whether it should now count as read.
 * @remarks
 * A Server Action rather than a client `fetch`, for the same reason sign-in is
 * one: the bearer token lives in an `HttpOnly` cookie and must never reach the
 * browser. `serverApiClient()` reads that cookie on the server and attaches the
 * token itself, so the browser sends only this call's two arguments.
 *
 * Takes no user identifier and there is nowhere to put one. The API derives the
 * account from the token's `sub` claim, so a caller cannot mark a study read on
 * somebody else's behalf even if it wanted to.
 *
 * Returns nothing. The caller refreshes the route afterwards and re-reads the
 * state from the server, rather than trusting a value returned here - if the two
 * ever disagreed, the one the next page load shows is the one that is true.
 */
export async function setStudyReadState(studyInstanceUid: string, read: boolean): Promise<void> {
  const client = await serverApiClient();

  if (read) {
    await client.markStudyRead(studyInstanceUid);
  } else {
    await client.markStudyUnread(studyInstanceUid);
  }
}
