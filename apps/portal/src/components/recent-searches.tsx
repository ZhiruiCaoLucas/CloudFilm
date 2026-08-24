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
    <section aria-labelledby="recent-searches-heading" className="flex flex-col gap-[18px]">
      <div className="flex items-baseline justify-between gap-4">
        <h2 id="recent-searches-heading" className="atrium-eyebrow text-atrium-muted">
          {t('home.recentTitle')}
        </h2>
        {patients.length > 0 ? (
          <span className="text-atrium-muted font-mono text-[11px]">{patients.length}</span>
        ) : null}
      </div>

      {patients.length === 0 ? (
        <EmptyState title={t('home.recentEmptyTitle')} description={t('home.recentEmptyBody')} />
      ) : (
        <ul className="border-atrium-border bg-atrium-white divide-atrium-border divide-y overflow-hidden rounded-xl border">
          {patients.map((patient, index) => (
            <li key={patient.medicalRecordNumber}>
              <Link
                href={`/patient/${encodeURIComponent(patient.medicalRecordNumber)}`}
                className="atrium-row focus-visible:outline-atrium-primary group flex items-center gap-4 px-5 py-3.5 focus-visible:outline-2 focus-visible:-outline-offset-2"
              >
                {/* Position in the list, not an identifier. Zero-padded so the
                    column keeps one width and the identifiers beside it stay
                    aligned down the page. */}
                <span
                  aria-hidden="true"
                  className="border-atrium-border bg-atrium-panel text-atrium-muted flex h-7 w-7 shrink-0 items-center justify-center rounded-md border font-mono text-[10.5px]"
                >
                  {String(index + 1).padStart(2, '0')}
                </span>

                <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                  <span className="text-atrium-ink truncate font-mono text-[13.5px] font-medium">
                    {patient.medicalRecordNumber}
                  </span>
                  <span className="text-atrium-muted text-[11px]">
                    {t('patient.medicalRecordNumber')}
                  </span>
                </span>

                <span className="text-atrium-primary hidden items-center gap-1.5 text-[12px] font-semibold sm:inline-flex">
                  {t('study.open')}
                  <span aria-hidden="true" className="atrium-arrow inline-block">
                    →
                  </span>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}

      {/* The rule this list follows, stated where the list is rather than only
          in the panel beside it. */}
      {patients.length > 0 ? (
        <p className="text-atrium-muted text-[11.5px] leading-relaxed">
          {t('home.privacyPointStorage')}
        </p>
      ) : null}
    </section>
  );
}
