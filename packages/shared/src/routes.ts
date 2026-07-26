/**
 * API routes, declared once so that no route string is duplicated in the portal.
 */
export const API_ROUTES = {
  systemInfo: '/api/v1/system/info',
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
