import { createElement, type ButtonHTMLAttributes, type ReactNode } from 'react';
import { cn } from './cn';

/** Visual weight of a {@link Button}. */
type ButtonVariant = 'primary' | 'secondary' | 'ghost';

/** Properties accepted by {@link Button}. */
export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  readonly variant?: ButtonVariant;
  readonly children?: ReactNode;
}

const VARIANT_CLASSES: Record<ButtonVariant, string> = {
  primary: 'bg-accent text-accent-foreground hover:bg-accent/90 focus-visible:outline-accent',
  secondary:
    'bg-surface-raised text-foreground ring-1 ring-inset ring-border hover:bg-surface-overlay focus-visible:outline-accent',
  ghost: 'text-muted hover:bg-surface-raised hover:text-foreground focus-visible:outline-accent',
};

/**
 * The CloudFilm button.
 *
 * Focus is always visible and hit targets meet the 44px recommendation, because
 * the portal is used on ward tablets as often as on a desktop.
 */
export function Button({ variant = 'primary', className, type, ...rest }: ButtonProps) {
  return createElement('button', {
    ...rest,
    type: type ?? 'button',
    className: cn(
      'inline-flex min-h-11 items-center justify-center gap-2 rounded-md px-4 text-sm font-medium',
      'transition-colors focus-visible:outline-2 focus-visible:outline-offset-2',
      'disabled:pointer-events-none disabled:opacity-50',
      VARIANT_CLASSES[variant],
      className,
    ),
  });
}
