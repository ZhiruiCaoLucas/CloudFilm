import { createApiClient, type ApiClient } from '@cloudfilm/core';
import { env } from './env';

/**
 * The API client used by server components.
 *
 * The portal's only route to application data. Calls travel over the internal
 * network, so no credentials are attached here; user scoped requests will supply
 * a bearer token explicitly once authentication exists.
 */
export function serverApiClient(): ApiClient {
  return createApiClient({ baseUrl: env.apiUrl });
}
