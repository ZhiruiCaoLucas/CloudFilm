/**
 * Node-runtime start-up checks.
 *
 * Kept out of `instrumentation.ts` because Next compiles that file for the edge
 * runtime as well, and static analysis cannot see that the `process.exit` below
 * is unreachable there. Importing this module dynamically, inside the runtime
 * guard, is the documented way to keep Node-only APIs out of the edge bundle.
 */
export async function verifyStartupConfiguration(): Promise<void> {
  try {
    // Imported inside the try, not at module scope: resolving configuration is
    // what throws, and it happens while the module is being evaluated. A
    // top-level import would raise before this handler could run, and Next would
    // swallow it and keep serving 500s.
    const { assertConfigured } = await import('./lib/env');
    assertConfigured();
  } catch (error) {
    console.error(`[cloudfilm] fatal: ${error instanceof Error ? error.message : String(error)}`);

    // Next catches a throw from the instrumentation hook and leaves a server
    // running that can only answer 500. Exiting instead turns a silently broken
    // container into an obvious crash loop, which an operator can diagnose.
    process.exit(1);
  }
}
