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

const TONE_CLASSES: Record<StatusTone, string> = {
  healthy: 'bg-emerald-500/10 text-emerald-600 ring-emerald-500/30 dark:text-emerald-400',
  degraded: 'bg-amber-500/10 text-amber-600 ring-amber-500/30 dark:text-amber-400',
  unhealthy: 'bg-red-500/10 text-red-600 ring-red-500/30 dark:text-red-400',
  unknown: 'bg-slate-500/10 text-slate-600 ring-slate-500/30 dark:text-slate-400',
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
