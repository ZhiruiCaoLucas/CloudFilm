/**
 * Next.js start-up hook. Runs once, before the server accepts traffic.
 *
 * Its only job is to resolve configuration eagerly. Without it, a production
 * deployment missing `CLOUDFILM_API_URL` would start, pass its liveness probe,
 * be marked healthy by the orchestrator, and then return 500 for every page -
 * the exact silent failure the strict production validation exists to prevent.
 */
export async function register(): Promise<void> {
  // Also invoked for the edge runtime, where these server-only addresses are
  // neither available nor needed.
  if (process.env.NEXT_RUNTIME !== 'nodejs') {
    return;
  }

  try {
    const { assertConfigured } = await import('./lib/env');
    assertConfigured();
  } catch (error) {
    console.error(`[cloudfilm] fatal: ${error instanceof Error ? error.message : String(error)}`);

    // Next catches a throw from this hook and leaves a server running that can
    // only answer 500. Exiting instead turns a silently broken container into an
    // obvious crash loop, which is what an operator can actually diagnose.
    process.exit(1);
  }
}
