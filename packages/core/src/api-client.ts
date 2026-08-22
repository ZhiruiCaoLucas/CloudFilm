import {
  API_ROUTES,
  isProblemDetails,
  STUDY_SEARCH,
  type CurrentUser,
  type LoginRequest,
  type LoginResponse,
  type Patient,
  type PatientRecord,
  type ProblemDetails,
  type RecentPatient,
  type StudyContext,
  type StudyAnalysis,
  type StudyReadState,
  type StudySearchQuery,
  type StudySearchResult,
  type SystemInfo,
  type ViewerSession,
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

  /**
   * Lists the patients the signed-in user opened most recently.
   *
   * Scoped to the caller by the API, from the bearer token. There is no
   * parameter for whose history to read, because there is no such request.
   */
  getRecentPatients(signal?: AbortSignal): Promise<readonly RecentPatient[]>;

  /**
   * Reports whether the signed-in user has marked a study read.
   *
   * Scoped to the caller by the API, from the bearer token. A study nobody has
   * marked answers `{ read: false }` rather than failing, so there is no
   * "unknown" case for a caller to handle.
   */
  getStudyReadState(studyInstanceUid: string, signal?: AbortSignal): Promise<StudyReadState>;

  /**
   * Marks a study read for the signed-in user.
   *
   * Idempotent: marking an already-read study succeeds and changes nothing. The
   * API records an audit event only when the state actually changes.
   */
  markStudyRead(studyInstanceUid: string, signal?: AbortSignal): Promise<StudyReadState>;

  /**
   * Marks a study unread for the signed-in user.
   *
   * Idempotent, for the same reason as {@link ApiClient.markStudyRead}.
   */
  markStudyUnread(studyInstanceUid: string, signal?: AbortSignal): Promise<StudyReadState>;

  /**
   * Finds studies by patient name, accession number or study date.
   *
   * Rejects a query with no criteria at all: the API answers 400 rather than
   * paging through the whole archive, and this client does not paper over that.
   * An invalid page, page size or date is refused the same way, so a caller
   * should be ready for an {@link ApiError} with status 400 and show its
   * message.
   *
   * The archive's own vocabulary - wildcards, limits, offsets - is not part of
   * this call and cannot be smuggled through it.
   */
  searchStudies(query: StudySearchQuery, signal?: AbortSignal): Promise<StudySearchResult>;

  /**
   * Issues a viewer session for one study to the signed-in caller.
   *
   * Scoped to the study being opened, never to everything the caller may see:
   * the result goes into a cookie that third-party viewer code runs alongside,
   * and a permit for one study is the most that cookie should be worth.
   *
   * Resolves to `null` when the caller may not see the study, for the same
   * reason as {@link ApiClient.getStudy} - the API answers "no such study"
   * either way, deliberately.
   */
  createViewerSession(
    studyInstanceUid: string,
    signal?: AbortSignal,
  ): Promise<ViewerSession | null>;

  /**
   * Sends one study to the analysis service and returns what it reports.
   *
   * No request body: the study comes from the path, and the bytes are fetched by
   * the API from the archive it already trusts. There is nowhere in this call to
   * name a different study, an address, or a file.
   *
   * Rejects rather than resolving to `null` for every unhappy answer, because
   * they do not mean the same thing to a reader: `404` for a study that cannot be
   * analysed, `501` for a deployment with no analysis service, `500` for a
   * failure. The caller decides which of those is worth saying out loud.
   */
  analyseStudy(studyInstanceUid: string, signal?: AbortSignal): Promise<StudyAnalysis>;

  /**
   * Reads the analysis CloudFilm stored the last time this study was analysed.
   *
   * Resolves to `null` when the study has never been analysed, for the same
   * reason as {@link ApiClient.getStudy}: the API answers "no such record" and
   * "not your study" identically and on purpose, and a caller that had to
   * try/catch to discover that is a caller that will forget to.
   *
   * Sends nothing to the analysis service and cannot start one. It is safe to
   * call on every page load, which is the point - a reader who has already had
   * a study analysed should not have to ask for it again.
   */
  getStudyAnalysis(studyInstanceUid: string, signal?: AbortSignal): Promise<StudyAnalysis | null>;

  /**
   * Exchanges a share token for a viewer session.
   *
   * Anonymous: no bearer token is required and none is used. Resolves to `null`
   * for a token that is unknown, expired, withdrawn or not permitted to view -
   * the API answers all four identically, and this client keeps them
   * indistinguishable rather than inventing a reason.
   */
  redeemShareLink(token: string, signal?: AbortSignal): Promise<ViewerSession | null>;
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

  /**
   * Sends one request.
   *
   * `method` exists for the routes that change something without sending
   * anything - a read mark is stated by the verb and the path alone. When it is
   * omitted the original rule still holds: a body means POST, and no body means
   * GET.
   */
  async function request<T>(
    path: string,
    signal?: AbortSignal,
    body?: unknown,
    method?: string,
  ): Promise<T> {
    const token = options.getAccessToken?.();
    const headers: Record<string, string> = { Accept: 'application/json' };

    if (token !== undefined) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    if (body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }

    const verb = method ?? (body === undefined ? undefined : 'POST');

    const response = await fetchImpl(`${baseUrl}${path}`, {
      headers,
      cache: 'no-store',
      ...(verb === undefined ? {} : { method: verb }),
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
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
    method?: string,
  ): Promise<T | null> {
    try {
      return await request<T>(path, signal, body, method);
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
    getRecentPatients: (signal) =>
      request<readonly RecentPatient[]>(API_ROUTES.access.recentPatients, signal),
    getStudyReadState: (studyInstanceUid, signal) =>
      request<StudyReadState>(API_ROUTES.studyReadState(studyInstanceUid), signal),
    markStudyRead: (studyInstanceUid, signal) =>
      request<StudyReadState>(
        API_ROUTES.studyReadState(studyInstanceUid),
        signal,
        undefined,
        'PUT',
      ),
    markStudyUnread: (studyInstanceUid, signal) =>
      request<StudyReadState>(
        API_ROUTES.studyReadState(studyInstanceUid),
        signal,
        undefined,
        'DELETE',
      ),
    searchStudies: (query, signal) =>
      request<StudySearchResult>(`${API_ROUTES.studySearch}?${searchParameters(query)}`, signal),
    analyseStudy: (studyInstanceUid, signal) =>
      request<StudyAnalysis>(API_ROUTES.studyAnalysis(studyInstanceUid), signal, undefined, 'POST'),
    getStudyAnalysis: (studyInstanceUid, signal) =>
      requestOptional<StudyAnalysis>(API_ROUTES.studyAnalysis(studyInstanceUid), [404], signal),
    createViewerSession: (studyInstanceUid, signal) =>
      requestOptional<ViewerSession>(
        API_ROUTES.viewerSession(studyInstanceUid),
        [401, 404],
        signal,
        undefined,
        'POST',
      ),
    redeemShareLink: (token, signal) =>
      requestOptional<ViewerSession>(
        API_ROUTES.shareViewerSession(token),
        [404],
        signal,
        undefined,
        'POST',
      ),
  };
}

/**
 * Renders a search query as a query string.
 *
 * Blank criteria are omitted rather than sent empty, so a form field the user
 * cleared does not become a condition that matches nothing. Paging is always
 * sent: a request that omits it silently gets the server's defaults, and a
 * result page that does not know which page it is cannot be paged from.
 *
 * `URLSearchParams` does the encoding, so a name containing a space, an
 * ampersand or a caret survives the trip intact.
 */
function searchParameters(query: StudySearchQuery): URLSearchParams {
  const parameters = new URLSearchParams();

  const criteria: readonly (readonly [string, string | undefined])[] = [
    ['patientName', query.patientName],
    ['accessionNumber', query.accessionNumber],
    ['studyDateFrom', query.studyDateFrom],
    ['studyDateTo', query.studyDateTo],
  ];

  for (const [name, value] of criteria) {
    if (value !== undefined && value.trim().length > 0) {
      parameters.set(name, value.trim());
    }
  }

  parameters.set('page', String(query.page ?? 0));
  parameters.set('pageSize', String(query.pageSize ?? STUDY_SEARCH.defaultPageSize));

  return parameters;
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
