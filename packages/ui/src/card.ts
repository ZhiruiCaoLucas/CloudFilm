import { createElement, type HTMLAttributes, type ReactNode } from 'react';
import { cn } from './cn';

/** Properties accepted by {@link Card} and its sections. */
export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  readonly children?: ReactNode;
}

/**
 * A bordered surface used to group related information.
 *
 * A hairline and a change of surface, and nothing else. The drop shadow this
 * used to carry made every grouping on a page look like a floating tile; a
 * clinical layout gets its structure from alignment and rules, and reserves
 * elevation for things that genuinely sit above the page.
 */
export function Card({ className, ...rest }: CardProps) {
  return createElement('div', {
    ...rest,
    className: cn(
      'border-border bg-surface-raised rounded-lg border',
      // Brightens its own edge under the pointer and does nothing else. A panel
      // that is read rather than pressed should acknowledge the cursor without
      // moving out from under it.
      'cf-lift',
      className,
    ),
  });
}

/** The heading area of a {@link Card}. */
export function CardHeader({ className, ...rest }: CardProps) {
  return createElement('div', {
    ...rest,
    className: cn('border-border border-b px-5 py-3.5', className),
  });
}

/** The content area of a {@link Card}. */
export function CardBody({ className, ...rest }: CardProps) {
  return createElement('div', {
    ...rest,
    className: cn('px-5 py-4', className),
  });
}
