import { PLACEHOLDER_VIEWER_TELEMETRY } from '@cloudfilm/shared/mock';
import type { Study } from '@cloudfilm/shared';
import { cn } from '@cloudfilm/ui';
import type { ViewerAvailability } from '@/hooks/use-viewer-availability';

const STATUS_LABELS: Record<ViewerAvailability, string> = {
  connecting: 'Connecting',
  online: 'Online',
  offline: 'Offline',
};

const STATUS_DOT: Record<ViewerAvailability, string> = {
  connecting: 'bg-amber-500',
  online: 'bg-emerald-500',
  offline: 'bg-slate-500',
};

/**
 * Workstation status strip.
 *
 * Viewer status and image count are real. Zoom and window level are properties
 * of a loaded image and can only come from the viewer itself, so they are
 * placeholders until Sprint 3 — shown so the bar's shape is settled, and marked
 * in the markup so they are not mistaken for live readings.
 */
export function WorkspaceStatusBar({
  study,
  viewerStatus,
}: {
  readonly study: Study;
  readonly viewerStatus: ViewerAvailability;
}) {
  return (
    <footer
      aria-label="Workspace status"
      className="border-border bg-surface-raised text-muted flex shrink-0 flex-wrap items-center gap-x-6 gap-y-1 border-t px-4 py-2 text-xs"
    >
      <div className="flex items-center gap-2">
        <span
          aria-hidden="true"
          className={cn('h-2 w-2 shrink-0 rounded-full', STATUS_DOT[viewerStatus])}
        />
        <span>Viewer</span>
        <span className="text-foreground font-medium">{STATUS_LABELS[viewerStatus]}</span>
      </div>

      <StatusField label="Study UID" value={study.studyInstanceUid} mono truncate />
      <StatusField label="Images" value={String(study.instanceCount)} mono />
      <StatusField label="Zoom" value={PLACEHOLDER_VIEWER_TELEMETRY.zoom} mono pending />
      <StatusField label="W/L" value={PLACEHOLDER_VIEWER_TELEMETRY.windowLevel} mono pending />

      {/* Discoverability: a shortcut nobody is told about is a shortcut nobody uses. */}
      <span className="ml-auto hidden lg:inline">
        <kbd className="bg-surface-overlay text-foreground rounded px-1 font-mono">[</kbd>
        {' / '}
        <kbd className="bg-surface-overlay text-foreground rounded px-1 font-mono">]</kbd> previous
        / next study
      </span>
    </footer>
  );
}

function StatusField({
  label,
  value,
  mono = false,
  truncate = false,
  pending = false,
}: {
  readonly label: string;
  readonly value: string;
  readonly mono?: boolean;
  readonly truncate?: boolean;
  readonly pending?: boolean;
}) {
  return (
    <div className={cn('flex items-center gap-2', truncate && 'min-w-0')}>
      <span>{label}</span>
      <span
        title={pending ? 'Reported by the viewer once a study is loaded' : undefined}
        className={cn(
          'text-foreground font-medium',
          mono && 'font-mono',
          truncate && 'truncate',
          pending && 'text-muted',
        )}
      >
        {value}
      </span>
    </div>
  );
}
