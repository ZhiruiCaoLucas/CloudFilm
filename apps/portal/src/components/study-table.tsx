import Link from 'next/link';
import {
  formatModality,
  STUDY_STATUS_LABELS,
  type Study,
  type StudyStatus,
} from '@cloudfilm/shared';
import { buttonClasses, StatusBadge, type StatusTone } from '@cloudfilm/ui';

/**
 * Maps a study's lifecycle state onto the design system's tones.
 *
 * Exhaustive over {@link StudyStatus}, so adding a status will not compile until
 * its presentation is decided here.
 */
const STATUS_TONES: Record<StudyStatus, StatusTone> = {
  Completed: 'healthy',
  InProgress: 'degraded',
  Cancelled: 'unknown',
};

/**
 * The patient's imaging studies.
 *
 * A real table rather than a grid of cards: this is tabular clinical data, and
 * the semantics let a screen reader announce "Study date, 2026-07-01" instead of
 * reading a bare date. It scrolls horizontally rather than reflowing, so columns
 * never silently disappear on a ward tablet.
 */
export function StudyTable({ studies }: { readonly studies: readonly Study[] }) {
  return (
    <div className="border-border overflow-x-auto rounded-lg border">
      <table className="w-full min-w-[42rem] border-collapse text-left text-sm">
        <caption className="sr-only">Imaging studies for this patient</caption>
        <thead className="bg-surface-overlay text-muted text-xs tracking-wide uppercase">
          <tr>
            <th scope="col" className="px-5 py-3 font-medium">
              Modality
            </th>
            <th scope="col" className="px-5 py-3 font-medium">
              Description
            </th>
            <th scope="col" className="px-5 py-3 font-medium">
              Study date
            </th>
            <th scope="col" className="px-5 py-3 font-medium">
              Series
            </th>
            <th scope="col" className="px-5 py-3 font-medium">
              Status
            </th>
            <th scope="col" className="px-5 py-3 text-right font-medium">
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {studies.map((study) => (
            <StudyRow key={study.studyInstanceUid} study={study} />
          ))}
        </tbody>
      </table>
    </div>
  );
}

function StudyRow({ study }: { readonly study: Study }) {
  return (
    <tr className="border-border border-t align-middle">
      <th scope="row" className="text-foreground px-5 py-4 font-medium">
        {formatModality(study.modality)}
      </th>
      <td className="text-muted px-5 py-4">{study.description}</td>
      <td className="text-foreground px-5 py-4 font-mono text-xs">{study.performedOn}</td>
      <td className="text-muted px-5 py-4 tabular-nums">{study.seriesCount}</td>
      <td className="px-5 py-4">
        <StatusBadge tone={STATUS_TONES[study.status]}>
          {STUDY_STATUS_LABELS[study.status]}
        </StatusBadge>
      </td>
      <td className="px-5 py-4">
        <div className="flex justify-end">
          <Link
            href={`/workspace/${encodeURIComponent(study.studyInstanceUid)}`}
            className={buttonClasses('secondary')}
          >
            Open Study
            <span className="sr-only">
              : {formatModality(study.modality)}, {study.performedOn}
            </span>
          </Link>
        </div>
      </td>
    </tr>
  );
}
