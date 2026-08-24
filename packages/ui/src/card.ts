import { createElement, type HTMLAttributes, type ReactNode } from 'react';
import { cn } from './cn';

/** Properties accepted by {@link CardHeader} and {@link CardBody}. */
export interface CardSectionProps extends HTMLAttributes<HTMLDivElement> {
  readonly children?: ReactNode;
}

/** Properties accepted by {@link Card}. */
export interface CardProps extends CardSectionProps {
  /**
   * Raises the card into an Atrium "console".
   *
   * Reserved for the surfaces that carry a search: the home identifier console
   * and the advanced-criteria card. They overlap the band above them, so they
   * need the elevation to read as sitting in front of it rather than beside it.
   */
  readonly elevated?: boolean;
}

/**
 * A bordered surface used to group related information.
 *
 * A hairline and a change of surface. Structure in Atrium comes from alignment
 * and rules; elevation is reserved for things that genuinely sit above the page,
 * which is what `elevated` is for.
 */
export function Card({ className, elevated = false, ...rest }: CardProps) {
  return createElement('div', {
    ...rest,
    className: cn(
      'border-atrium-border bg-atrium-white border',
      elevated ? 'atrium-console' : 'rounded-xl shadow-[var(--shadow-sm)]',
      // Brightens its own edge under the pointer and does nothing else. A panel
      // that is read rather than pressed should acknowledge the cursor without
      // moving out from under it.
      'atrium-lift',
      className,
    ),
  });
}

/** The heading area of a {@link Card}. */
export function CardHeader({ className, ...rest }: CardSectionProps) {
  return createElement('div', {
    ...rest,
    className: cn('border-atrium-border border-b px-[22px] py-4', className),
  });
}

/** The content area of a {@link Card}. */
export function CardBody({ className, ...rest }: CardSectionProps) {
  return createElement('div', {
    ...rest,
    className: cn('px-[22px] py-[18px]', className),
  });
}
