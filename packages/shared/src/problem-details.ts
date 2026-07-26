/**
 * RFC 9457 problem details, the single error shape returned by every CloudFilm service.
 */
export interface ProblemDetails {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly instance?: string;
  readonly correlationId?: string;
}

/**
 * Narrows an unknown response body to {@link ProblemDetails}.
 *
 * Used to distinguish a structured server error from an unexpected payload without
 * trusting the response body's shape.
 */
export function isProblemDetails(value: unknown): value is ProblemDetails {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Record<string, unknown>;

  return (
    (typeof candidate['title'] === 'string' || typeof candidate['detail'] === 'string') &&
    (candidate['status'] === undefined || typeof candidate['status'] === 'number')
  );
}
