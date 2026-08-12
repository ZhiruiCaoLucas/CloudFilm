import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { PatientSummary } from '@/components/patient-summary';
import { StudyTable } from '@/components/study-table';
import { currentUser, serverApiClient } from '@/lib/api';
import { PortalShell } from '@/components/portal-shell';
import { serverTranslations } from '@/lib/locale';

interface PatientPageProps {
  readonly params: Promise<{ readonly patientId: string }>;
}

export async function generateMetadata({ params }: PatientPageProps): Promise<Metadata> {
  const { patientId } = await params;

  return { title: `Patient ${decodeURIComponent(patientId)} · CloudFilm` };
}

/**
 * A patient and their imaging studies.
 *
 * Reads from the fixture set rather than the API. The page depends only on the
 * `PatientRecord` shape, so replacing the lookup with an API call in Sprint 2
 * touches this one line and nothing below it.
 */
export default async function PatientPage({ params }: PatientPageProps) {
  const { patientId } = await params;
  const t = await serverTranslations();
  const client = await serverApiClient();
  const record = await client.getPatient(decodeURIComponent(patientId));
  const user = await currentUser();

  if (record === null) {
    notFound();
  }

  return (
    <PortalShell user={user}>
      <div className="flex flex-col gap-10">
        <nav aria-label={t('patient.breadcrumbLabel')}>
          <Link
            href="/"
            className="text-muted hover:text-foreground focus-visible:outline-accent rounded-md text-sm focus-visible:outline-2 focus-visible:outline-offset-4"
          >
            {t('patient.breadcrumb')}
          </Link>
        </nav>

        <PatientSummary patient={record.patient} />

        <section aria-labelledby="studies-heading" className="flex flex-col gap-4">
          <div className="flex items-baseline justify-between gap-4">
            <h2 id="studies-heading" className="text-foreground text-sm font-medium">
              {t('patient.studiesHeading')}
            </h2>
            <p className="text-muted text-xs">
              {t(
                record.studies.length === 1 ? 'patient.studyCountOne' : 'patient.studyCountOther',
                { count: record.studies.length },
              )}
            </p>
          </div>

          <StudyTable studies={record.studies} />
        </section>
      </div>
    </PortalShell>
  );
}
