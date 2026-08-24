import { createElement } from 'react';
import { cn } from './cn';

/**
 * Atrium's decorative kit.
 *
 * Four marks, all of them inert. Every one is `aria-hidden`, none is in the tab
 * order, none carries information, and none may be placed inside or behind the
 * imaging viewport - they belong to chrome: headers, hero bands, empty states.
 *
 * All three are static SVG or a CSS gradient. Nothing animates, nothing listens
 * for the cursor, and nothing runs on a frame budget the reading area is also
 * spending.
 */

/** Properties accepted by {@link EcgHairline}. */
export interface EcgHairlineProps {
  readonly width?: number;
  readonly height?: number;
  readonly className?: string;
}

/**
 * A flat line with one heartbeat blip.
 *
 * Used once under a headline or a hero copy line, in the pale sky step. It is a
 * rule that happens to be alive, not an illustration.
 */
export function EcgHairline({ width = 300, height = 22, className }: EcgHairlineProps) {
  return createElement(
    'svg',
    {
      width,
      height,
      viewBox: '0 0 300 22',
      fill: 'none',
      'aria-hidden': true,
      focusable: false,
      className: cn('text-[var(--atrium-sky-300)]', className),
    },
    createElement('path', {
      key: 'trace',
      d: 'M0 11h108l6-9 6 18 6-14 6 5h168',
      stroke: 'currentColor',
      strokeWidth: 1.4,
      strokeLinejoin: 'round',
    }),
  );
}

/** Properties accepted by {@link DotField}. */
export interface DotFieldProps {
  readonly className?: string;
}

/**
 * The masked dot field.
 *
 * A 22px grid of one-pixel dots faded out with a radial mask, so it reads as a
 * calibration field rather than as a pattern. Absolutely positioned by the
 * caller; it fills whatever it is given.
 */
export function DotField({ className }: DotFieldProps) {
  return createElement('div', {
    'aria-hidden': true,
    className: cn('atrium-dots pointer-events-none absolute inset-0', className),
  });
}

/** Properties accepted by {@link CrossOutline}. */
export interface CrossOutlineProps {
  readonly size?: number;
  readonly className?: string;
}

/**
 * The oversized medical-cross outline.
 *
 * Stroke only, very pale, bleeding off a corner. Once per screen at most, and
 * never overlapping something the reader has to click.
 */
export function CrossOutline({ size = 180, className }: CrossOutlineProps) {
  return createElement(
    'svg',
    {
      width: size,
      height: size,
      viewBox: '0 0 240 240',
      fill: 'none',
      'aria-hidden': true,
      focusable: false,
      className: cn('pointer-events-none text-[#c8dcf8]', className),
    },
    createElement('path', {
      key: 'cross',
      d: 'M96 8h48v88h88v48h-88v88H96v-88H8V96h88Z',
      stroke: 'currentColor',
      strokeWidth: 2,
    }),
  );
}
