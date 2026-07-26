import { EmptyState } from '@cloudfilm/ui';

/**
 * Recently opened patients.
 *
 * Empty by design in Sprint 1. Recent searches are patient-identifying, so they
 * cannot be kept until there is an authenticated user to scope them to and an
 * audit trail to record the access — storing them in the browser first would
 * leak one clinician's patients to the next person at the workstation.
 */
export function RecentSearches() {
  return (
    <section aria-labelledby="recent-searches-heading" className="flex flex-col gap-4">
      <h2 id="recent-searches-heading" className="text-foreground text-sm font-medium">
        Recent searches
      </h2>
      <EmptyState
        title="No recent searches"
        description="Patients you open will be listed here once sign-in and access auditing are in place."
      />
    </section>
  );
}
