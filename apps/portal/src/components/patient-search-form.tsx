'use client';

import { useId, useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { Button, Input } from '@cloudfilm/ui';

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
      setError('Enter a patient ID to search.');
      return;
    }

    if (!MEDICAL_RECORD_NUMBER.test(medicalRecordNumber)) {
      setError('A patient ID may contain letters, numbers and hyphens only.');
      return;
    }

    setError(null);
    startNavigation(() => {
      router.push(`/patient/${encodeURIComponent(medicalRecordNumber)}`);
    });
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="flex w-full flex-col gap-3">
      <label htmlFor={inputId} className="text-foreground text-sm font-medium">
        Patient ID
      </label>

      <div className="flex flex-col gap-3 sm:flex-row">
        <Input
          id={inputId}
          name="patientId"
          value={value}
          onChange={(event) => setValue(event.target.value)}
          invalid={error !== null}
          aria-describedby={error === null ? hintId : `${errorId} ${hintId}`}
          placeholder="e.g. 10001"
          autoComplete="off"
          enterKeyHint="search"
          spellCheck={false}
          className="sm:flex-1"
        />
        <Button type="submit" disabled={isNavigating} className="sm:w-32">
          {isNavigating ? 'Searching…' : 'Search'}
        </Button>
      </div>

      {error !== null ? (
        <p id={errorId} role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      ) : null}

      <p id={hintId} className="text-muted text-sm">
        Enter the patient&rsquo;s Medical Record Number to open their imaging studies.
      </p>
    </form>
  );
}
