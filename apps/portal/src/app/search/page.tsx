import type { Metadata } from 'next';
import Link from 'next/link';
import { ApiError } from '@cloudfilm/core';
import { STUDY_SEARCH, type StudySearchResult, type Translator } from '@cloudfilm/shared';
import { buttonClasses, EmptyState } from '@cloudfilm/ui';
import { PortalShell } from '@/components/portal-shell';
import { SearchResultsTable } from '@/components/search-results-table';
import { StudySearchForm, type StudySearchFields } from '@/components/study-search-form';
import { currentUser, serverApiClient } from '@/lib/api';
import { serverTranslations } from '@/lib/locale';

interface SearchPageProps {
  readonly searchParams: Promise<Record<string, string | readonly string[] | undefined>>;
}

/**
 * The document title, deliberately free of anything the search contained.
 *
 * A title is written to the tab strip, the window title, the OS task bar and
 * browser history - which a signed-in browser profile syncs to its vendor - and
 * into every screen share and recording. None of those are surfaces CloudFilm
 * can audit. A search title is the worst offender available: the query itself
 * is identifying even before its results are, so the criteria, the patient
 * name, the accession number and the result count all stay out of it.
 *
 * Takes no arguments for exactly that reason: there is nothing to read here.
 */
export async function generateMetadata(): Promise<Metadata> {
  const t = await serverTranslations();

  return { title: t('search.pageTitle') };
}

/**
 * Study search.
 *
 * The whole state of this page is its URL. The form writes criteria into the
 * query string, the server reads them back and runs the search, and the results
 * are rendered from that one round trip - so a search can be refreshed, shared
 * with a colleague, and walked backwards through with the browser's own
 * buttons. Nothing is held in component state, and nothing is cached.
 */
export default async function SearchPage({ searchParams }: SearchPageProps) {
  const parameters = await searchParams;
  const t = await serverTranslations();

  const fields: StudySearchFields = {
    patientName: single(parameters['patientName']),
    accessionNumber: single(parameters['accessionNumber']),
    studyDateFrom: single(parameters['studyDateFrom']),
    studyDateTo: single(parameters['studyDateTo']),
  };

  const page = toPage(single(parameters['page']));
  const hasCriteria = Object.values(fields).some((value) => value.length > 0);

  const user = await currentUser();

  // No criteria means no request. The API would answer 400, and asking it to
  // say so would turn an empty form into an error card.
  const outcome = hasCriteria ? await search(fields, page) : null;

  return (
    <PortalShell user={user}>
      <div className="flex flex-col gap-[34px]">
        <nav aria-label={t('patient.breadcrumbLabel')}>
          <Link
            href="/"
            className="text-atrium-muted hover:text-atrium-primary focus-visible:outline-atrium-primary rounded text-[13px] font-medium focus-visible:outline-2 focus-visible:outline-offset-4"
          >
            {t('search.breadcrumb')}
          </Link>
        </nav>

        {/*
          Masthead and the criteria that produced what is below it, on one line:
          the reader's question on the left, its current answer on the right.
        */}
        <div className="atrium-enter-1 flex flex-wrap items-end justify-between gap-x-8 gap-y-5">
          <div className="border-atrium-primary flex min-w-0 flex-col gap-2 border-l-[3px] pl-[18px]">
            <h1 className="atrium-h1 text-atrium-ink">{t('search.title')}</h1>
            <p className="atrium-body text-atrium-body max-w-[62ch]">{t('search.subtitle')}</p>
          </div>

          {hasCriteria ? <CriteriaChips fields={fields} t={t} /> : null}
        </div>

        {/*
          The criteria form. Open when there is nothing to show, folded away
          behind "Edit criteria" once there is - a native `details`, so the form
          stays in the DOM, submits identically, and needs no JavaScript to
          disclose. Nothing about the search itself changes.
        */}
        <details open={outcome === null || outcome === 'refused'} className="atrium-enter-2 group">
          <summary className="marker:content-none [&::-webkit-details-marker]:hidden">
            <span className={buttonClasses('secondary', 'cursor-pointer', 'sm')}>
              <span className="atrium-arrow inline-block transition-transform duration-[180ms] group-open:rotate-90">
                ›
              </span>
              {t('search.editCriteria')}
            </span>
          </summary>

          <div className="atrium-card atrium-swap mt-4 px-6 py-7 sm:px-8 sm:py-8">
            <StudySearchForm initial={fields} />
          </div>
        </details>

        <section
          aria-labelledby="search-results-heading"
          className="atrium-enter-3 flex flex-col gap-[18px]"
        >
          {outcome === null ? (
            <>
              <h2 id="search-results-heading" className="atrium-eyebrow text-atrium-muted">
                {t('search.resultsHeading')}
              </h2>
              <EmptyState title={t('search.promptTitle')} description={t('search.promptBody')} />
            </>
          ) : outcome === 'refused' ? (
            <>
              <h2 id="search-results-heading" className="atrium-eyebrow text-atrium-muted">
                {t('search.resultsHeading')}
              </h2>
              <div className="rounded-xl bg-[var(--atrium-danger-bg)] px-5 py-4 shadow-[inset_0_0_0_1px_#b423183d]">
                <h3 className="atrium-h3 text-[var(--atrium-danger)]">
                  {t('search.invalidTitle')}
                </h3>
                <p className="atrium-body mt-1.5 text-[var(--atrium-danger)]/90">
                  {t('search.invalidBody')}
                </p>
              </div>
            </>
          ) : outcome.items.length === 0 ? (
            <>
              <h2 id="search-results-heading" className="atrium-eyebrow text-atrium-muted">
                {t('search.resultsHeading')}
              </h2>
              <EmptyState
                title={t('search.noResultsTitle')}
                description={t('search.noResultsBody')}
              />
            </>
          ) : (
            <Results fields={fields} result={outcome} t={t} />
          )}
        </section>
      </div>
    </PortalShell>
  );
}

