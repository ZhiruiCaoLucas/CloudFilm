'use client';

import { Button, Card, CardBody, CardHeader } from '@cloudfilm/ui';
import { PortalShell } from '@/components/portal-shell';
import { useTranslations } from '@/components/locale-provider';

/**
 * Catches unexpected failures while rendering a patient record.
 *
 * Without a boundary these surfaced as Next's development overlay or a blank
 * page in production. Neither is acceptable in a clinical application: the user
 * must be told the record could not be shown, and must never be left unsure
 * whether they are looking at stale data.
 *
 * `digest` is the server-side correlation identifier. It is the only detail
 * shown, because a message or stack can disclose internal hostnames.
 */
export default function PatientError({
  error,
  reset,
}: {
  readonly error: Error & { digest?: string };
  readonly reset: () => void;
}) {
  const t = useTranslations();

  return (
    <PortalShell>
      <Card>
        <CardHeader>
          <h1 className="text-foreground text-lg font-semibold tracking-tight">
            {t('patient.errorTitle')}
          </h1>
        </CardHeader>
        <CardBody className="flex flex-col gap-4">
          <p className="text-muted text-sm">{t('patient.errorBody')}</p>
          {error.digest === undefined ? null : (
            <p className="text-muted text-xs">{t('common.reference', { digest: error.digest })}</p>
          )}
          <div className="flex gap-3">
            <Button onClick={reset}>{t('common.retry')}</Button>
          </div>
        </CardBody>
      </Card>
    </PortalShell>
  );
}
