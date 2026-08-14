import type { Metadata } from 'next';
import Link from 'next/link';
import { PatientSearchForm } from '@/components/patient-search-form';
import { RecentSearches } from '@/components/recent-searches';
import { PortalShell } from '@/components/portal-shell';
import { currentUser } from '@/lib/api';
import { serverTranslations } from '@/lib/locale';

export async function generateMetadata(): Promise<Metadata> {
  const t = await serverTranslations();

  return { title: t('home.pageTitle') };
}

export default async function HomePage() {
  const [user, t] = await Promise.all([currentUser(), serverTranslations()]);

  return (
    <PortalShell user={user}>
      <div className="flex flex-col gap-16">
        <section className="flex flex-col items-center gap-8 pt-8 text-center sm:pt-16">
          <div className="flex flex-col gap-3">
            <h1 className="text-foreground text-3xl font-semibold tracking-tight sm:text-4xl">
              {t('home.title')}
            </h1>
            <p className="text-muted text-base">{t('home.subtitle')}</p>
          </div>

          <div className="flex w-full max-w-xl flex-col gap-3 text-left">
            <PatientSearchForm />

            {/*
              The identifier search stays the primary path - a clinician who has
              an MRN wants one keystroke, not a form. This is the way out for
              everyone who does not have one.
            */}
            <Link
              href="/search"
              className="text-muted hover:text-foreground focus-visible:outline-accent self-start rounded-md text-sm focus-visible:outline-2 focus-visible:outline-offset-4"
            >
              {t('home.advancedSearch')}
            </Link>
          </div>
        </section>

        <RecentSearches />
      </div>
    </PortalShell>
  );
}
