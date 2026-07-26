export const dynamic = 'force-dynamic';

/**
 * Container liveness probe.
 *
 * Answers only for the portal process itself. Dependency health is reported by the
 * API's readiness probe, so a transient database outage does not cause the
 * orchestrator to restart the web tier.
 */
export function GET(): Response {
  return Response.json({ status: 'Healthy' }, { status: 200 });
}
