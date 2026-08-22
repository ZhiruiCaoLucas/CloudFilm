import type { Metadata } from 'next';
import Link from 'next/link';
import { ApiError } from '@cloudfilm/core';
import { STUDY_SEARCH, type StudySearchResult, type Translator } from '@cloudfilm/shared';
import { buttonClasses, Card, CardBody, CardHeader, EmptyState } from '@cloudfilm/ui';
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
      <div className="flex flex-col gap-6">
        <nav aria-label={t('patient.breadcrumbLabel')}>
          <Link
            href="/"
            className="text-muted hover:text-foreground focus-visible:outline-accent rounded-md text-sm focus-visible:outline-2 focus-visible:outline-offset-4"
          >
            {t('search.breadcrumb')}
          </Link>
        </nav>

        {/* A two-pixel accent rule to the left of the title. It is the only
            colour above the fold, and it is what tells the eye where the page
            starts without a heading large enough to do the same job. */}
        <div className="border-accent flex flex-col gap-1 border-l-2 pl-4">
          <h1 className="text-foreground text-lg font-semibold tracking-tight">
            {t('search.title')}
          </h1>
          <p className="text-muted text-[0.8125rem]">{t('search.subtitle')}</p>
        </div>

        {/*
          The form sits on a raised surface so the page reads as two blocks -
          the question, then the answer - rather than one long column in which
          the criteria and the results run together.
        */}
        <div className="border-border bg-surface-raised cf-lift rounded-lg border bg-linear-to-b from-white/60 to-transparent px-5 py-5 shadow-[inset_0_1px_0_0_var(--glass-edge)] dark:from-white/[0.02]">
          <StudySearchForm initial={fields} />
        </div>

        <section aria-labelledby="search-results-heading" className="flex flex-col gap-3">
          {outcome === null ? (
            <>
              <h2 id="search-results-heading" className="text-foreground text-sm font-medium">
                {t('search.resultsHeading')}
              </h2>
              <EmptyState title={t('search.promptTitle')} description={t('search.promptBody')} />
            </>
          ) : outcome === 'refused' ? (
            <>
              <h2 id="search-results-heading" className="text-foreground text-sm font-medium">
                {t('search.resultsHeading')}
              </h2>
              <Card>
                <CardHeader>
                  <h3 className="text-foreground text-sm font-medium">
                    {t('search.invalidTitle')}
                  </h3>
                </CardHeader>
                <CardBody>
                  <p className="text-muted text-sm">{t('search.invalidBody')}</p>
                </CardBody>
              </Card>
            </>
          ) : outcome.items.length === 0 ? (
            <>
              <h2 id="search-results-heading" className="text-foreground text-sm font-medium">
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
        <h2 id="search-results-heading" className="text-foreground text-sm font-medium">
          {t('search.resultsHeading')}
        </h2>
        <p className="text-muted text-xs">
          {t(result.items.length === 1 ? 'search.resultCountOne' : 'search.resultCountOther', {
            count: result.items.length,
          })}
          <span aria-hidden="true"> · </span>
          {t('search.pagePosition', { page: result.page + 1 })}
        </p>
      </div>

      <SearchResultsTable items={result.items} />

      <div className="flex flex-wrap items-center justify-between gap-3">
        {/*
          Said out loud rather than left for the reader to infer from a Next
          control that happens to be enabled. The archive reports no total, so
          "there is more" is the only honest thing that can be said about what
          was not shown.
        */}
        <p className="text-muted max-w-prose text-xs">
          {result.hasMore ? t('search.moreAvailable') : null}
        </p>

        <nav aria-label={t('search.resultsHeading')} className="flex items-center gap-2">
          {isFirstPage ? (
            <PagerPlaceholder>{t('search.previous')}</PagerPlaceholder>
          ) : (
            <Link
              href={pageHref(fields, result.page - 1)}
              className={buttonClasses('secondary', 'min-h-9 px-3')}
            >
              {t('search.previous')}
            </Link>
          )}

          {result.hasMore ? (
            <Link
              href={pageHref(fields, result.page + 1)}
              className={buttonClasses('secondary', 'min-h-9 px-3')}
            >
              {t('search.next')}
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
    <span className="text-muted inline-flex min-h-9 items-center px-3 text-sm">{children}</span>
  );
}

/**
 * The address of another page of the same search.
 *
 * Carries the criteria and the page number, and nothing else. The archive's own
 * vocabulary - limits, offsets, wildcards - is the API's business and never
 * appears in a URL a clinician can see or edit.
 */
function pageHref(fields: StudySearchFields, page: number): string {
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