/**
 * The criteria currently applied, each removable.
 *
 * Every chip is a link to the same search minus one field, built by the same
 * `searchHref` the pager uses - so removing a criterion is a normal navigation
 * that the back button undoes, and no filter state exists anywhere but the URL.
 * The page resets to zero when a criterion is dropped, because page four of the
 * old query is not a meaningful position in the new one.
 */
function CriteriaChips({
  fields,
  t,
}: {
  readonly fields: StudySearchFields;
  readonly t: Translator;
}) {
  const applied = (
    [
      ['patientName', 'search.patientName'],
      ['accessionNumber', 'search.accessionNumber'],
      ['studyDateFrom', 'search.dateFrom'],
      ['studyDateTo', 'search.dateTo'],
    ] as const
  ).filter(([field]) => fields[field].length > 0);

  if (applied.length === 0) {
    return null;
  }

  return (
    <ul aria-label={t('search.criteriaLabel')} className="flex flex-wrap items-center gap-2">
      {applied.map(([field, labelKey]) => (
        <li key={field}>
          <Link
            href={searchHref({ ...fields, [field]: '' }, 0)}
            className="border-atrium-border-strong bg-atrium-white text-atrium-body hover:border-atrium-primary hover:text-atrium-ink focus-visible:outline-atrium-primary group inline-flex items-center gap-2 rounded-lg border py-1.5 pr-2.5 pl-3 text-[12px] shadow-[var(--shadow-sm)] transition-[color,border-color,box-shadow] duration-[180ms] focus-visible:outline-2 focus-visible:outline-offset-2"
          >
            <span className="text-atrium-muted">{t(labelKey)}</span>
            <span className="text-atrium-ink font-mono text-[11.5px]">{fields[field]}</span>
            <span
              aria-hidden="true"
              className="group-hover:text-atrium-primary text-[13px] leading-none text-[var(--atrium-muted-2)] transition-colors duration-[180ms]"
            >
              ×
            </span>
            <span className="sr-only">{t('search.removeCriterion')}</span>
          </Link>
        </li>
      ))}
    </ul>
  );
}

/**
 * Runs one search.
 *
 * @returns The page, or `'refused'` when the API rejected the criteria.
 * @remarks
 * Only a 400 is caught, and only to replace a generic error card with a sentence
 * the reader can act on. Everything else - including the redirect a 401 throws -
 * propagates untouched, which is what keeps the sign-in flow working from this
 * page.
 */
async function search(
  fields: StudySearchFields,
  page: number,
): Promise<StudySearchResult | 'refused'> {
  const client = await serverApiClient();

  try {
    return await client.searchStudies({
      patientName: blankToUndefined(fields.patientName),
      accessionNumber: blankToUndefined(fields.accessionNumber),
      studyDateFrom: blankToUndefined(fields.studyDateFrom),
      studyDateTo: blankToUndefined(fields.studyDateTo),
      page,
      pageSize: STUDY_SEARCH.defaultPageSize,
    });
  } catch (error) {
    if (error instanceof ApiError && error.status === 400) {
      return 'refused';
    }

    throw error;
  }
}

/**
 * One page of results, with the controls to move between pages.
 *
 * Both controls are links, not buttons: turning a page changes the address, so
 * it has to be middle-clickable, openable in a new tab and reachable with the
 * browser's own back button. A disabled link is rendered as plain text rather
 * than a link nobody may follow.
 */
