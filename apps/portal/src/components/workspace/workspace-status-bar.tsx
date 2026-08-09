import type { Study } from '@cloudfilm/shared';
import { cn } from '@cloudfilm/ui';

/**
 * Workstation status strip.
 *
 * Shows only what CloudFilm actually knows: the study it resolved and the image
 * count the archive reported. Both come from the API.
 *
 * It previously also showed viewer reachability, zoom and window level. All
 * three were wrong on screen at once - the bar read "Viewer Offline" beside a
 * rendering X-ray, and "W 400 / L 40" while the viewer's own overlay read
 * "W: 4095 L: 2048". Zoom and window level belong to a loaded image and only
 * the viewer can report them; reachability came from a probe that a
 * Cross-Origin-Resource-Policy header makes permanently unanswerable. A status
 * bar that contradicts the image beside it is worse than a shorter one, so the
 * three unknowable fields are gone until the viewer can report them for real.
 */
export function WorkspaceStatusBar({ study }: { readonly study: Study }) {
  return (
    <footer
      aria-label="Workspace status"
      className="border-border bg-surface-raised text-muted flex shrink-0 flex-wrap items-center gap-x-6 gap-y-1 border-t px-4 py-2 text-xs"
    >
      <StatusField label="Study UID" value={study.studyInstanceUid} mono truncate />
      <StatusField label="Images" value={String(study.instanceCount)} mono />

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
