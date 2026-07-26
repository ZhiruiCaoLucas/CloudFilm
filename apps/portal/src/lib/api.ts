import { createApiClient, type ApiClient } from '@cloudfilm/core';
import { env } from './env';

/**
 * The API client used by server components and route handlers.
 *
 * Calls travel over the internal Docker network, so no credentials are attached
 * here; user scoped requests will supply a bearer token explicitly.
 */
export function serverApiClient(): ApiClient {
  return createApiClient({ baseUrl: env.apiUrl });
}
