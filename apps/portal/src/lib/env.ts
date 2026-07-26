/**
 * Runtime configuration for the portal.
 *
 * The rule is asymmetric on purpose:
 *
 * - **Development and build** fall back to the local port map, so a fresh clone
 *   runs with `pnpm install && pnpm dev` and no configuration step. A developer
 *   who has not started the backend yet should see a portal that says so, not a
 *   stack trace.
 * - **Production runtime** has no fallbacks at all. A missing variable throws
 *   during module load, which crashes the server before it can serve a single
 *   request. That mirrors `ValidateOnStart()` in the API: a misconfigured
 *   hospital deployment must fail loudly and immediately, never silently point
 *   at localhost.
 */

type EnvName = 'CLOUDFILM_API_URL' | 'CLOUDFILM_VIEWER_URL';

/**
 * Local defaults. These match the port map documented in the README, which is
 * shared by `docker compose up` and by running the services directly with
 * `dotnet run` — so the same default works either way.
 */
const DEVELOPMENT_DEFAULTS: Record<EnvName, string> = {
  CLOUDFILM_API_URL: 'http://localhost:8080',
  CLOUDFILM_VIEWER_URL: 'http://localhost:8081',
};

/**
 * True only when this process is serving production traffic.
 *
 * `next build` also runs with `NODE_ENV=production`, but a build must not
 * require a reachable deployment's configuration — the same image is built once
 * and run in several environments. `NEXT_PHASE` distinguishes the two.
 *
 * If Next ever stopped setting `NEXT_PHASE`, a configuration-free build would
 * start throwing and CI would fail. That is the intended direction of failure:
 * loud at build time, never a silent localhost fallback in production.
 */
const isProductionRuntime =
  process.env.NODE_ENV === 'production' && process.env['NEXT_PHASE'] !== 'phase-production-build';

const warned = new Set<EnvName>();

function read(name: EnvName): string {
  const value = process.env[name]?.trim();

  if (value !== undefined && value.length > 0) {
    return value.replace(/\/+$/, '');
  }

  if (isProductionRuntime) {
    throw new Error(
      `Environment variable ${name} is required in production but was not set. ` +
        'See .env.example for the full list.',
    );
  }

  if (!warned.has(name)) {
    warned.add(name);
    console.warn(`[cloudfilm] ${name} is not set; using ${DEVELOPMENT_DEFAULTS[name]}`);
  }

  return DEVELOPMENT_DEFAULTS[name];
}

export const env = {
  /**
   * Address of the CloudFilm API.
   *
   * Read on the server only. The browser never learns this address, which is
   * what keeps the API — and everything behind it — off the public network.
   */
  apiUrl: read('CLOUDFILM_API_URL'),

  /**
   * Address of the OHIF Viewer deployment.
   *
   * Read on the server, then sent to the browser as an iframe `src`, so it must
   * be the address a *browser* can reach — not a container name.
   *
   * Deliberately not prefixed `NEXT_PUBLIC_`: that would inline the value into
   * the bundle at build time and tie one portal image to one environment. Read
   * at runtime instead, the same image serves every hospital.
   */
  viewerUrl: read('CLOUDFILM_VIEWER_URL'),
} as const;

/** True when the portal is running in development. */
export const isDevelopment = process.env.NODE_ENV !== 'production';

/**
 * Forces configuration to be resolved, and logs what was resolved.
 *
 * `env` is evaluated when this module is first imported - which, in Next, is
 * lazily, on the first request that reaches a route using it. That is too late:
 * a misconfigured production deployment would boot, answer its liveness probe
 * successfully, and then fail every page. `instrumentation.ts` calls this at
 * start-up so the process dies before it is ever marked healthy.
 */
export function assertConfigured(): void {
  console.info(`[cloudfilm] configuration resolved: api=${env.apiUrl} viewer=${env.viewerUrl}`);
}
