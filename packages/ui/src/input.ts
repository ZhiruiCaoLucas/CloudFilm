import { createElement, type InputHTMLAttributes } from 'react';
import { cn } from './cn';

/** Properties accepted by {@link Input}. */
export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  /** Marks the field as failing validation. Sets `aria-invalid` as well as the styling. */
  readonly invalid?: boolean;
}

/**
 * The CloudFilm text input.
 *
 * Invalid state is conveyed by border colour *and* `aria-invalid`, never by
 * colour alone — the error text that explains it is wired up by the caller
 * through `aria-describedby`.
 */
export function Input({ className, invalid = false, type, ...rest }: InputProps) {
  return createElement('input', {
    ...rest,
    type: type ?? 'text',
    'aria-invalid': invalid || undefined,
    className: cn(
      'min-h-11 w-full rounded-md px-3 text-sm',
      'bg-surface-raised text-foreground placeholder:text-muted',
      'ring-1 ring-inset transition-colors',
      'focus-visible:ring-2 focus-visible:outline-none',
      invalid
        ? 'ring-red-500/60 focus-visible:ring-red-500'
        : 'ring-border focus-visible:ring-accent',
      'disabled:cursor-not-allowed disabled:opacity-50',
      className,
    ),
  });
}
