import Link from 'next/link';
import {
  formatModality,
  STUDY_STATUS_MESSAGE_KEYS,
  type Patient,
  type Study,
  type Translator,
} from '@cloudfilm/shared';
import { cn } from '@cloudfilm/ui';
import { StudyThumbnail } from './study-thumbnail';
import { serverTranslations } from '@/lib/locale';

/**
 * Left rail: who the patient is, and every study available for them.
 *
 * Each study is an anchor rather than a click handler. Selecting a study is a
 * change of address, so it must be linkable, middle-clickable, and reachable by
 * Tab — all of which come free from `<a>` and none of which come free from a
 * `div` with an `onClick`.
 */
export async function StudyNavigator({
  patient,
  studies,
  selectedStudyInstanceUid,
}: {
  readonly patient: Patient;
  readonly studies: readonly Study[];
  readonly selectedStudyInstanceUid: string;
}) {
  const t = await serverTranslations();

  return (
    <aside
      aria-label={t('workspace.navigatorLabel')}
      className="border-border bg-surface-raised flex w-[17.5rem] shrink-0 flex-col overflow-y-auto border-r"
    >
      <section className="border-border shrink-0 border-b px-4 py-4">
        <h2 className="text-muted mb-2 text-[0.625rem] tracking-[0.08em] uppercase">
          {t('workspace.patient')}
        </h2>
        <p className="text-foreground text-[0.9375rem] font-medium tracking-tight">
          {patient.fullName}
        </p>
        {/* The same two-column grid the metadata rail uses, so identity here and
            metadata there are read off one alignment. */}
        <dl className="text-muted mt-2.5 grid grid-cols-[3.5rem_minmax(0,1fr)] gap-x-3 text-[0.6875rem] leading-5">
          <dt>{t('workspace.mrn')}</dt>
          <dd className="text-foreground truncate font-mono">{patient.medicalRecordNumber}</dd>
          <dt>{t('workspace.born')}</dt>
          <dd className="text-foreground font-mono">{patient.birthDate}</dd>
          <dt>{t('workspace.gender')}</dt>
          <dd className="text-foreground">{t(`gender.${patient.gender}`)}</dd>
        </dl>
      </section>

      <nav aria-label={t('workspace.studiesLabel')} className="flex-1 px-2 py-3">
        <h2 className="text-muted px-2 pb-2.5 text-[0.625rem] tracking-[0.08em] uppercase">
          {t('workspace.studiesCount', { count: studies.length })}
        </h2>
        <ul className="flex flex-col gap-1">
          {studies.map((study) => (
            <li key={study.studyInstanceUid}>
              <StudyNavigatorItem
                study={study}
                isSelected={study.studyInstanceUid === selectedStudyInstanceUid}
                t={t}
              />
            </li>
          ))}
        </ul>
      </nav>
    </aside>
  );
}

function StudyNavigatorItem({
  study,
  isSelected,
  t,
}: {
  readonly study: Study;
  readonly isSelected: boolean;
  readonly t: Translator;
}) {
  return (
    <Link
      href={`/workspace/${encodeURIComponent(study.studyInstanceUid)}`}
      aria-current={isSelected ? 'page' : undefined}
      className={cn(
        'focus-visible:outline-accent flex w-full items-start gap-3 rounded-md py-2 pr-2 pl-2 text-left',
        'border-l-2 transition-[background-color,border-color] duration-200',
        'focus-visible:outline-2 focus-visible:-outline-offset-2',
        // A rule in the accent colour rather than a tinted, ringed tile. The
        // selected study is one item in a short list the reader is stepping
        // through with the keyboard, and it needs to be unmistakable without
        // becoming the brightest object beside the image.
        isSelected
          ? 'border-accent bg-accent-soft shadow-[inset_0_1px_0_0_var(--glass-edge)]'
          : 'hover:bg-surface-overlay/70 border-transparent',
      )}
    >
      <StudyThumbnail modality={study.modality} />

      <span className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="text-foreground truncate text-[0.8125rem] font-medium">
          {formatModality(study.modality)}
        </span>
        <span className="text-muted truncate text-[0.75rem]">{study.description}</span>
        <span className="text-muted flex items-center gap-1.5 font-mono text-[0.6875rem]">
          <span>{study.performedOn}</span>
          <span aria-hidden="true">·</span>
          <span>{t(STUDY_STATUS_MESSAGE_KEYS[study.status])}</span>
        </span>
      </span>
    </Link>
  );
}
