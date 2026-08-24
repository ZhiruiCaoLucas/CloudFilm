import { createElement, type ReactNode } from 'react';
import { CrossOutline } from './atmosphere';
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
 * An empty region should say why it is empty. A clinician who sees a blank panel
 * cannot tell the difference between "nothing here" and "failed to load", and in
 * a clinical system that ambiguity matters.
 *
 * Atrium gives it a rounded panel, a wash of blue falling from the top edge and
 * one oversized cross outline bleeding out of the corner. Both marks are inert
 * and hidden from assistive technology; the words carry the meaning.
 */
export function EmptyState({ title, description, children, className }: EmptyStateProps) {
  return createElement(
    'div',
    {
      className: cn(
        'border-atrium-border bg-atrium-white relative overflow-hidden rounded-xl border',
        'flex flex-col items-center justify-center gap-2 px-6 py-[56px] text-center',
        className,
      ),
    },
    createElement('div', {
      key: 'wash',
      'aria-hidden': true,
      className: 'atrium-radial-top pointer-events-none absolute inset-0',
    }),
    createElement(CrossOutline, {
      key: 'cross',
      size: 200,
      className: 'absolute -top-14 right-[-46px] opacity-70',
    }),
    createElement('p', { key: 'title', className: 'atrium-h3 text-atrium-ink relative' }, title),
    description !== undefined
      ? createElement(
          'p',
          {
            key: 'description',
            className: 'atrium-body text-atrium-body relative max-w-sm',
          },
          description,
        )
      : null,
    children === undefined
      ? null
      : createElement('div', { key: 'children', className: 'relative mt-2' }, children),
  );
}
