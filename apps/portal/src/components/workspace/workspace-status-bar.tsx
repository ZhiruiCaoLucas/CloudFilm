'use client';

import { useTransition } from 'react';
import { useRouter } from 'next/navigation';
import type { Study } from '@cloudfilm/shared';
import { cn } from '@cloudfilm/ui';
import { useTranslations } from '@/components/locale-provider';
import { setStudyReadState } from '@/app/read-state-actions';

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
export function WorkspaceStatusBar({
  study,
  isRead,
}: {
  readonly study: Study;
  readonly isRead: boolean;
}) {
  const t = useTranslations();
  const router = useRouter();
  const [isSaving, startSaving] = useTransition();

  function toggleRead() {
    startSaving(async () => {
      await setStudyReadState(study.studyInstanceUid, !isRead);

      // Re-read the state from the server rather than assuming the request
      // produced the value asked for. The mark is per user and lives in
      // CloudFilm's own table, so the authoritative answer is one round trip
      // away and there is no reason to display a guess instead.
      router.refresh();
    });
  }

  const stateLabel = isRead ? t('workspace.read') : t('workspace.unread');

  return (
    <footer
      aria-label={t('workspace.statusLabel')}
      className="border-border bg-surface-raised text-muted flex min-h-9 shrink-0 flex-wrap items-center gap-x-7 gap-y-1 border-t px-4 py-1.5 text-[0.6875rem]"
    >
      <StatusField label={t('study.studyUid')} value={study.studyInstanceUid} mono truncate />
      <StatusField label={t('study.images')} value={String(study.instanceCount)} mono />

      {/*
        A toggle, not a link: it changes CloudFilm's own state rather than the
        address. aria-pressed carries the current value, so the control announces
        itself as pressed or not rather than relying on colour, and the title
        says what pressing it will do.

        The label is the state, never an optimistic guess at it. This row is what
        a clinician will trust when deciding whether they have already reported a
        study, so it shows what the server last said and nothing else - it goes
        quiet while saving instead of asserting a value that may not have been
        stored.
      */}
      <button
        type="button"
        onClick={toggleRead}
        disabled={isSaving}
        aria-pressed={isRead}
        aria-busy={isSaving}
        title={isRead ? t('workspace.markUnread') : t('workspace.markRead')}
        className={cn(
          'inline-flex min-h-6 items-center gap-1.5 rounded px-2 py-0.5 text-[0.6875rem] font-medium',
          'ring-1 transition-[color,background-color,box-shadow] duration-200 ring-inset',
          'focus-visible:outline-accent focus-visible:outline-2 focus-visible:outline-offset-2',
          'disabled:pointer-events-none disabled:opacity-60',
          isRead
            ? 'bg-linear-to-b from-emerald-500/16 to-emerald-500/8 text-emerald-700 ring-emerald-500/30 dark:text-emerald-400'
            : 'ring-border hover:bg-surface-overlay hover:ring-border-strong hover:text-foreground',
        )}
      >
        <span aria-hidden="true" className="font-mono">
          {isRead ? '\u2713' : '\u25cb'}
        </span>
        {isSaving ? t('workspace.readStateSaving') : stateLabel}
      </button>

      {/* Discoverability: a shortcut nobody is told about is a shortcut nobody uses. */}
      <span className="ml-auto hidden lg:inline">
        <kbd className="border-border bg-surface-overlay text-foreground rounded border px-1.5 py-px font-mono">
          [
        </kbd>
        {' / '}
        <kbd className="border-border bg-surface-overlay text-foreground rounded border px-1.5 py-px font-mono">
          ]
        </kbd>{' '}
        {t('workspace.shortcutHint')}
      </span>
    </footer>
  );
}

/**
 * One labelled reading in the status strip.
 *
 * There used to be a `pending` flag here, left over from the removed viewer
 * telemetry: it dimmed the value and explained that the viewer would report it
 * once a study loaded. No caller ever set it, so the branch was unreachable and
 * the message it showed could never appear on screen. Removed rather than
 * translated, along with its message key.
 */
function StatusField({
  label,
  value,
  mono = false,
  truncate = false,
}: {
  readonly label: string;
  readonly value: string;
  readonly mono?: boolean;
  readonly truncate?: boolean;
}) {
  return (
    <div className={cn('flex items-baseline gap-2', truncate && 'min-w-0')}>
      <span className="tracking-[0.06em] uppercase">{label}</span>
      <span
        className={cn('text-foreground font-medium', mono && 'font-mono', truncate && 'truncate')}
      >
        {value}
      </span>
    </div>
  );
}
