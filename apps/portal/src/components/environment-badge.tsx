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
        // The same construction as StatusBadge, because it is one: a dot that
        // repeats the state in a second channel, then a mono uppercase label.
        'inline-flex items-center gap-2 rounded-md px-2.5 py-[5px]',
        'transition-colors duration-[180ms]',
        'font-mono text-[10.5px] font-medium tracking-[0.1em] uppercase',
        isProduction
          ? 'bg-[var(--atrium-neutral-badge-bg)] text-[var(--atrium-neutral-badge)] shadow-[inset_0_0_0_1px_#52627d29]'
          : 'bg-[var(--atrium-warning-bg)] text-[var(--atrium-warning)] shadow-[inset_0_0_0_1px_#8a52003d]',
      )}
    >
      <span aria-hidden="true" className="h-1.5 w-1.5 shrink-0 rounded-full bg-current" />
      {label}
    </span>
  );
}
