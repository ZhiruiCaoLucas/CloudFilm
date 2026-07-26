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
  // neither available nor needed. The Node-only work lives behind a dynamic
  // import so it is never compiled into the edge bundle.
  if (process.env.NEXT_RUNTIME !== 'nodejs') {
    return;
  }

  const { verifyStartupConfiguration } = await import('./instrumentation-node');
  await verifyStartupConfiguration();
}
