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
