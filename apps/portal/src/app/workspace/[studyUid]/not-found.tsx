import Link from 'next/link';
import { buttonClasses, Card, CardBody, CardHeader } from '@cloudfilm/ui';
import { serverTranslations } from '@/lib/locale';

/**
 * Shown when no study matches the requested Study Instance UID.
 *
 * Rendered inside the workspace's own full-viewport shell rather than the portal
 * chrome, so a bad study link does not drop the user into a differently shaped
 * page than the one they were navigating to.
 */
export default async function WorkspaceNotFound() {
  const t = await serverTranslations();

  return (
    <div id="main" className="flex h-dvh items-center justify-center px-6">
      <Card className="max-w-lg">
        <CardHeader>
          <h1 className="atrium-h2 text-atrium-ink">{t('workspace.notFoundTitle')}</h1>
        </CardHeader>
        <CardBody className="flex flex-col gap-4">
          <p className="atrium-body text-atrium-body">{t('workspace.notFoundBody')}</p>
          <div>
            <Link href="/" className={buttonClasses()}>
              {t('patient.backToSearch')}
            </Link>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
