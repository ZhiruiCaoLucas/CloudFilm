import { createElement, type ButtonHTMLAttributes, type ReactNode } from 'react';
import { cn } from './cn';

/** Visual weight of a button. */
export type ButtonVariant = 'primary' | 'secondary' | 'ghost';

/**
 * Control height.
 *
 * Atrium sizes a control by the weight of the decision it commits to: `sm` for
 * table-row and toolbar actions, `md` for everything ordinary, `lg` for the
 * sign-in submit, `xl` for the home page's single search action.
 */
export type ButtonSize = 'sm' | 'md' | 'lg' | 'xl';

/** Properties accepted by {@link Button}. */
export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  readonly variant?: ButtonVariant;
  readonly size?: ButtonSize;
  readonly children?: ReactNode;
}

const VARIANT_CLASSES: Record<ButtonVariant, string> = {
  primary: 'cf-control-primary text-white',
  secondary: 'cf-control-secondary text-atrium-ink hover:text-atrium-primary',
  ghost: 'cf-control-ghost text-atrium-muted',
};

const SIZE_CLASSES: Record<ButtonSize, string> = {
  sm: 'min-h-9 gap-1.5 px-3 text-[12.5px]',
  md: 'min-h-11 gap-2 px-[18px] text-[14.5px]',
  lg: 'min-h-[52px] gap-2 px-6 text-[15px]',
  xl: 'min-h-16 gap-2.5 px-7 text-[15.5px]',
};

/**
 * Button styling, usable on any element.
 *
 * Exported because navigation must be an anchor, not a button: a control that
 * changes the URL has to be middle-clickable, openable in a new tab, and
 * announced as a link. Sharing the classes keeps those anchors visually
 * identical to real buttons without copying utility strings into app code.
 *
 * The surfaces themselves are `cf-control-*` rules in the portal's stylesheet
 * rather than utility strings here. A gradient, a border, an inset highlight and
 * a hover lift are four coordinated declarations, not four independent ones, and
 * keeping them in one rule is what stops a button drifting out of agreement with
 * the field beside it.
 */
export function buttonClasses(
  variant: ButtonVariant = 'primary',
  className?: string,
  size: ButtonSize = 'md',
): string {
  return cn(
    'inline-flex items-center justify-center rounded-lg font-semibold whitespace-nowrap',
    'focus-visible:outline-atrium-primary focus-visible:outline-2 focus-visible:outline-offset-2',
    'disabled:pointer-events-none',
    // Carries the 180ms motion: the lift, the shadow escalation and - on the
    // primary only - the brightness step. Removed under prefers-reduced-motion.
    'cf-control',
    SIZE_CLASSES[size],
    VARIANT_CLASSES[variant],
    className,
  );
}

/**
 * The CloudFilm button.
 *
 * Focus is always visible, and the default height stays a comfortable ward-tablet
 * target while the smaller step exists for the dense table and toolbar rows the
 * workstation is full of.
 */
export function Button({
  variant = 'primary',
  size = 'md',
  className,
  type,
  ...rest
}: ButtonProps) {
  return createElement('button', {
    ...rest,
    type: type ?? 'button',
    className: buttonClasses(variant, className, size),
  });
}
