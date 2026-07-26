import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * Merges Tailwind class names, resolving conflicting utilities in favour of the last one.
 *
 * Lets a caller override a component's styling without the component exposing an
 * escape hatch for every property.
 */
export function cn(...values: ClassValue[]): string {
  return twMerge(clsx(values));
}
