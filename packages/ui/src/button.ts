import { createElement, type ButtonHTMLAttributes, type ReactNode } from 'react';
import { cn } from './cn';

/** Visual weight of a button. */
export type ButtonVariant = 'primary' | 'secondary' | 'ghost';

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
 * Button styling, usable on any element.
 *
 * Exported because navigation must be an anchor, not a button: a control that
 * changes the URL has to be middle-clickable, openable in a new tab, and
 * announced as a link. Sharing the classes keeps those anchors visually
 * identical to real buttons without copying utility strings into app code.
 */
export function buttonClasses(variant: ButtonVariant = 'primary', className?: string): string {
  return cn(
    'inline-flex min-h-11 items-center justify-center gap-2 rounded-md px-4 text-sm font-medium',
    'transition-colors focus-visible:outline-2 focus-visible:outline-offset-2',
    'disabled:pointer-events-none disabled:opacity-50',
    VARIANT_CLASSES[variant],
    className,
  );
}

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
    className: buttonClasses(variant, className),
  });
}
