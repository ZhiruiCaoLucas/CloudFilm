import Link from 'next/link';
import { KNOWN_MEDICAL_RECORD_NUMBERS } from '@cloudfilm/shared/mock';
import { Card, CardBody, CardHeader } from '@cloudfilm/ui';

/**
 * Shown when no patient matches the searched Medical Record Number.
 *
 * A bare 404 would be indistinguishable from a broken link. Until the API
 * exists, the page also names the record numbers the fixtures actually contain,
 * so a developer is never left guessing what to type.
 */
export default function PatientNotFound() {
  return (
    <div className="flex flex-col gap-8">
      <Card>
        <CardHeader>
          <h1 className="text-foreground text-xl font-semibold tracking-tight">
            No matching patient
          </h1>
        </CardHeader>
        <CardBody className="flex flex-col gap-4">
          <p className="text-muted text-sm">
            No patient with that Medical Record Number was found. Check the number and search again.
          </p>
          <p className="text-muted text-sm">
            CloudFilm is not yet connected to a hospital archive. Records available in this build:{' '}
            {KNOWN_MEDICAL_RECORD_NUMBERS.map((number) => (
              <code
                key={number}
                className="bg-surface-overlay text-foreground mr-1 rounded px-1.5 py-0.5 font-mono text-xs"
              >
                {number}
              </code>
            ))}
          </p>
          <div>
            <Link
              href="/"
              className="bg-accent text-accent-foreground hover:bg-accent/90 focus-visible:outline-accent inline-flex min-h-11 items-center justify-center rounded-md px-4 text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2"
            >
              Back to patient search
            </Link>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
