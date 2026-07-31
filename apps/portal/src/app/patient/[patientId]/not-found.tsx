import Link from 'next/link';
import { buttonClasses, Card, CardBody, CardHeader } from '@cloudfilm/ui';
import { serverApiClient } from '@/lib/api';
import { PortalShell } from '@/components/portal-shell';

/**
 * Shown when no patient matches the searched Medical Record Number.
 *
 * A bare 404 would be indistinguishable from a broken link. Until the API
 * exists, the page also names the record numbers the fixtures actually contain,
 * so a developer is never left guessing what to type.
 */
/**
 * Lists the record numbers that do exist, so a user is not left guessing.
 *
 * A failure here must not replace a not-found page with an error page, so an
 * unavailable API simply yields no suggestions.
 */
async function knownMedicalRecordNumbers(): Promise<readonly string[]> {
  try {
    const patients = await serverApiClient().listPatients();
    return patients.map((patient) => patient.medicalRecordNumber);
  } catch {
    return [];
  }
}

export default async function PatientNotFound() {
  const knownNumbers = await knownMedicalRecordNumbers();

  return (
    <PortalShell>
      <div className="flex flex-col gap-8">
        <Card>
          <CardHeader>
            <h1 className="text-foreground text-xl font-semibold tracking-tight">
              No matching patient
            </h1>
          </CardHeader>
          <CardBody className="flex flex-col gap-4">
            <p className="text-muted text-sm">
              No patient with that Medical Record Number was found. Check the number and search
              again.
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
              <Link href="/" className={buttonClasses()}>
                Back to patient search
              </Link>
            </div>
          </CardBody>
        </Card>
      </div>
    </PortalShell>
  );
}
