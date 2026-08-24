import Link from 'next/link';
import {
  formatModality,
  STUDY_STATUS_MESSAGE_KEYS,
  type Patient,
  type Series,
  type Study,
  type Translator,
} from '@cloudfilm/shared';
import { cn } from '@cloudfilm/ui';
import { StudyThumbnail } from './study-thumbnail';
import { serverTranslations } from '@/lib/locale';

/**
 * Left rail: who the patient is, which studies exist, and what is inside the
 * one on screen.
 *
 * Restructured from a flat list into three stacked regions, each with its own
 * scroll behaviour:
 *
 *   1. a fixed rail header naming the region and counting what is in it;
 *   2. a patient identity card - a white card on the rail's pale ground, so
 *      identity reads as a fixed fact rather than as the first row of a list;
 *   3. a scrolling navigator in which the active study expands to reveal its
 *      series. Series were previously visible only in the metadata rail, which
 *      meant choosing what to look at and seeing what was available happened on
 *      opposite sides of the screen.
 *
 * The series list is rendered from data the page already fetched for the
 * metadata rail. Nothing new is requested.
 *
 * Each study is an anchor rather than a click handler. Selecting a study is a
 * change of address, so it must be linkable, middle-clickable, and reachable by
 * Tab — all of which come free from `<a>` and none of which come free from a
 * `div` with an `onClick`.
 */
export async function StudyNavigator({
  patient,
  studies,
  series,
  selectedStudyInstanceUid,
}: {
  readonly patient: Patient;
  readonly studies: readonly Study[];
  readonly series: readonly Series[];
  readonly selectedStudyInstanceUid: string;
}) {
  const t = await serverTranslations();

  return (
    <aside
      aria-label={t('workspace.navigatorLabel')}
      // No border on the edge that meets the viewport: a near-black column
      // beside a pale rail is already the strongest separation on the screen,
      // and a hairline drawn on top of it only makes the layout read as three
      // boxes bolted together.
      className="bg-atrium-panel hidden min-h-0 shrink-0 flex-col lg:flex"
    >
      {/* ---- rail header, aligned to the title bar's height -------------- */}
      <div className="border-atrium-border flex h-[68px] shrink-0 items-center justify-between gap-2 border-b px-4">
        <h2 className="atrium-eyebrow text-atrium-muted">{t('workspace.navigatorLabel')}</h2>
        <span className="bg-atrium-white border-atrium-border text-atrium-muted rounded border px-1.5 py-0.5 font-mono text-[11px]">
          {studies.length}
        </span>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto px-3 pb-4">
        {/* ---- patient identity, as a card on the rail ------------------- */}
        <section className="border-atrium-border bg-atrium-white rounded-xl border p-4 shadow-[var(--shadow-sm)]">
          <h3 className="atrium-eyebrow text-atrium-muted">{t('workspace.patient')}</h3>
          <p className="text-atrium-ink mt-2 text-[14.5px] leading-tight font-semibold tracking-[-0.02em]">
            {patient.fullName}
          </p>
          <dl className="text-atrium-muted mt-3 grid grid-cols-[3.25rem_minmax(0,1fr)] gap-x-3 text-[11.5px] leading-[22px]">
            <dt>{t('workspace.mrn')}</dt>
            <dd className="text-atrium-ink truncate font-mono">{patient.medicalRecordNumber}</dd>
            <dt>{t('workspace.born')}</dt>
            <dd className="text-atrium-ink font-mono">{patient.birthDate}</dd>
            <dt>{t('workspace.gender')}</dt>
            <dd className="text-atrium-ink">{t(`gender.${patient.gender}`)}</dd>
          </dl>
        </section>

        {/* ---- studies, with the active one opened ----------------------- */}
        <nav aria-label={t('workspace.studiesLabel')} className="mt-5">
          <h3 className="atrium-eyebrow text-atrium-muted px-1 pb-2.5">
            {t('workspace.studiesCount', { count: studies.length })}
          </h3>

          <ul className="flex flex-col gap-1.5">
            {studies.map((study) => {
              const isSelected = study.studyInstanceUid === selectedStudyInstanceUid;

              return (
                <li key={study.studyInstanceUid}>
                  <StudyNavigatorItem study={study} isSelected={isSelected} t={t} />

                  {/*
                    Only the open study lists its series, and only when the
                    archive reported any. A collapsed row stays one line tall,
                    so a patient with a dozen studies is still one scroll.
                  */}
                  {isSelected && series.length > 0 ? <SeriesList series={series} t={t} /> : null}
                </li>
              );
            })}
          </ul>
        </nav>
      </div>
    </aside>
  );
}

