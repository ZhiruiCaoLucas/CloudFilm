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
 * A tinted fill, a hairline of the same hue at 24%, and text in the full-strength
 * tone. The dot in front repeats the state in a second channel, so the badge
 * survives being read in greyscale or by somebody who cannot separate the hues.
 */
const TONE_CLASSES: Record<StatusTone, string> = {
  healthy:
    'bg-[var(--atrium-success-bg)] text-[var(--atrium-success)] shadow-[inset_0_0_0_1px_#0f7a523d]',
  degraded:
    'bg-[var(--atrium-warning-bg)] text-[var(--atrium-warning)] shadow-[inset_0_0_0_1px_#8a52003d]',
  unhealthy:
    'bg-[var(--atrium-danger-bg)] text-[var(--atrium-danger)] shadow-[inset_0_0_0_1px_#b423183d]',
  unknown:
    'bg-[var(--atrium-neutral-badge-bg)] text-[var(--atrium-neutral-badge)] shadow-[inset_0_0_0_1px_#52627d3d]',
};

/**
 * Reports service state.
 *
 * Colour is paired with a text label and a filled dot, so the badge stays
 * readable for colour vision deficient users and in monochrome print.
 */
export function StatusBadge({ tone, children, className }: StatusBadgeProps) {
  return createElement(
    'span',
    {
      className: cn(
        'inline-flex items-center gap-[7px] rounded-md px-[11px] py-[5px] text-[11.5px] font-semibold',
        TONE_CLASSES[tone],
        className,
      ),
    },
    createElement('span', {
      key: 'dot',
      'aria-hidden': true,
      className: 'h-[5px] w-[5px] shrink-0 rounded-full bg-current',
    }),
    children,
  );
}
