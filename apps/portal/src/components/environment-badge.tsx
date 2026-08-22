import { cn } from '@cloudfilm/ui';

/**
 * Shows which deployment the user is looking at.
 *
 * Permanently visible in the top navigation on purpose: staff move between
 * training and live systems, and mistaking one for the other in a clinical
 * setting is a patient-safety problem, not a cosmetic one.
 *
 * Takes the styling decision as a boolean and the text as a string, rather than
 * deriving both from one English word. Comparing `environment === 'Production'`
 * would have made the badge turn amber the moment the label was translated.
 */
export function EnvironmentBadge({
  isProduction,
  label,
}: {
  readonly isProduction: boolean;
  readonly label: string;
}) {
  return (
    <span
      className={cn(
        // Squared and small, matching StatusBadge. Both are readings rather
        // than notifications, and they appear beside one another.
        'inline-flex items-center rounded px-2 py-0.5 text-[0.6875rem] font-medium ring-1 ring-inset',
        isProduction
          ? 'bg-surface-overlay text-muted ring-border'
          : 'bg-amber-500/10 text-amber-700 ring-amber-500/30 dark:text-amber-400',
      )}
    >
      {label}
    </span>
  );
}
