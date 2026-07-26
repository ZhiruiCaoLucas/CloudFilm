import { Button } from '@cloudfilm/ui';

/**
 * Shown when the OHIF deployment cannot be reached.
 *
 * Deliberately not an error. A viewer that is not deployed yet is an expected
 * state of this build, and a clinical application should never put a browser
 * failure page or a stack trace in the reading area.
 */
export function ViewerOffline({ onRetry }: { readonly onRetry: () => void }) {
  return (
    <div className="flex h-full flex-col items-center justify-center gap-5 px-6 text-center">
      <ViewerOfflineIcon />

      <div className="flex flex-col gap-2">
        <p className="text-base font-medium text-slate-200">Viewer Offline</p>
        <p className="text-sm text-slate-400">Waiting for Orthanc / OHIF…</p>
        <p className="max-w-sm text-sm text-slate-500">
          This feature will become available in Sprint 3, when the imaging services are deployed
          alongside the portal.
        </p>
      </div>

      <Button variant="secondary" onClick={onRetry}>
        Check again
      </Button>
    </div>
  );
}

function ViewerOfflineIcon() {
  return (
    <svg
      viewBox="0 0 48 48"
      className="h-16 w-16 text-slate-600"
      role="presentation"
      aria-hidden="true"
      focusable="false"
    >
      <rect
        x="4"
        y="8"
        width="40"
        height="28"
        rx="3"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
      />
      <path d="M18 40h12" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="M24 36v4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <circle cx="24" cy="22" r="7" fill="none" stroke="currentColor" strokeWidth="2" />
      <path d="m19 27 10-10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}
