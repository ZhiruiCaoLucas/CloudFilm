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
    <header className="border-border bg-surface-raised flex h-12 shrink-0 items-center gap-5 border-b px-4 shadow-[inset_0_-1px_0_0_var(--glass-edge)]">
      <Link
        href="/"
        className="focus-visible:outline-accent flex shrink-0 items-center gap-2 rounded-md focus-visible:outline-2 focus-visible:outline-offset-4"
      >
        <CloudFilmLogo className="h-[1.375rem] w-[1.375rem]" />
        <span className="text-foreground text-[0.8125rem] font-semibold tracking-tight">
          {t('common.appName')}
        </span>
      </Link>

      <div className="bg-border h-5 w-px shrink-0" />

      <dl className="flex min-w-0 flex-1 flex-wrap items-baseline gap-x-7 gap-y-0.5 text-[0.8125rem]">
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
      {/*
        Labels are set small, wide and grey; values are set at reading size in
        the foreground colour. The contrast between the two is what lets a
        radiologist find the patient name in this bar without reading the bar.
      */}
      <dt className="text-muted shrink-0 text-[0.625rem] tracking-[0.08em] uppercase">{label}</dt>
      <dd className={`text-foreground truncate font-medium ${mono ? 'font-mono text-xs' : ''}`}>
        {children}
      </dd>
    </div>
  );
}