function Results({
  fields,
  result,
  t,
}: {
  readonly fields: StudySearchFields;
  readonly result: StudySearchResult;
  readonly t: Translator;
}) {
  const isFirstPage = result.page === 0;

  return (
    <>
      {/*
        Heading, count and page position share one line. Three stacked lines of
        small grey text above a table is vertical space spent on chrome, and it
        pushes the first result below the fold on a laptop.
      */}
      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
        <h2 id="search-results-heading" className="atrium-eyebrow text-atrium-muted">
          {t('search.resultsHeading')}
        </h2>
        <p className="text-atrium-muted flex items-baseline gap-2 text-[12px]">
          <span>
            {t(result.items.length === 1 ? 'search.resultCountOne' : 'search.resultCountOther', {
              count: result.items.length,
            })}
          </span>
          <span aria-hidden="true" className="text-[var(--atrium-muted-2)]">
            ·
          </span>
          <span className="text-atrium-body font-mono text-[11.5px]">
            {t('search.pagePosition', { page: result.page + 1 })}
          </span>
        </p>
      </div>

      <div className="atrium-swap">
        <SearchResultsTable items={result.items} />
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3">
        {/*
          Said out loud rather than left for the reader to infer from a Next
          control that happens to be enabled. The archive reports no total, so
          "there is more" is the only honest thing that can be said about what
          was not shown.
        */}
        <p className="text-atrium-muted max-w-prose text-[12px]">
          {result.hasMore ? t('search.moreAvailable') : null}
        </p>

        <nav aria-label={t('search.resultsHeading')} className="flex items-center gap-2">
          {/* The reachable side is a control; the unreachable side is plain
              text. A disabled link is a thing that looks pressable and is not. */}
          {isFirstPage ? (
            <PagerPlaceholder>{t('search.previous')}</PagerPlaceholder>
          ) : (
            <Link
              href={searchHref(fields, result.page - 1)}
              className="text-atrium-body hover:text-atrium-primary focus-visible:outline-atrium-primary group inline-flex min-h-9 items-center gap-1.5 rounded px-2 text-[13px] font-semibold transition-colors duration-[180ms] focus-visible:outline-2 focus-visible:outline-offset-2"
            >
              <span
                aria-hidden="true"
                className="inline-block transition-transform duration-[180ms] group-hover:-translate-x-0.5"
              >
                ‹
              </span>
              {t('search.previous')}
            </Link>
          )}

          {result.hasMore ? (
            <Link
              href={searchHref(fields, result.page + 1)}
              className={buttonClasses('secondary', 'group border-atrium-border-strong', 'sm')}
            >
              {t('search.next')}
              <span
                aria-hidden="true"
                className="inline-block transition-transform duration-[180ms] group-hover:translate-x-0.5"
              >
                ›
              </span>
            </Link>
          ) : (
            <PagerPlaceholder>{t('search.next')}</PagerPlaceholder>
          )}
        </nav>
      </div>
    </>
  );
}

/**
 * The unavailable side of the pager.
 *
 * Plain text rather than a disabled link: there is no page to go to, so there
 * is nothing to link to. It keeps the height and padding of the real control so
 * the pair stays aligned whichever side is live.
 */
function PagerPlaceholder({ children }: { readonly children: React.ReactNode }) {
  return (
    <span className="inline-flex min-h-9 items-center px-3 text-[13px] font-medium text-[var(--atrium-muted-2)]">
      {children}
    </span>
  );
}

/**
 * The address of one search: its criteria and a page number.
 *
 * Carries the criteria and the page number, and nothing else. The archive's own
 * vocabulary - limits, offsets, wildcards - is the API's business and never
 * appears in a URL a clinician can see or edit.
 */
function searchHref(fields: StudySearchFields, page: number): string {
  const parameters = new URLSearchParams();

  for (const [name, value] of Object.entries(fields)) {
    if (value.length > 0) {
      parameters.set(name, value);
    }
  }

  parameters.set('page', String(page));

  return `/search?${parameters.toString()}`;
}

/** Reads one query-string value, taking the first when a parameter repeats. */
function single(value: string | readonly string[] | undefined): string {
  if (Array.isArray(value)) {
    return value[0] ?? '';
  }

  return typeof value === 'string' ? value.trim() : '';
}

/**
 * Reads the page number.
 *
 * Anything that is not a whole number at or above zero is the first page. A
 * hand-edited URL is not worth an error page, and the API validates the value it
 * is actually given.
 */
function toPage(value: string): number {
  const parsed = Number.parseInt(value, 10);

  return Number.isInteger(parsed) && parsed > 0 ? parsed : 0;
}

function blankToUndefined(value: string): string | undefined {
  return value.length > 0 ? value : undefined;
}
