import { createElement, type ReactNode } from 'react';
import { cn } from './cn';

/** Semantic meaning of a {@link StatusBadge}. */
export type StatusTone = 'healthy' | 'degraded' | 'unhealthy' | 'unknown';

/** Properties accepted by {@link StatusBadge}. */
export interface StatusBadgeProps {
  readonly tone: StatusTone;
  readonly children: ReactNode;
  readonly className?: string;
}

/**
 * Per-tone surface.
 *
 * A tint, a ring and a text colour, exactly as before. What is new is the
 * gradient: the tint is slightly stronger at the top edge than the bottom, which
 * is enough to make the token read as a small physical chip beside the
 * fixed-width values it sits with - and not enough to be seen as colour.
 */
const TONE_CLASSES: Record<StatusTone, string> = {
  healthy:
    'bg-linear-to-b from-emerald-500/15 to-emerald-500/8 text-emerald-700 ring-emerald-500/30 dark:text-emerald-400',
  degraded:
    'bg-linear-to-b from-amber-500/15 to-amber-500/8 text-amber-700 ring-amber-500/30 dark:text-amber-400',
  unhealthy:
    'bg-linear-to-b from-red-500/15 to-red-500/8 text-red-700 ring-red-500/30 dark:text-red-400',
  unknown:
    'bg-linear-to-b from-slate-500/12 to-slate-500/6 text-slate-600 ring-slate-500/25 dark:text-slate-400',
};

/**
 * Reports service state.
 *
 * Colour is paired with a text label so the badge stays readable for colour vision
 * deficient users and in monochrome print.
 */
export function StatusBadge({ tone, children, className }: StatusBadgeProps) {
  return createElement(
    'span',
    {
      className: cn(
        // Squared rather than a pill. A status token is a reading, and it sits
        // beside identifiers and counts set in a fixed-width face; a fully
        // rounded capsule reads as a notification instead.
        'inline-flex items-center rounded px-2 py-0.5 text-[0.6875rem] font-medium ring-1 ring-inset',
        TONE_CLASSES[tone],
        className,
      ),
    },
    children,
  );
}
