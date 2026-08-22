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
      // Matched to the button height so a field and the control beside it sit on
      // one line without either being nudged.
      'min-h-10 w-full rounded-md px-3 text-sm',
      'text-foreground placeholder:text-muted/70',
      // The recessed counterpart to cf-control: an inset top highlight, and a
      // focus ring that opens softly instead of snapping on. Nothing moves - a
      // field that lifts under the caret has moved the text being read.
      'cf-field',
      invalid ? 'cf-field-invalid' : '',
      'disabled:cursor-not-allowed disabled:opacity-45',
      className,
    ),
  });
}
