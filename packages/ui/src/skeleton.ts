import { createElement } from 'react';
import { cn } from './cn';

/** Properties accepted by {@link Skeleton}. */
export interface SkeletonProps {
  readonly className?: string;
}

/**
 * Placeholder occupying the space real content will fill.
 *
 * Deliberately static. A pulsing placeholder is motion in the reading area's
 * peripheral vision, and the portal's own rule is no unnecessary animation;
 * a clinician waiting for a study does not need something blinking at them.
 */
export function Skeleton({ className }: SkeletonProps) {
  return createElement('div', {
    'aria-hidden': true,
    // Atrium's skeleton step: one stop darker than the panel surface, so a
    // placeholder reads as pending rather than as an empty row.
    className: cn('rounded-lg bg-[#e8eff9] dark:bg-[#1b2330]', className),
  });
}
