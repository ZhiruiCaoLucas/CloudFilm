import type { MessageKey } from '@cloudfilm/shared';

/**
 * The state the sign-in form renders between attempts.
 *
 * Deliberately not in `actions.ts`. A `'use server'` module may export nothing
 * but async functions - every export becomes a callable server endpoint - so a
 * plain object living there would either break the build or, worse, widen that
 * boundary. The shape and its initial value are ordinary module-level data, so
 * they belong in an ordinary module that both the action and the form import.
 */
export interface SignInState {
  /**
   * The message *key* to show, or `null` before the first attempt.
   *
   * A key rather than a message. The action runs on the server and the form
   * renders on the client, so resolving text inside the action would freeze it
   * in whatever locale was active when that request was handled, and it would
   * then be stale the moment the reader switched language. The key travels; the
   * form resolves it in the language it is currently rendering.
   *
   * One key for every rejection. The API deliberately does not distinguish an
   * unknown account from a wrong password, and reintroducing that distinction
   * here would undo the account enumeration defence the API is built around.
   */
  readonly error: MessageKey | null;
}

/** The state the form starts in. */
export const initialSignInState: SignInState = { error: null };
