import { EmptyState } from '@cloudfilm/ui';
import { serverTranslations } from '@/lib/locale';

/**
 * Recently opened patients.
 *
 * Empty by design in Sprint 1. Recent searches are patient-identifying, so they
 * cannot be kept until there is an authenticated user to scope them to and an
 * audit trail to record the access — storing them in the browser first would
 * leak one clinician's patients to the next person at the workstation.
 */
export async function RecentSearches() {
  const t = await serverTranslations();

  return (
    <section aria-labelledby="recent-searches-heading" className="flex flex-col gap-4">
      <h2 id="recent-searches-heading" className="text-foreground text-sm font-medium">
        {t('home.recentTitle')}
      </h2>
      <EmptyState title={t('home.recentEmptyTitle')} description={t('home.recentEmptyBody')} />
    </section>
  );
}
