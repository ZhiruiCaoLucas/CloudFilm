import Link from 'next/link';
import { formatModality, type Patient, type Study } from '@cloudfilm/shared';
import { CloudFilmLogo } from '@/components/cloudfilm-logo';
import { serverTranslations } from '@/lib/locale';

/**
 * Workstation title bar.
 *
 * Patient identity and study identity stay on screen for the whole reading
 * session. A radiologist reporting on the wrong study is the failure mode this
 * bar exists to prevent, so it never scrolls away.
 */
export async function WorkspaceHeader({
  patient,
  study,
}: {
  readonly patient: Patient;
  readonly study: Study;
}) {
  const t = await serverTranslations();

  return (
    <header className="border-border bg-surface-raised flex shrink-0 items-center gap-6 border-b px-4 py-2.5">
      <Link
        href="/"
        className="focus-visible:outline-accent flex shrink-0 items-center gap-2 rounded-md focus-visible:outline-2 focus-visible:outline-offset-4"
      >
        <CloudFilmLogo className="h-6 w-6" />
        <span className="text-foreground text-sm font-semibold tracking-tight">
          {t('common.appName')}
        </span>
      </Link>

      <div className="border-border h-6 w-px shrink-0 border-l" />

      <dl className="flex min-w-0 flex-1 flex-wrap items-baseline gap-x-6 gap-y-1 text-sm">
        <HeaderField label={t('workspace.patient')}>
          <Link
            href={`/patient/${encodeURIComponent(patient.medicalRecordNumber)}`}
            className="hover:text-accent focus-visible:outline-accent rounded-sm focus-visible:outline-2 focus-visible:outline-offset-2"
          >
            {patient.fullName}
          </Link>
        </HeaderField>
        <HeaderField label={t('workspace.mrn')} mono>
          {patient.medicalRecordNumber}
        </HeaderField>
        <HeaderField label={t('workspace.study')}>
          {formatModality(study.modality)} · {study.description}
        </HeaderField>
        <HeaderField label={t('workspace.date')} mono>
          {study.performedOn}
        </HeaderField>
      </dl>
    </header>
  );
}

function HeaderField({
  label,
  mono = false,
  children,
}: {
  readonly label: string;
  readonly mono?: boolean;
  readonly children: React.ReactNode;
}) {
  return (
    <div className="flex min-w-0 items-baseline gap-2">
      <dt className="text-muted shrink-0 text-xs tracking-wide uppercase">{label}</dt>
      <dd className={`text-foreground truncate font-medium ${mono ? 'font-mono text-xs' : ''}`}>
        {children}
      </dd>
    </div>
  );
}
