/**
 * Stand-in viewer telemetry for the workspace status bar.
 *
 * Zoom and window level are properties of a *loaded image*, so they can only
 * come from the viewer itself. Sprint 3 will receive them from OHIF; until an
 * image is actually on screen there is nothing real to report, and these
 * placeholders make the status bar's shape visible without inventing a number
 * that looks authoritative.
 */
export const PLACEHOLDER_VIEWER_TELEMETRY = {
  /** Magnification, as OHIF would report it. */
  zoom: '100%',

  /** Window width and window centre, in Hounsfield units. */
  windowLevel: 'W 400 / L 40',
} as const;
