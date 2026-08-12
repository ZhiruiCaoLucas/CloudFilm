import type { Metadata } from 'next';
import { redirect } from 'next/navigation';
import { Card, CardBody, CardHeader } from '@cloudfilm/ui';
import { PortalShell } from '@/components/portal-shell';
import { currentUser } from '@/lib/api';
import { toInternalPath } from '@/lib/session';
import { serverTranslations } from '@/lib/locale';
import { SignInForm } from './signin-form';

export async function generateMetadata(): Promise<Metadata> {
  const t = await serverTranslations();

  return { title: t('auth.pageTitle') };
}

interface SignInPageProps {
  readonly searchParams: Promise<{ readonly returnUrl?: string }>;
}

/**
 * Sign-in.
 *
 * Rendered on the server so that an already-signed-in user is bounced straight
 * back to where they were going, rather than being shown a form they do not
 * need and would have to fill in again.
 */
export default async function SignInPage({ searchParams }: SignInPageProps) {
  const { returnUrl } = await searchParams;
  const destination = toInternalPath(returnUrl);

  // Tolerant on purpose: an unreachable API must still render the form.
  const [user, t] = await Promise.all([currentUser(), serverTranslations()]);

  if (user !== null) {
    redirect(destination);
  }

  return (
    <PortalShell>
      <div className="mx-auto w-full max-w-sm">
        <Card>
          <CardHeader>
            <h1 className="text-foreground text-lg font-semibold tracking-tight">
              {t('auth.title')}
            </h1>
          </CardHeader>
          <CardBody className="flex flex-col gap-6">
            <p className="text-muted text-sm">{t('auth.body')}</p>
            <SignInForm returnUrl={destination} />
          </CardBody>
        </Card>
      </div>
    </PortalShell>
  );
}
