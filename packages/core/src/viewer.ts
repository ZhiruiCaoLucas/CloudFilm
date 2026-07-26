/** The information the OHIF Viewer needs to open a study. */
export interface ViewerLaunchRequest {
  /** Absolute base address of the OHIF deployment, without a trailing slash. */
  readonly viewerBaseUrl: string;
  /** DICOM Study Instance UID. */
  readonly studyInstanceUid: string;
}

/**
 * Builds the URL that opens a study in OHIF.
 *
 * OHIF is treated as an unmodified black box addressed by URL. Keeping the
 * integration to a URL contract is what lets CloudFilm upgrade OHIF without touching
 * portal code, and what would let the viewer be replaced later.
 */
export function buildViewerUrl(request: ViewerLaunchRequest): string {
  const base = request.viewerBaseUrl.replace(/\/+$/, '');
  const studyInstanceUid = request.studyInstanceUid.trim();

  if (studyInstanceUid.length === 0) {
    throw new Error('studyInstanceUid must not be empty.');
  }

  return `${base}/viewer?StudyInstanceUIDs=${encodeURIComponent(studyInstanceUid)}`;
}
