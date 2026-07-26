import { cn } from '@cloudfilm/ui';

/**
 * Shows which deployment the user is looking at.
 *
 * Permanently visible in the top navigation on purpose: staff move between
 * training and live systems, and mistaking one for the other in a clinical
 * setting is a patient-safety problem, not a cosmetic one.
 */
export function EnvironmentBadge({ environment }: { readonly environment: string }) {
  const isProduction = environment === 'Production';

  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ring-1 ring-inset',
        isProduction
          ? 'bg-surface-overlay text-muted ring-border'
          : 'bg-amber-500/10 text-amber-700 ring-amber-500/30 dark:text-amber-400',
      )}
    >
      {environment}
    </span>
  );
}
