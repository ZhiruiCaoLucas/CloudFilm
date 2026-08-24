import Link from 'next/link';
import { unstable_rethrow } from 'next/navigation';
import { buttonClasses, Card, CardBody, CardHeader } from '@cloudfilm/ui';
import { serverApiClient } from '@/lib/api';
import { PortalShell } from '@/components/portal-shell';
import { serverTranslations } from '@/lib/locale';

/**
 * Lists the record numbers that do exist, so a user is not left guessing.
 *
 * A failure here must not replace a not-found page with an error page, so an
 * unavailable API simply yields no suggestions.
 *
 * `unstable_rethrow` is load-bearing, not decoration. Next implements
 * `redirect()` by throwing, so once the patient endpoints require a token this
 * `catch` would swallow the redirect to sign-in and silently return no
 * suggestions instead. The failure would look like a cosmetic gap rather than
 * broken navigation. This rethrows Next's own control-flow signals and keeps
 * catching only the transport failures the fallback was written for.
 */
async function knownMedicalRecordNumbers(): Promise<readonly string[]> {
  try {
    const client = await serverApiClient();
    const patients = await client.listPatients();
    return patients.map((patient) => patient.medicalRecordNumber);
  } catch (error) {
    unstable_rethrow(error);
    return [];
  }
}

export default async function PatientNotFound() {
  const [knownNumbers, t] = await Promise.all([knownMedicalRecordNumbers(), serverTranslations()]);

  return (
    <PortalShell>
      <div className="flex flex-col gap-8">
        <Card>
          <CardHeader>
            <h1 className="atrium-h2 text-atrium-ink">{t('patient.notFoundTitle')}</h1>
          </CardHeader>

          <CardBody className="flex flex-col gap-4">
            <p className="atrium-body text-atrium-body">{t('patient.notFoundBody')}</p>

            {knownNumbers.length > 0 && (
              <p className="atrium-body text-atrium-body">
                {t('patient.notFoundKnown')}{' '}
                {knownNumbers.map((number) => (
                  <code
                    key={number}
                    className="bg-atrium-panel text-atrium-ink mr-1 rounded px-1.5 py-0.5 font-mono text-xs"
                  >
                    {number}
                  </code>
                ))}
              </p>
            )}

            <div>
              <Link href="/" className={buttonClasses()}>
                {t('patient.backToSearch')}
              </Link>
            </div>
          </CardBody>
        </Card>
      </div>
    </PortalShell>
  );
}
