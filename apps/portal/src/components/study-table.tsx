import Link from 'next/link';
import {
  formatModality,
  STUDY_STATUS_MESSAGE_KEYS,
  type Study,
  type StudyStatus,
  type Translator,
} from '@cloudfilm/shared';
import { buttonClasses, StatusBadge, type StatusTone } from '@cloudfilm/ui';
import { serverTranslations } from '@/lib/locale';

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
export async function StudyTable({ studies }: { readonly studies: readonly Study[] }) {
  const t = await serverTranslations();

  return (
    <div className="border-border overflow-x-auto rounded-lg border">
      <table className="w-full min-w-[42rem] border-collapse text-left text-sm">
        <caption className="sr-only">{t('study.tableCaption')}</caption>
        <thead className="bg-surface-overlay text-muted text-[0.625rem] tracking-[0.08em] uppercase">
          <tr>
            <th scope="col" className="px-4 py-2.5 font-medium">
              {t('study.modality')}
            </th>
            <th scope="col" className="px-4 py-2.5 font-medium">
              {t('study.description')}
            </th>
            <th scope="col" className="px-4 py-2.5 font-medium">
              {t('study.date')}
            </th>
            <th scope="col" className="px-4 py-2.5 font-medium">
              {t('study.series')}
            </th>
            <th scope="col" className="px-4 py-2.5 font-medium">
              {t('study.status')}
            </th>
            <th scope="col" className="px-4 py-2.5 text-right font-medium">
              <span className="sr-only">{t('study.actions')}</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {studies.map((study) => (
            <StudyRow key={study.studyInstanceUid} study={study} t={t} />
          ))}
        </tbody>
      </table>
    </div>
  );
}

function StudyRow({ study, t }: { readonly study: Study; readonly t: Translator }) {
  return (
    <tr className="border-border hover:bg-surface-overlay/40 border-t align-middle transition-colors">
      <th scope="row" className="text-foreground px-4 py-3 font-medium">
        {formatModality(study.modality)}
      </th>
      <td className="text-muted px-4 py-3">{study.description}</td>
      <td className="text-foreground px-4 py-3 font-mono text-xs">{study.performedOn}</td>
      <td className="text-muted px-4 py-3 tabular-nums">{study.seriesCount}</td>
      <td className="px-4 py-3">
        <StatusBadge tone={STATUS_TONES[study.status]}>
          {t(STUDY_STATUS_MESSAGE_KEYS[study.status])}
        </StatusBadge>
      </td>
      <td className="px-4 py-3">
        <div className="flex justify-end">
          <Link
            href={`/workspace/${encodeURIComponent(study.studyInstanceUid)}`}
            className={buttonClasses('secondary', 'min-h-9 px-3 whitespace-nowrap')}
          >
            {t('study.open')}
            <span className="sr-only">
              : {formatModality(study.modality)}, {study.performedOn}
            </span>
          </Link>
        </div>
      </td>
    </tr>
  );
}
