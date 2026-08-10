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
