import Link from 'next/link';
import { formatModality, type StudySearchItem, type Translator } from '@cloudfilm/shared';
import { buttonClasses } from '@cloudfilm/ui';
import { serverTranslations } from '@/lib/locale';

/**
 * Studies matching a search, across patients.
 *
 * A table rather than cards, like the patient page's study list: this is
 * tabular clinical data, and the semantics let a screen reader announce
 * "Accession, ACC-1" instead of reading a bare string. It scrolls horizontally
 * rather than reflowing, so no column silently disappears on a ward tablet.
 *
 * The patient is the first column and the row header. A search is the one list
 * in CloudFilm that crosses patients, so whose study a row describes is the
 * first thing a reader has to be able to see - and it is what makes the second
 * link, to the patient's own record, worth having.
 */
export async function SearchResultsTable({
  items,
}: {
  readonly items: readonly StudySearchItem[];
}) {
  const t = await serverTranslations();

  return (
    <div className="border-border overflow-x-auto rounded-lg border">
      <table className="w-full min-w-[56rem] border-collapse text-left text-sm">
        <caption className="sr-only">{t('search.tableCaption')}</caption>
        <thead className="bg-surface-overlay text-muted text-[0.625rem] tracking-[0.08em] uppercase">
          <tr>
            <th scope="col" className="px-4 py-2.5 font-medium">
              {t('search.patient')}
            </th>
            <th scope="col" className="px-4 py-2.5 font-medium">
              {t('patient.medicalRecordNumber')}
            </th>
            <th scope="col" className="px-4 py-2.5 font-medium whitespace-nowrap">
              {t('study.date')}
            </th>
            <th scope="col" className="px-4 py-2.5 font-medium">
              {t('study.modality')}
            </th>
            <th scope="col" className="px-4 py-2.5 font-medium">
              {t('study.description')}
            </th>
            <th scope="col" className="px-4 py-2.5 font-medium">
              {t('study.accession')}
            </th>

            {/*
              Counts are right aligned so their digits line up down the column,
              which is what makes a column of numbers scannable at all.
            */}
            <th scope="col" className="px-4 py-2.5 text-right font-medium">
              {t('study.series')}
            </th>
            <th scope="col" className="px-4 py-2.5 text-right font-medium">
              {t('study.images')}
            </th>
            <th scope="col" className="px-4 py-2.5 text-right font-medium">
              <span className="sr-only">{t('study.actions')}</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <ResultRow key={item.study.studyInstanceUid} item={item} t={t} />
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ResultRow({ item, t }: { readonly item: StudySearchItem; readonly t: Translator }) {
  const { patient, study } = item;

  return (
    <tr className="border-border hover:bg-surface-overlay/40 border-t align-middle transition-colors">
      {/*
        The patient carries the row. Name at full weight, medical record number
        beside it in the next column at a smaller size and in mono - close
        enough to read together, plainly subordinate to the name.
      */}
      <th scope="row" className="text-foreground max-w-56 px-4 py-3 font-medium">
        <Link
          href={`/patient/${encodeURIComponent(patient.medicalRecordNumber)}`}
          className="hover:text-accent focus-visible:outline-accent block truncate rounded-sm focus-visible:outline-2 focus-visible:outline-offset-2"
          title={patient.fullName}
        >
          {patient.fullName}
        </Link>
      </th>
      <td className="text-muted px-4 py-3 font-mono text-xs whitespace-nowrap">
        {patient.medicalRecordNumber}
      </td>
      <td className="text-foreground px-4 py-3 font-mono text-xs whitespace-nowrap">
        {study.performedOn}
      </td>
      <td className="text-foreground px-4 py-3 whitespace-nowrap">
        {formatModality(study.modality)}
      </td>

      {/*
        Descriptions and accession numbers are free text from another system and
        can be arbitrarily long. Both are capped and truncated so one unusual
        study cannot stretch the table past every other row, with the full value
        on the element itself for anyone who needs it.
      */}
      <td className="text-muted max-w-72 px-4 py-3">
        <span className="block truncate" title={study.description}>
          {study.description}
        </span>
      </td>
      <td className="text-muted max-w-40 px-4 py-3 font-mono text-xs">
        <span className="block truncate" title={study.accessionNumber}>
          {study.accessionNumber}
        </span>
      </td>
      <td className="text-muted px-4 py-3 text-right tabular-nums">{study.seriesCount}</td>
      <td className="text-muted px-4 py-3 text-right tabular-nums">{study.instanceCount}</td>
      <td className="px-4 py-3">
        <div className="flex justify-end">
          <Link
            href={`/workspace/${encodeURIComponent(study.studyInstanceUid)}`}
            className={buttonClasses('secondary', 'min-h-9 px-3 whitespace-nowrap')}
          >
            {t('study.open')}

            {/*
              Every row's button reads "Open Study", so a screen reader listing
              the links would hear the same words repeated. This says which one.
            */}
            <span className="sr-only">
              : {patient.fullName}, {formatModality(study.modality)}, {study.performedOn}
            </span>
          </Link>
        </div>
      </td>
    </tr>
  );
}
