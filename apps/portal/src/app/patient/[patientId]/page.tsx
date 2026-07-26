import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { findPatientRecord } from '@cloudfilm/shared/mock';
import { PatientSummary } from '@/components/patient-summary';
import { SampleDataNotice } from '@/components/sample-data-notice';
import { StudyTable } from '@/components/study-table';
import { PortalShell } from '@/components/portal-shell';

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
  const record = findPatientRecord(decodeURIComponent(patientId));

  if (record === undefined) {
    notFound();
  }

  return (
    <PortalShell>
      <div className="flex flex-col gap-10">
        <nav aria-label="Breadcrumb">
          <Link
            href="/"
            className="text-muted hover:text-foreground focus-visible:outline-accent rounded-md text-sm focus-visible:outline-2 focus-visible:outline-offset-4"
          >
            ← Patient search
          </Link>
        </nav>

        <SampleDataNotice />

        <PatientSummary patient={record.patient} />

        <section aria-labelledby="studies-heading" className="flex flex-col gap-4">
          <div className="flex items-baseline justify-between gap-4">
            <h2 id="studies-heading" className="text-foreground text-sm font-medium">
              Studies
            </h2>
            <p className="text-muted text-xs">
              {record.studies.length} {record.studies.length === 1 ? 'study' : 'studies'}
            </p>
          </div>

          <StudyTable studies={record.studies} />
        </section>
      </div>
    </PortalShell>
  );
}
