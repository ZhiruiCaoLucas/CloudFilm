'use client';

import { useState } from 'react';
import {
  formatModality,
  STUDY_STATUS_MESSAGE_KEYS,
  type Patient,
  type Series,
  type Study,
  type StudyAnalysis,
  type Translator,
} from '@cloudfilm/shared';
import { Button, cn } from '@cloudfilm/ui';
import { useTranslations } from '@/components/locale-provider';
import { AnalysisPanel } from './analysis-panel';

/**
 * Study and patient metadata, alongside the reading area.
 *
 * A workstation keeps identifying detail visible while an image is on screen:
 * confirming acquisition date, modality and series count against a report is
 * part of reading, not a separate lookup. Collapsible because on a small
 * display the pixels are better spent on the image.
 *
 * Hidden below the `lg` breakpoint entirely - the workspace is desktop-first,
 * and a 288px rail on a tablet leaves too little room for diagnostic viewing.
 */
export function StudyMetadataPanel({
  patient,
  study,
  series,
  analysis,
}: {
  readonly patient: Patient;
  readonly study: Study;
  readonly series: readonly Series[];

  /** The stored analysis of this study, or `null` if it has never been analysed. */
  readonly analysis: StudyAnalysis | null;
}) {
  const t = useTranslations();
  const [isOpen, setIsOpen] = useState(true);

  return (
    <aside
      aria-label={t('workspace.metadataLabel')}
      className="border-border bg-surface-raised hidden shrink-0 flex-col border-l lg:flex"
    >
      {/* The same 48px as the workstation title bar, so the two rules across the
          top of the screen are one continuous line. */}
      <div className="border-border flex h-12 shrink-0 items-center justify-between gap-2 border-b px-3">
        {isOpen ? (
          <h2 className="text-muted text-[0.625rem] tracking-[0.08em] uppercase">
            {t('workspace.metadata')}
          </h2>
        ) : null}
        <Button
          variant="ghost"
          onClick={() => setIsOpen((open) => !open)}
          aria-expanded={isOpen}
          className="min-h-7 px-2 text-[0.6875rem]"
        >
          {isOpen ? t('workspace.hide') : t('workspace.info')}
        </Button>
      </div>

      {isOpen ? (
        <div className="w-[19rem] overflow-y-auto px-4 py-4">
          <MetadataSection title={t('workspace.study')}>
            <MetadataRow label={t('study.description')} value={study.description} />
            <MetadataRow label={t('study.modality')} value={formatModality(study.modality)} />
            <MetadataRow label={t('study.date')} value={study.performedOn} mono />

            {/*
              Omitted rather than shown blank. A study with no accession number
              is ordinary, and an empty row invites the reader to wonder whether
              the value failed to load.
            */}
            {study.accessionNumber.length > 0 ? (
              <MetadataRow label={t('study.accession')} value={study.accessionNumber} mono />
            ) : null}

            <MetadataRow
              label={t('study.status')}
              value={t(STUDY_STATUS_MESSAGE_KEYS[study.status])}
            />
            <MetadataRow label={t('study.series')} value={String(study.seriesCount)} mono />
            <MetadataRow label={t('study.images')} value={String(study.instanceCount)} mono />
            <MetadataRow label={t('study.studyUid')} value={study.studyInstanceUid} mono wrap />
          </MetadataSection>

          {/*
            Patient -> Study -> Series -> Images -> Viewer. The section is
            omitted entirely when the archive reported no series, rather than
            rendered as an empty heading.
          */}
          {series.map((item) => (
            <MetadataSection key={item.seriesInstanceUid} title={seriesTitle(item, t)}>
              {item.modality.length > 0 ? (
                <MetadataRow label={t('series.modality')} value={formatModality(item.modality)} />
              ) : null}
              {item.description.length > 0 ? (
                <MetadataRow label={t('series.description')} value={item.description} />
              ) : null}
              {item.instanceCount > 0 ? (
                <MetadataRow label={t('series.images')} value={String(item.instanceCount)} mono />
              ) : null}
              {item.institutionName.length > 0 ? (
                <MetadataRow label={t('series.institution')} value={item.institutionName} />
              ) : null}
            </MetadataSection>
          ))}

          <MetadataSection title={t('workspace.patient')}>
            <MetadataRow label={t('patient.name')} value={patient.fullName} />
            <MetadataRow label={t('workspace.mrn')} value={patient.medicalRecordNumber} mono />
            <MetadataRow label={t('patient.birthDate')} value={patient.birthDate} mono />
            <MetadataRow label={t('patient.gender')} value={t(`gender.${patient.gender}`)} />
          </MetadataSection>

          {/*
            Keyed on the study, and that key is load-bearing rather than a lint
            appeasement: moving between a patient's studies is a client-side
            navigation into the same component tree, so without it React would
            keep this panel's state and one study's measurements would be read as
            another's.
          */}
          <AnalysisPanel
            key={study.studyInstanceUid}
            studyInstanceUid={study.studyInstanceUid}
            initialAnalysis={analysis}
          />
        </div>
      ) : null}
    </aside>
  );
}

/**
 * Heading for one series.
 *
 * Zero means the archive reported no Series Number, so the heading drops the
 * number rather than claiming "Series 0" - which is not a value DICOM can hold
 * and would read as a real series identifier.
 */
function seriesTitle(series: Series, t: Translator): string {
  return series.seriesNumber > 0
    ? t('series.numbered', { number: series.seriesNumber })
    : t('series.unnumbered');
}

function MetadataSection({
  title,
  children,
}: {
  readonly title: string;
  readonly children: React.ReactNode;
}) {
  return (
    // A rule above every section but the first. The rail is a single scrolling
    // column of small grey-on-white type, and without a separator the eye has
    // nothing to use to tell one group of readings from the next.
    <section className="border-border mt-5 border-t pt-4 first:mt-0 first:border-t-0 first:pt-0">
      <h3 className="text-muted mb-2.5 text-[0.625rem] tracking-[0.08em] uppercase">{title}</h3>
      <dl className="flex flex-col gap-1.5">{children}</dl>
    </section>
  );
}

function MetadataRow({
  label,
  value,
  mono = false,
  wrap = false,
}: {
  readonly label: string;
  readonly value: string;
  readonly mono?: boolean;
  readonly wrap?: boolean;
}) {
  return (
    // Label and value on one line, in two fixed columns. Stacking them doubled
    // the height of a rail whose whole purpose is to hold a lot of readings at
    // once, and a ragged left edge on the values made them impossible to scan
    // vertically. The label column is fixed so every value starts at the same x.
    <div className="grid grid-cols-[5.75rem_minmax(0,1fr)] items-baseline gap-x-3">
      <dt className="text-muted truncate text-[0.6875rem] leading-5">{label}</dt>
      <dd
        className={cn(
          'text-foreground text-[0.8125rem] leading-5',
          mono && 'font-mono text-[0.75rem]',
          wrap ? 'break-all' : 'truncate',
        )}
        title={value}
      >
        {value}
      </dd>
    </div>
  );
}
