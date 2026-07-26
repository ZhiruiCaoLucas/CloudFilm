import { createElement, type HTMLAttributes, type ReactNode } from 'react';
import { cn } from './cn';

/** Properties accepted by {@link Card} and its sections. */
export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  readonly children?: ReactNode;
}

/** A bordered surface used to group related information. */
export function Card({ className, ...rest }: CardProps) {
  return createElement('div', {
    ...rest,
    className: cn('rounded-lg border border-border bg-surface-raised shadow-sm', className),
  });
}

/** The heading area of a {@link Card}. */
export function CardHeader({ className, ...rest }: CardProps) {
  return createElement('div', {
    ...rest,
    className: cn('border-b border-border px-5 py-4', className),
  });
}

/** The content area of a {@link Card}. */
export function CardBody({ className, ...rest }: CardProps) {
  return createElement('div', {
    ...rest,
    className: cn('px-5 py-4', className),
  });
}
