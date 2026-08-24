'use client';

import Link from 'next/link';
import { Button, buttonClasses, Card, CardBody, CardHeader } from '@cloudfilm/ui';
import { useTranslations } from '@/components/locale-provider';

/**
 * Catches unexpected failures while rendering the workspace.
 *
 * Rendered in the workstation's own full-viewport shell rather than the portal
 * chrome, so a failure does not drop the user into a differently shaped page
 * than the one they were navigating to.
 */
export default function WorkspaceError({
  error,
  reset,
}: {
  readonly error: Error & { digest?: string };
  readonly reset: () => void;
}) {
  const t = useTranslations();

  return (
    <div className="flex h-dvh items-center justify-center px-6">
      <Card className="max-w-lg">
        <CardHeader>
          <h1 className="atrium-h2 text-atrium-ink">{t('workspace.errorTitle')}</h1>
        </CardHeader>
        <CardBody className="flex flex-col gap-4">
          <p className="atrium-body text-atrium-body">{t('workspace.errorBody')}</p>
          {error.digest === undefined ? null : (
            <p className="text-atrium-muted text-xs">
              {t('common.reference', { digest: error.digest })}
            </p>
          )}
          <div className="flex gap-3">
            <Button onClick={reset}>{t('common.retry')}</Button>
            <Link href="/" className={buttonClasses('secondary')}>
              {t('workspace.patientSearch')}
            </Link>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
