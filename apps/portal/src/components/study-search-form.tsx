'use client';

import { useId, useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { Button, Input } from '@cloudfilm/ui';
import { useTranslations } from './locale-provider';

/** The criteria the form collects, as they appear in the URL. */
export interface StudySearchFields {
  readonly patientName: string;
  readonly accessionNumber: string;
  readonly studyDateFrom: string;
  readonly studyDateTo: string;
}

/**
 * Study search criteria.
 *
 * A client component because it owns transient input state and navigates on
 * submit. Submitting does not fetch: it changes the address, and the page
 * re-renders from the new query string on the server. That is what makes a
 * search shareable, refreshable, and reachable with the back button - and it is
 * why the results are never held in component state.
 *
 * The fields are pre-filled from the URL, so returning to a search shows the
 * criteria that produced it rather than an empty form beside populated results.
 */
export function StudySearchForm({ initial }: { readonly initial: StudySearchFields }) {
  const t = useTranslations();
  const router = useRouter();

  const nameId = useId();
  const accessionId = useId();
  const fromId = useId();
  const toId = useId();
  const errorId = useId();
  const hintId = useId();

  const [fields, setFields] = useState<StudySearchFields>(initial);
  const [error, setError] = useState<string | null>(null);
  const [isSearching, startSearching] = useTransition();

  function update(field: keyof StudySearchFields, value: string) {
    setFields((current) => ({ ...current, [field]: value }));
  }

  function clear() {
    setFields({ patientName: '', accessionNumber: '', studyDateFrom: '', studyDateTo: '' });
    setError(null);
  }

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const patientName = fields.patientName.trim();
    const accessionNumber = fields.accessionNumber.trim();
    const studyDateFrom = fields.studyDateFrom.trim();
    const studyDateTo = fields.studyDateTo.trim();

    // The API answers 400 for a search with no criteria, and rightly so - it is
    // a request for the archive itself. Catching it here turns a server error
    // into a sentence next to the field the reader has to fill in.
    if (
      patientName.length === 0 &&
      accessionNumber.length === 0 &&
      studyDateFrom.length === 0 &&
      studyDateTo.length === 0
    ) {
      setError(t('search.errorEmpty'));
      return;
    }

    if (studyDateFrom.length > 0 && studyDateTo.length > 0 && studyDateFrom > studyDateTo) {
      // Safe as a string comparison: both are YYYY-MM-DD, which sorts
      // lexicographically in date order.
      setError(t('search.errorDateOrder'));
      return;
    }

    setError(null);

    const parameters = new URLSearchParams();

    if (patientName.length > 0) parameters.set('patientName', patientName);
    if (accessionNumber.length > 0) parameters.set('accessionNumber', accessionNumber);
    if (studyDateFrom.length > 0) parameters.set('studyDateFrom', studyDateFrom);
    if (studyDateTo.length > 0) parameters.set('studyDateTo', studyDateTo);

    // A new search always starts at the first page. Keeping the old page would
    // land the reader on page four of a result set that may have one page.
    parameters.set('page', '0');

    startSearching(() => {
      router.push(`/search?${parameters.toString()}`);
    });
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="flex w-full flex-col gap-[22px]">
      {/*
        Two columns from the sm breakpoint up, one below it. The four criteria
        are equally weighted - none is the primary one - so they get equal
        cells rather than a hierarchy the data does not have. Row and column
        gaps differ on purpose: label and input already carry vertical rhythm
        inside a field, so the grid needs less of it between rows than between
        columns.
      */}
      <div className="grid gap-x-[22px] gap-y-[18px] min-[900px]:grid-cols-2">
        <Field
          id={nameId}
          label={t('search.patientName')}
          hint={t('search.patientNameHint')}
          hintId={hintId}
        >
          <Input
            id={nameId}
            name="patientName"
            value={fields.patientName}
            onChange={(event) => update('patientName', event.target.value)}
            invalid={error !== null}
            aria-describedby={hintId}
            autoComplete="off"
            enterKeyHint="search"
            spellCheck={false}
          />
        </Field>

        <Field id={accessionId} label={t('search.accessionNumber')}>
          <Input
            id={accessionId}
            name="accessionNumber"
            value={fields.accessionNumber}
            onChange={(event) => update('accessionNumber', event.target.value)}
            invalid={error !== null}
            autoComplete="off"
            enterKeyHint="search"
            spellCheck={false}
          />
        </Field>

        <Field id={fromId} label={t('search.dateFrom')}>
          <Input
            id={fromId}
            name="studyDateFrom"
            type="date"
            value={fields.studyDateFrom}
            onChange={(event) => update('studyDateFrom', event.target.value)}
            invalid={error !== null}
          />
        </Field>

        <Field id={toId} label={t('search.dateTo')}>
          <Input
            id={toId}
            name="studyDateTo"
            type="date"
            value={fields.studyDateTo}
            onChange={(event) => update('studyDateTo', event.target.value)}
            invalid={error !== null}
          />
        </Field>
      </div>

      {/*
        One row: the message on the left, the actions on the right. The message
        shares the row rather than occupying one of its own, so the form does
        not grow taller the moment it is wrong - the reader's eye stays where it
        was. It still comes before the buttons in source order, so a screen
        reader reaches it first.
      */}
      <div className="border-atrium-border flex flex-wrap items-center justify-end gap-3 border-t pt-5">
        {error !== null ? (
          <p
            id={errorId}
            role="alert"
            className="mr-auto rounded-[10px] bg-[var(--atrium-danger-bg)] px-3.5 py-2 text-[12.5px] text-[var(--atrium-danger)] shadow-[inset_0_0_0_1px_#b423183d]"
          >
            {error}
          </p>
        ) : null}

        <Button type="button" variant="ghost" disabled={isSearching} onClick={clear}>
          {t('search.clear')}
        </Button>
        <Button type="submit" disabled={isSearching} className="min-w-32">
          {isSearching ? t('search.submitting') : t('search.submit')}
        </Button>
      </div>
    </form>
  );
}

function Field({
  id,
  label,
  hint,
  hintId,
  children,
}: {
  readonly id: string;
  readonly label: string;
  readonly hint?: string;
  readonly hintId?: string;
  readonly children: React.ReactNode;
}) {
  return (
    <div className="flex flex-col gap-2">
      <label htmlFor={id} className="atrium-label text-atrium-ink">
        {label}
      </label>
      {children}
      {hint !== undefined ? (
        <p id={hintId} className="text-atrium-muted text-[11.5px] leading-relaxed">
          {hint}
        </p>
      ) : null}
    </div>
  );
}
