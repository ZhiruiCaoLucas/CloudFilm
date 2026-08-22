/**
 * API routes, declared once so that no route string is duplicated in the portal.
 */
export const API_ROUTES = {
  systemInfo: '/api/v1/system/info',
  patients: '/api/v1/patients',
  patient: (medicalRecordNumber: string): string =>
    `/api/v1/patients/${encodeURIComponent(medicalRecordNumber)}`,
  study: (studyInstanceUid: string): string =>
    `/api/v1/studies/${encodeURIComponent(studyInstanceUid)}`,
  auth: {
    login: '/api/v1/auth/login',
    me: '/api/v1/auth/me',
  },
  access: {
    recentPatients: '/api/v1/access/recent',
  },

  /**
   * Studies matching a query, across patients.
   *
   * A sibling of the study routes rather than a member of the imaging module's
   * own group: it is the one read that starts from a question instead of an
   * identifier, and it is served by its own endpoint.
   */
  studySearch: '/api/v1/studies/search',

  /**
   * The signed-in user's read mark for one study.
   *
   * Under `/imaging/studies` rather than `/studies`: the read state is
   * CloudFilm's own workflow state and is served by its own module, while
   * `/api/v1/studies/{uid}` belongs to the imaging catalogue.
   */
  studyReadState: (studyInstanceUid: string): string =>
    `/api/v1/imaging/studies/${encodeURIComponent(studyInstanceUid)}/read-state`,
  /**
   * The analysis of one study.
   *
   * Beside the read state and the viewer session, for the same reason: an
   * analysis is CloudFilm's own workflow rather than part of the archive's
   * catalogue.
   *
   * Two verbs on one path, like the read state. `POST` runs an analysis and is
   * not idempotent - each call sends the study to the analysis service and is
   * recorded as having done so. `GET` reads the result CloudFilm stored when it
   * last did, sends nothing anywhere, and answers `404` for a study that has
   * never been analysed.
   */
  studyAnalysis: (studyInstanceUid: string): string =>
    `/api/v1/imaging/studies/${encodeURIComponent(studyInstanceUid)}/analysis`,

  /**
   * A viewer session for one study, for the signed-in caller.
   *
   * Beside the read state rather than under `/studies`, for the same reason:
   * this is CloudFilm's own workflow, not part of the imaging catalogue.
   */
  viewerSession: (studyInstanceUid: string): string =>
    `/api/v1/imaging/studies/${encodeURIComponent(studyInstanceUid)}/viewer-session`,

  /**
   * A viewer session in exchange for a share token.
   *
   * Anonymous. The study is not a parameter - it comes from the link the token
   * resolves to - so a recipient cannot ask for a different one.
   */
  shareViewerSession: (token: string): string =>
    `/api/v1/share/${encodeURIComponent(token)}/viewer-session`,
  healthLive: '/health/live',
  healthReady: '/health/ready',
} as const;

/**
 * Retrieve Service routes.
 */
export const RETRIEVE_ROUTES = {
  hospitals: '/api/v1/hospitals',
  healthLive: '/health/live',
  healthReady: '/health/ready',
} as const;
