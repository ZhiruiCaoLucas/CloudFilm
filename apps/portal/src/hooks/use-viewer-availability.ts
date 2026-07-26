'use client';

import { useCallback, useEffect, useState } from 'react';

/** Whether the OHIF deployment is reachable from this browser. */
export type ViewerAvailability = 'connecting' | 'online' | 'offline';

/** How long to wait for the viewer origin before treating it as down. */
const PROBE_TIMEOUT_MS = 4_000;

/** The outcome of one probe, tagged with the probe it answers. */
interface ProbeResult {
  readonly key: string;
  readonly status: 'online' | 'offline';
}

/**
 * Reports whether the OHIF deployment is serving.
 *
 * The probe runs in the browser, not on the server, because the browser is the
 * only party whose reachability matters: the viewer URL is a public address, and
 * the portal's server container may not be able to resolve it at all.
 *
 * `mode: 'no-cors'` is what makes this work cross-origin. The response is opaque
 * and its contents unreadable, which is fine — the question is only whether the
 * origin answered. A running server resolves; a refused connection rejects.
 *
 * The alternative, mounting the iframe and waiting for `load`, cannot
 * distinguish a loaded viewer from the browser's own error page, and would put
 * "This site can't be reached" in front of a clinician.
 */
export function useViewerAvailability(viewerBaseUrl: string): {
  readonly status: ViewerAvailability;
  readonly retry: () => void;
} {
  const [result, setResult] = useState<ProbeResult | null>(null);
  const [attempt, setAttempt] = useState(0);

  const probeKey = `${attempt}:${viewerBaseUrl}`;

  // Derived rather than stored: any result belonging to an earlier probe is by
  // definition stale, so changing the URL or pressing retry returns to
  // "connecting" without an effect writing state and forcing a second render.
  const status: ViewerAvailability =
    result !== null && result.key === probeKey ? result.status : 'connecting';

  useEffect(() => {
    const controller = new AbortController();
    const timeout = setTimeout(() => {
      controller.abort();
    }, PROBE_TIMEOUT_MS);
    let active = true;

    fetch(viewerBaseUrl, {
      mode: 'no-cors',
      cache: 'no-store',
      signal: controller.signal,
    })
      .then(() => {
        if (active) {
          setResult({ key: probeKey, status: 'online' });
        }
      })
      .catch(() => {
        if (active) {
          setResult({ key: probeKey, status: 'offline' });
        }
      })
      .finally(() => {
        clearTimeout(timeout);
      });

    return () => {
      active = false;
      controller.abort();
      clearTimeout(timeout);
    };
  }, [probeKey, viewerBaseUrl]);

  const retry = useCallback(() => {
    setAttempt((previous) => previous + 1);
  }, []);

  return { status, retry };
}
