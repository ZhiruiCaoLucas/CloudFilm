import { createElement, type InputHTMLAttributes } from 'react';
import { cn } from './cn';

/**
 * Field height.
 *
 * `md` is every ordinary field. `lg` is the home page's single identifier field,
 * which is the only input on the screen and is sized to say so.
 */
export type InputSize = 'md' | 'lg';

/** Properties accepted by {@link Input}. */
export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  /** Marks the field as failing validation. Sets `aria-invalid` as well as the styling. */
  readonly invalid?: boolean;

  /**
   * Which height step to use.
   *
   * Named `inputSize` rather than `size` deliberately: `size` is a real HTML
   * attribute on `<input>`, and shadowing it would take a working attribute away
   * from every caller to gain a styling prop.
   */
  readonly inputSize?: InputSize;
}

const SIZE_CLASSES: Record<InputSize, string> = {
  md: 'min-h-11 rounded-[10px] px-3.5 text-[14px]',
  lg: 'min-h-16 rounded-xl px-5 text-[15.5px]',
};

/**
 * The CloudFilm text input.
 *
 * Invalid state is conveyed by border colour *and* `aria-invalid`, never by
 * colour alone — the error text that explains it is wired up by the caller
 * through `aria-describedby`.
 *
 * The surface is the `cf-field` rule in the portal's stylesheet: a recessed
 * counterpart to `cf-control`, with an inset hairline at rest that trades itself
 * for the focus ring rather than stacking with it.
 */
export function Input({ className, invalid = false, inputSize = 'md', type, ...rest }: InputProps) {
  return createElement('input', {
    ...rest,
    type: type ?? 'text',
    'aria-invalid': invalid || undefined,
    className: cn(
      'text-atrium-ink placeholder:text-atrium-muted/70 w-full',
      SIZE_CLASSES[inputSize],
      'cf-field',
      invalid ? 'cf-field-invalid' : '',
      'disabled:cursor-not-allowed disabled:opacity-45',
      className,
    ),
  });
}
