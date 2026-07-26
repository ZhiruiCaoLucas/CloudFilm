import {
  API_ROUTES,
  isProblemDetails,
  type ProblemDetails,
  type SystemInfo,
} from '@cloudfilm/shared';

/**
 * A failed CloudFilm API call.
 *
 * Carries the server correlation id so that a browser side failure can be traced to
 * the exact server log entry, which matters when a hospital reports an incident.
 */
export class ApiError extends Error {
  public readonly status: number;
  public readonly correlationId: string | undefined;

  public constructor(status: number, message: string, correlationId?: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.correlationId = correlationId;
  }
}

/** Configuration for {@link createApiClient}. */
export interface ApiClientOptions {
  /** Absolute base address of the CloudFilm API, without a trailing slash. */
  readonly baseUrl: string;
  /** Bearer token supplier. Returning `undefined` sends an anonymous request. */
  readonly getAccessToken?: () => string | undefined;
  /** Fetch implementation, injected so the client works in the browser, on the server and under test. */
  readonly fetch?: typeof globalThis.fetch;
}

/** The typed CloudFilm API surface available to the portal. */
export interface ApiClient {
  /** Reads the API build and environment. */
  getSystemInfo(signal?: AbortSignal): Promise<SystemInfo>;
}

/**
 * Creates a CloudFilm API client.
 *
 * Transport concerns - base address, authentication header, error translation - live
 * here rather than in React components, which keeps them testable without a DOM.
 */
export function createApiClient(options: ApiClientOptions): ApiClient {
  const baseUrl = options.baseUrl.replace(/\/+$/, '');
  const fetchImpl = options.fetch ?? globalThis.fetch;

  async function request<T>(path: string, signal?: AbortSignal): Promise<T> {
    const token = options.getAccessToken?.();
    const headers: Record<string, string> = { Accept: 'application/json' };

    if (token !== undefined) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetchImpl(`${baseUrl}${path}`, {
      headers,
      cache: 'no-store',
      ...(signal ? { signal } : {}),
    });

    if (!response.ok) {
      throw await toApiError(response);
    }

    return (await response.json()) as T;
  }

  return {
    getSystemInfo: (signal) => request<SystemInfo>(API_ROUTES.systemInfo, signal),
  };
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails | undefined;

  try {
    const body: unknown = await response.json();
    problem = isProblemDetails(body) ? body : undefined;
  } catch {
    problem = undefined;
  }

  return new ApiError(
    response.status,
    problem?.title ?? problem?.detail ?? `Request failed with status ${response.status}.`,
    problem?.correlationId,
  );
}
