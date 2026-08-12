/**
 * Header names shared between the middleware and the server-side helpers.
 *
 * Kept in its own module, free of `server-only`, because `middleware.ts` runs in
 * the edge runtime and importing the session helpers there would drag Node-only
 * code into it.
 */

/**
 * The path the browser actually asked for, including its query string.
 *
 * Next does not expose the current URL to a Server Component. Without it a
 * component that discovers it needs a signed-in user cannot say where to come
 * back to, so the middleware copies it onto every request.
 */
export const PATHNAME_HEADER = 'x-cloudfilm-pathname';

/**
 * The scheme the *browser* used, as reported by whatever terminated TLS.
 *
 * Decides whether the session cookie is marked `Secure`. Read from the request
 * rather than from `NODE_ENV`, because the Compose stack runs a production
 * build over plain HTTP on localhost: keying off the build mode would set
 * `Secure` there and the browser would silently discard the cookie, producing a
 * login that appears to succeed and then does nothing.
 */
export const FORWARDED_PROTO_HEADER = 'x-forwarded-proto';
