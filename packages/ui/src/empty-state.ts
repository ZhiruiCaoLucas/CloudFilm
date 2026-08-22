import { createElement, type ReactNode } from 'react';
import { cn } from './cn';

/** Properties accepted by {@link EmptyState}. */
export interface EmptyStateProps {
  /** Short statement of what is absent. */
  readonly title: string;
  /** One line explaining how the area gets populated. */
  readonly description?: string;
  /** Optional action or supplementary content. */
  readonly children?: ReactNode;
  readonly className?: string;
}

/**
 * Placeholder for a region that has nothing to show yet.
 *
 * An empty region should say why it is empty. A clinician who sees a blank
 * panel cannot tell the difference between "nothing here" and "failed to load",
 * and in a clinical system that ambiguity matters.
 */
export function EmptyState({ title, description, children, className }: EmptyStateProps) {
  return createElement(
    'div',
    {
      className: cn(
        // A solid hairline, not a dashed one. Dashes read as a drop target or an
        // unfinished region; this area is neither, it simply has nothing in it
        // yet, and it says so in words.
        'border-border bg-surface-raised flex flex-col items-center gap-1.5 rounded-lg border px-6 py-12 text-center',
        className,
      ),
    },
    createElement('p', { key: 'title', className: 'text-foreground text-sm font-medium' }, title),
    description !== undefined
      ? createElement(
          'p',
          { key: 'description', className: 'text-muted max-w-sm text-sm' },
          description,
        )
      : null,
    children,
  );
}
