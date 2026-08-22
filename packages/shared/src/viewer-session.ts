/**
 * A short-lived permit to view one study in the viewer.
 *
 * The viewer reaches DICOM images through its own proxy rather than through the
 * API, so nothing the portal does to an API request can limit what it fetches.
 * A viewer session is what closes that: the proxy asks the API, on every
 * DICOMweb request, whether the session in the browser's cookie admits it.
 *
 * The same object serves a signed-in clinician and an anonymous share-link
 * recipient. They differ where the session is issued - one is checked against
 * the caller's entitlements, the other against a token - and are identical
 * afterwards, which is what keeps the proxy's rule to a single case.
 */
export interface ViewerSession {
  /** Opaque identifier, to be placed in {@link VIEWER_SESSION_COOKIE}. */
  readonly sessionId: string;
  /** The one study this session admits. */
  readonly studyInstanceUid: string;
  /** What the session permits, from the grant vocabulary. */
  readonly permission: string;
  /** When it stops working, as an ISO 8601 instant. */
  readonly expiresAtUtc: string;
}

/**
 * The cookie the browser presents to the imaging proxy.
 *
 * Must match the name the API reads. Cookies ignore ports, which is how one set
 * by the portal reaches the viewer on another port of the same host - and why a
 * deployment that puts the two on different hostnames must give them a shared
 * parent domain.
 */
export const VIEWER_SESSION_COOKIE = 'cloudfilm_viewer';
