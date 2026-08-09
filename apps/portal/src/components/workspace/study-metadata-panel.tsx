'use client';

import { useState } from 'react';
import {
  formatModality,
  STUDY_STATUS_LABELS,
  type Patient,
  type Series,
  type Study,
} from '@cloudfilm/shared';
import { Button, cn } from '@cloudfilm/ui';

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
}: {
  readonly patient: Patient;
  readonly study: Study;
  readonly series: readonly Series[];
}) {
  const [isOpen, setIsOpen] = useState(true);

  return (
    <aside
      aria-label="Study metadata"
      className="border-border bg-surface-raised hidden shrink-0 flex-col border-l lg:flex"
    >
      <div className="border-border flex items-center justify-between gap-2 border-b px-3 py-2">
        {isOpen ? <h2 className="text-muted text-xs tracking-wide uppercase">Metadata</h2> : null}
        <Button
          variant="ghost"
          onClick={() => setIsOpen((open) => !open)}
          aria-expanded={isOpen}
          className="min-h-8 px-2 text-xs"
        >
          {isOpen ? 'Hide' : 'Info'}
        </Button>
      </div>

      {isOpen ? (
        <div className="w-72 overflow-y-auto px-3 py-4">
          <MetadataSection title="Study">
            <MetadataRow label="Description" value={study.description} />
            <MetadataRow label="Modality" value={formatModality(study.modality)} />
            <MetadataRow label="Study date" value={study.performedOn} mono />

            {/*
              Omitted rather than shown blank. A study with no accession number
              is ordinary, and an empty row invites the reader to wonder whether
              the value failed to load.
            */}
            {study.accessionNumber.length > 0 ? (
              <MetadataRow label="Accession" value={study.accessionNumber} mono />
            ) : null}

            <MetadataRow label="Status" value={STUDY_STATUS_LABELS[study.status]} />
            <MetadataRow label="Series" value={String(study.seriesCount)} mono />
            <MetadataRow label="Images" value={String(study.instanceCount)} mono />
            <MetadataRow label="Study UID" value={study.studyInstanceUid} mono wrap />
          </MetadataSection>

          {/*
            Patient -> Study -> Series -> Images -> Viewer. The section is
            omitted entirely when the archive reported no series, rather than
            rendered as an empty heading.
          */}
          {series.map((item) => (
            <MetadataSection key={item.seriesInstanceUid} title={seriesTitle(item)}>
              {item.modality.length > 0 ? (
                <MetadataRow label="Modality" value={formatModality(item.modality)} />
              ) : null}
              {item.description.length > 0 ? (
                <MetadataRow label="Description" value={item.description} />
              ) : null}
              {item.instanceCount > 0 ? (
                <MetadataRow label="Images" value={String(item.instanceCount)} mono />
              ) : null}
              {item.institutionName.length > 0 ? (
                <MetadataRow label="Institution" value={item.institutionName} />
              ) : null}
            </MetadataSection>
          ))}

          <MetadataSection title="Patient">
            <MetadataRow label="Name" value={patient.fullName} />
            <MetadataRow label="MRN" value={patient.medicalRecordNumber} mono />
            <MetadataRow label="Birth date" value={patient.birthDate} mono />
            <MetadataRow label="Gender" value={patient.gender} />
          </MetadataSection>
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
function seriesTitle(series: Series): string {
  return series.seriesNumber > 0 ? `Series ${series.seriesNumber}` : 'Series';
}

function MetadataSection({
  title,
  children,
}: {
  readonly title: string;
  readonly children: React.ReactNode;
}) {
  return (
    <section className="mb-6 last:mb-0">
      <h3 className="text-muted mb-2 text-xs tracking-wide uppercase">{title}</h3>
      <dl className="flex flex-col gap-2">{children}</dl>
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
    <div className="flex flex-col gap-0.5">
      <dt className="text-muted text-xs">{label}</dt>
      <dd
        className={cn(
          'text-foreground text-sm',
          mono && 'font-mono text-xs',
          wrap ? 'break-all' : 'truncate',
        )}
        title={value}
      >
        {value}
      </dd>
    </div>
  );
}
