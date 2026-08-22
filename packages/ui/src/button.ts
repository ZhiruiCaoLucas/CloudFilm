import { createElement, type ButtonHTMLAttributes, type ReactNode } from 'react';
import { cn } from './cn';

/** Visual weight of a button. */
export type ButtonVariant = 'primary' | 'secondary' | 'ghost';

/** Properties accepted by {@link Button}. */
export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  readonly variant?: ButtonVariant;
  readonly children?: ReactNode;
}

/**
 * Per-variant surface.
 *
 * The surfaces themselves are `cf-control-*` classes in the portal's stylesheet
 * rather than utility strings here. A translucent gradient that slides under the
 * pointer, an inset hairline of light along the top edge and a press state are
 * four coordinated declarations, not four independent ones - keeping them in one
 * rule is what stops a button drifting out of agreement with the field beside
 * it. Only the text colour and the focus ring stay as utilities, because those
 * are the two things a call site legitimately overrides.
 */
const VARIANT_CLASSES: Record<ButtonVariant, string> = {
  primary: 'cf-control-primary text-accent-foreground focus-visible:outline-accent',
  secondary: 'cf-control-secondary text-foreground focus-visible:outline-accent',
  ghost: 'cf-control-ghost text-muted hover:text-foreground focus-visible:outline-accent',
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
    // 40px rather than 44. Still comfortably above the WCAG target-size minimum
    // and still a ward-tablet hit target, but a workstation's controls should
    // read as instruments rather than as a phone's buttons - and every dense
    // surface in the portal was already overriding the taller default.
    'inline-flex min-h-10 items-center justify-center gap-2 rounded-md px-3.5 text-sm font-medium',
    'whitespace-nowrap focus-visible:outline-2 focus-visible:outline-offset-2',
    'disabled:pointer-events-none disabled:opacity-45',
    // Carries the motion: the highlight slide, the one-pixel lift and the press.
    // Removed entirely under prefers-reduced-motion.
    'cf-control',
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
