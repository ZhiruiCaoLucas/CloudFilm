'use client';

import { useState } from 'react';
import {
  formatModality,
  STUDY_STATUS_MESSAGE_KEYS,
  type Patient,
  type Series,
  type Study,
  type StudyAnalysis,
  type StudyStatus,
  type Translator,
} from '@cloudfilm/shared';
import { Button, cn, StatusBadge, type StatusTone } from '@cloudfilm/ui';
import { useTranslations } from '@/components/locale-provider';
import { AnalysisPanel } from './analysis-panel';
import { CollapsibleSection, IdentifierBlock, MetadataRow } from './metadata-primitives';

const STATUS_TONES: Record<StudyStatus, StatusTone> = {
  Completed: 'healthy',
  InProgress: 'degraded',
  Cancelled: 'unknown',
};

/**
 * Right rail, ordered by priority rather than by data category.
 *
 * It used to be four equal blocks of DICOM values with the analysis appended
 * last, which meant an action a clinician might want had to be discovered by
 * scrolling past everything they already knew. Three layers now:
 *
 *   A - context. The study on screen in four facts, at the top, never scrolled
 *       away. Deliberately not a repeat of the header or the navigator: no
 *       patient name, no MRN, no birth date, because all three are already on
 *       screen twice.
 *   B - analysis. Its own module on its own surface, directly under the context,
 *       with the primary action as the largest thing in it.
 *   C - detail. Everything else, in collapsed `<details>` groups. Still one
 *       click away, no longer competing for the first glance.
 *
 * Collapsed, the rail keeps a narrow strip rather than disappearing: the control
 * that reopens it has to stay where the reader left it.
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
      className={cn(
        // No border on the edge that meets the viewport, for the reason the
        // navigator has none: the tonal step already separates them.
        'bg-atrium-panel hidden min-h-0 shrink-0 flex-col xl:flex',
        // The rail changes width rather than appearing and disappearing, so the
        // viewport beside it resizes smoothly instead of snapping.
        'transition-[width] duration-[220ms] ease-[cubic-bezier(0.22,0.61,0.28,1)]',
        isOpen ? 'w-[360px]' : 'w-[52px]',
      )}
    >
      {/* ---- rail header, aligned to the title bar's height -------------- */}
      <div
        className={cn(
          'border-atrium-border bg-atrium-white flex h-[68px] shrink-0 items-center border-b',
          isOpen ? 'justify-between gap-2 px-5' : 'justify-center px-2',
        )}
      >
        {isOpen ? (
          <h2 className="atrium-eyebrow text-atrium-muted">{t('workspace.metadata')}</h2>
        ) : null}
        <Button
          variant="ghost"
          size="sm"
          onClick={() => setIsOpen((open) => !open)}
          aria-expanded={isOpen}
          className="text-atrium-primary px-2"
        >
          {isOpen ? t('workspace.hide') : t('workspace.info')}
        </Button>
      </div>

      {isOpen ? (
        <div className="atrium-swap min-h-0 flex-1 overflow-y-auto">
          {/* ---- Layer A: what is on screen ---------------------------- */}
          <section
            aria-labelledby="study-context-heading"
            className="border-atrium-border bg-atrium-white border-b px-5 pt-4 pb-5"
          >
            <h3 id="study-context-heading" className="atrium-eyebrow text-atrium-muted">
              {t('workspace.study')}
            </h3>

            <p className="text-atrium-ink mt-2 text-[14.5px] leading-snug font-semibold tracking-[-0.02em]">
              {study.description}
            </p>

            {/* Modality, date and status on one line, separated by dots. Three
                facts a reader confirms at a glance do not need three rows. */}
            <div className="mt-2.5 flex flex-wrap items-center gap-x-2 gap-y-2">
              <span className="text-atrium-body font-mono text-[11.5px] font-medium">
                {formatModality(study.modality)}
              </span>
              <span aria-hidden="true" className="text-[var(--atrium-muted-2)]">
                ·
              </span>
              <span className="text-atrium-body font-mono text-[11.5px]">{study.performedOn}</span>
              <StatusBadge tone={STATUS_TONES[study.status]} className="ml-auto">
                {t(STUDY_STATUS_MESSAGE_KEYS[study.status])}
              </StatusBadge>
            </div>
          </section>

          {/* ---- Layer B: the action ----------------------------------- */}
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

          {/* ---- Layer C: everything else, folded away ------------------ */}
          {/*
            Layer C sits on the rail's own ground with no card and no wrapper
            rule. That is what gives the analysis module above it the only
            elevation in the scroll area - the hierarchy is a change of surface,
            not another border.
          */}
          <div className="px-1 pb-4">
            <h3 className="atrium-eyebrow text-atrium-muted px-4 pt-2 pb-1">
              {t('workspace.studyDetails')}
            </h3>

            <CollapsibleSection title={t('workspace.studyInformation')}>
              <dl className="flex flex-col gap-1.5">
                <MetadataRow label={t('study.description')} value={study.description} />
                <MetadataRow label={t('study.modality')} value={formatModality(study.modality)} />
                <MetadataRow label={t('study.date')} value={study.performedOn} mono />

                {/*
                  Omitted rather than shown blank. A study with no accession
                  number is ordinary, and an empty row invites the reader to
                  wonder whether the value failed to load.
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
              </dl>
            </CollapsibleSection>

            {/*
              One group for every series, rather than one group per rail. The
              section is omitted entirely when the archive reported none.
            */}
            {series.length > 0 ? (
              <CollapsibleSection title={t('workspace.seriesInformation')}>
                <div className="flex flex-col gap-4">
                  {series.map((item) => (
                    <div key={item.seriesInstanceUid}>
                      <h4 className="text-atrium-ink mb-2 text-[12px] font-semibold">
                        {seriesTitle(item, t)}
                      </h4>
                      <dl className="flex flex-col gap-1.5">
                        {item.modality.length > 0 ? (
                          <MetadataRow
                            label={t('series.modality')}
                            value={formatModality(item.modality)}
                          />
                        ) : null}
                        {item.description.length > 0 ? (
                          <MetadataRow label={t('series.description')} value={item.description} />
                        ) : null}
                        {item.instanceCount > 0 ? (
                          <MetadataRow
                            label={t('series.images')}
                            value={String(item.instanceCount)}
                            mono
                          />
                        ) : null}
                        {item.institutionName.length > 0 ? (
                          <MetadataRow
                            label={t('series.institution')}
                            value={item.institutionName}
                          />
                        ) : null}
                      </dl>
                    </div>
                  ))}
                </div>
              </CollapsibleSection>
            ) : null}

            <CollapsibleSection title={t('workspace.patientInformation')}>
              <dl className="flex flex-col gap-1.5">
                <MetadataRow label={t('patient.name')} value={patient.fullName} />
                <MetadataRow label={t('workspace.mrn')} value={patient.medicalRecordNumber} mono />
                <MetadataRow label={t('patient.birthDate')} value={patient.birthDate} mono />
                <MetadataRow label={t('patient.gender')} value={t(`gender.${patient.gender}`)} />
              </dl>
            </CollapsibleSection>

            {/* The long identifiers, last. They are the values least often read
                and the ones most able to wreck a column if left in the grid. */}
            <CollapsibleSection title={t('workspace.technicalMetadata')}>
              <dl className="flex flex-col gap-1.5">
                <IdentifierBlock label={t('study.studyUid')} value={study.studyInstanceUid} />
                {series.map((item) => (
                  <IdentifierBlock
                    key={item.seriesInstanceUid}
                    label={seriesTitle(item, t)}
                    value={item.seriesInstanceUid}
                  />
                ))}
              </dl>
            </CollapsibleSection>
          </div>
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