/**
 * One study in the navigator.
 *
 * The active row is a raised white card with a 3px rule in the primary down its
 * leading edge; every other row is flat on the rail and tints on hover. The
 * contrast is deliberate - a reader stepping through studies with `[` and `]`
 * has to be able to find the current one without reading any of them.
 */
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
        'focus-visible:outline-atrium-primary flex w-full items-center gap-3 rounded-lg p-2.5 text-left',
        'border-l-[3px] transition-[background-color,border-color,box-shadow] duration-[180ms]',
        // A selected row keeps its elevation; an unselected one gains a hairline
        // of it on hover, so the list answers the pointer without jumping.
        'hover:shadow-[var(--shadow-sm)]',
        'focus-visible:outline-2 focus-visible:-outline-offset-2',
        isSelected
          ? 'border-atrium-primary bg-atrium-white shadow-[var(--shadow-sm)]'
          : 'hover:bg-atrium-white/70 border-transparent',
      )}
    >
      <StudyThumbnail modality={study.modality} />

      <span className="flex min-w-0 flex-1 flex-col gap-1">
        <span className="flex min-w-0 items-baseline justify-between gap-2">
          <span className="text-atrium-ink truncate text-[12.5px] font-semibold">
            {formatModality(study.modality)}
          </span>
          <span className="text-atrium-muted shrink-0 font-mono text-[10.5px]">
            {study.performedOn}
          </span>
        </span>
        <span className="text-atrium-body truncate text-[12px] leading-tight">
          {study.description}
        </span>
        <span className="text-atrium-muted flex items-center gap-1.5 text-[10.5px]">
          <span className="font-mono">
            {t('study.series')} {study.seriesCount}
          </span>
          <span aria-hidden="true" className="text-[var(--atrium-muted-2)]">
            ·
          </span>
          <span>{t(STUDY_STATUS_MESSAGE_KEYS[study.status])}</span>
        </span>
      </span>
    </Link>
  );
}

/**
 * The series inside the open study.
 *
 * Indented under the study row and hung off a vertical hairline, so the nesting
 * is drawn rather than merely implied by padding. Not links: CloudFilm has no
 * per-series route, and the viewer owns series selection - these say what is in
 * the study, which is the question the rail is answering.
 */
function SeriesList({ series, t }: { readonly series: readonly Series[]; readonly t: Translator }) {
  return (
    <ul className="border-atrium-border atrium-swap mt-1.5 ml-[26px] flex flex-col gap-px border-l pl-3">
      {series.map((item) => (
        <li
          key={item.seriesInstanceUid}
          className="hover:bg-atrium-white/70 flex min-w-0 items-baseline justify-between gap-2 rounded py-1.5 pr-1 pl-1 transition-colors duration-[180ms]"
        >
          <span className="flex min-w-0 items-baseline gap-2">
            <span className="text-atrium-muted shrink-0 font-mono text-[10.5px]">
              {item.seriesNumber > 0 ? String(item.seriesNumber).padStart(2, '0') : '—'}
            </span>
            <span className="text-atrium-body truncate text-[11.5px]">
              {item.description.length > 0 ? item.description : formatModality(item.modality)}
            </span>
          </span>
          <span className="text-atrium-muted shrink-0 font-mono text-[10.5px]">
            {t('series.images')} {item.instanceCount}
          </span>
        </li>
      ))}
    </ul>
  );
}
