import {
  API_ROUTES,
  isProblemDetails,
  type CurrentUser,
  type LoginRequest,
  type LoginResponse,
  type Patient,
  type PatientRecord,
  type ProblemDetails,
  type StudyContext,
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

  /** Lists the patients this deployment can serve. */
  listPatients(signal?: AbortSignal): Promise<readonly Patient[]>;

  /**
   * Reads a patient and their studies.
   *
   * Resolves to `null` when no patient matches, rather than throwing. A patient
   * who does not exist is an ordinary outcome of a search, not an error, and the
   * caller renders a not-found page rather than an error page.
   */
  getPatient(medicalRecordNumber: string, signal?: AbortSignal): Promise<PatientRecord | null>;

  /**
   * Reads a study together with its patient and sibling studies.
   *
   * Resolves to `null` when no study matches, for the same reason as
   * {@link ApiClient.getPatient}.
   */
  getStudy(studyInstanceUid: string, signal?: AbortSignal): Promise<StudyContext | null>;

  /**
   * Exchanges credentials for an access token.
   *
   * Resolves to `null` when the credentials are rejected. A failed sign-in is an
   * ordinary outcome of a form submission, not an exceptional one, and the API
   * answers every rejection identically - so there is nothing for a caller to
   * branch on beyond "no token".
   */
  login(request: LoginRequest, signal?: AbortSignal): Promise<LoginResponse | null>;

  /**
   * Reads the signed-in user from the supplied bearer token.
   *
   * Resolves to `null` when no token is attached, or when the token has expired
   * or is otherwise not accepted. That makes it usable as a session probe: a
   * caller asks who this is and gets either an answer or nobody.
   */
  getCurrentUser(signal?: AbortSignal): Promise<CurrentUser | null>;
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

  async function request<T>(path: string, signal?: AbortSignal, body?: unknown): Promise<T> {
    const token = options.getAccessToken?.();
    const headers: Record<string, string> = { Accept: 'application/json' };

    if (token !== undefined) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    if (body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }

    const response = await fetchImpl(`${baseUrl}${path}`, {
      headers,
      cache: 'no-store',
      ...(body === undefined ? {} : { method: 'POST', body: JSON.stringify(body) }),
      ...(signal ? { signal } : {}),
    });

    if (!response.ok) {
      throw await toApiError(response);
    }

    return (await response.json()) as T;
  }

  /**
   * Runs a request, turning the listed statuses into `null` rather than a throw.
   *
   * 404 means "no such record" and 401 means "not signed in". Both are ordinary
   * answers to a question, and a caller that has to wrap every lookup in a
   * try/catch to discover that is a caller that will forget to.
   */
  async function requestOptional<T>(
    path: string,
    absentStatuses: readonly number[],
    signal?: AbortSignal,
    body?: unknown,
  ): Promise<T | null> {
    try {
      return await request<T>(path, signal, body);
    } catch (error) {
      if (error instanceof ApiError && absentStatuses.includes(error.status)) {
        return null;
      }
      throw error;
    }
  }

  return {
    getSystemInfo: (signal) => request<SystemInfo>(API_ROUTES.systemInfo, signal),
    listPatients: (signal) => request<readonly Patient[]>(API_ROUTES.patients, signal),
    getPatient: (medicalRecordNumber, signal) =>
      requestOptional<PatientRecord>(API_ROUTES.patient(medicalRecordNumber), [404], signal),
    getStudy: (studyInstanceUid, signal) =>
      requestOptional<StudyContext>(API_ROUTES.study(studyInstanceUid), [404], signal),
    login: (request_, signal) =>
      requestOptional<LoginResponse>(API_ROUTES.auth.login, [401], signal, request_),
    getCurrentUser: (signal) => requestOptional<CurrentUser>(API_ROUTES.auth.me, [401], signal),
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
