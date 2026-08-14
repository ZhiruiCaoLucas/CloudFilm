import Link from 'next/link';
import { EmptyState } from '@cloudfilm/ui';
import { recentPatients } from '@/lib/api';
import { serverTranslations } from '@/lib/locale';

/**
 * The patients this clinician opened most recently.
 *
 * Server rendered from the access audit trail, scoped by the API to the
 * signed-in user. Nothing is kept in the browser: a shared workstation is the
 * normal case in a hospital, and browser-held history would show one
 * clinician's patients to whoever sits down next.
 *
 * Only the medical record number is shown. It is the least identifying thing
 * that still gets the reader back to the patient, and the panel is a shortcut
 * rather than a record - a name and date of birth on the landing page would be
 * PHI on display to anyone walking past.
 */
export async function RecentSearches() {
  const [patients, t] = await Promise.all([recentPatients(), serverTranslations()]);

  return (
    <section aria-labelledby="recent-searches-heading" className="flex flex-col gap-4">
      <h2 id="recent-searches-heading" className="text-foreground text-sm font-medium">
        {t('home.recentTitle')}
      </h2>

      {patients.length === 0 ? (
        <EmptyState title={t('home.recentEmptyTitle')} description={t('home.recentEmptyBody')} />
      ) : (
        <ul className="flex flex-wrap gap-2">
          {patients.map((patient) => (
            <li key={patient.medicalRecordNumber}>
              <Link
                href={`/patient/${encodeURIComponent(patient.medicalRecordNumber)}`}
                className="border-border bg-surface-raised text-foreground hover:border-accent hover:text-accent focus-visible:outline-accent inline-flex rounded-md border px-3 py-2 font-mono text-sm focus-visible:outline-2 focus-visible:outline-offset-2"
              >
                {patient.medicalRecordNumber}
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
