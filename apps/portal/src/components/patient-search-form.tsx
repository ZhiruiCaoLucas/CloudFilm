'use client';

import { useId, useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { Button, Input } from '@cloudfilm/ui';
import { useTranslations } from './locale-provider';

/**
 * Accepted Medical Record Number shape.
 *
 * Permissive on purpose: hospitals issue alphanumeric MRNs, often with leading
 * zeros or a site prefix. The field rejects only what could not be an
 * identifier, and leaves real validation to the system that owns the number.
 */
const MEDICAL_RECORD_NUMBER = /^[A-Za-z0-9][A-Za-z0-9-]{0,31}$/;

/**
 * Patient lookup.
 *
 * A client component because it owns transient input state and navigates on
 * submit. It is a real `<form>`, so Enter submits and the flow works for
 * keyboard and screen-reader users without any extra handling.
 */
export function PatientSearchForm() {
  const t = useTranslations();
  const inputId = useId();
  const hintId = useId();
  const errorId = useId();

  const router = useRouter();
  const [value, setValue] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isNavigating, startNavigation] = useTransition();

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const medicalRecordNumber = value.trim();

    if (medicalRecordNumber.length === 0) {
      setError(t('home.errorEmpty'));
      return;
    }

    if (!MEDICAL_RECORD_NUMBER.test(medicalRecordNumber)) {
      setError(t('home.errorFormat'));
      return;
    }

    setError(null);
    startNavigation(() => {
      router.push(`/patient/${encodeURIComponent(medicalRecordNumber)}`);
    });
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="flex w-full flex-col gap-3.5">
      {/* The label is visually hidden here only: the console section around
          this form already carries the same string as its heading, so showing
          it twice would repeat the words on screen. Screen readers still get a
          properly associated label. */}
      <label htmlFor={inputId} className="sr-only">
        {t('home.searchLabel')}
      </label>

      <div className="flex flex-col gap-3 sm:flex-row sm:gap-[14px]">
        <Input
          id={inputId}
          name="patientId"
          inputSize="lg"
          value={value}
          onChange={(event) => setValue(event.target.value)}
          invalid={error !== null}
          aria-describedby={error === null ? hintId : `${errorId} ${hintId}`}
          placeholder={t('home.searchPlaceholder')}
          autoComplete="off"
          enterKeyHint="search"
          spellCheck={false}
          className="font-mono tracking-[0.01em] sm:flex-1"
        />
        {/* A fixed width rather than a share of the row: the label changes
            length while a search is running, and a button that resizes under
            the pointer mid-submit is a button that moves away from it. */}
        <Button type="submit" size="xl" disabled={isNavigating} className="sm:w-[168px]">
          {isNavigating ? t('home.searchSubmitting') : t('home.searchSubmit')}
        </Button>
      </div>

      {error !== null ? (
        <p
          id={errorId}
          role="alert"
          className="rounded-[10px] bg-[var(--atrium-danger-bg)] px-3.5 py-2.5 text-[12.5px] text-[var(--atrium-danger)] shadow-[inset_0_0_0_1px_#b423183d]"
        >
          {error}
        </p>
      ) : null}

      <p id={hintId} className="text-atrium-muted mt-1 max-w-[46ch] text-[12.5px] leading-relaxed">
        {t('home.searchHint')}
      </p>
    </form>
  );
}
