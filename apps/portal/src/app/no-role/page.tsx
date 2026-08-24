import type { Metadata } from 'next';
import Link from 'next/link';
import { Button, Card, CardBody, CardHeader } from '@cloudfilm/ui';
import { PortalShell } from '@/components/portal-shell';
import { signOut } from '@/app/signin/actions';
import { currentUser } from '@/lib/api';
import { serverTranslations } from '@/lib/locale';

/**
 * Generic, like every other title in the portal.
 *
 * There is nothing account-specific to put here even if it were safe to: the
 * page says the same thing to everybody who reaches it.
 */
export async function generateMetadata(): Promise<Metadata> {
  const t = await serverTranslations();

  return { title: t('noRole.pageTitle') };
}

/**
 * Where a signed-in account with no application role is sent.
 *
 * Reached when the API answers 403 - authenticated, but holding none of the
 * roles CloudFilm recognises. That is a real and permanent state until an
 * administrator changes it, so it gets a page that says so rather than the
 * error card a caller would otherwise see, which would suggest a fault.
 *
 * The page offers no way to obtain a role, and there is nothing here that could
 * be made to grant one. Roles are assigned server-side; this only explains the
 * refusal the server already made.
 *
 * It reads the current user for the navigation only. That call is `/auth/me`,
 * which requires authentication and no role, so this page cannot be refused for
 * the same reason that brought the reader to it - which would be a redirect
 * loop.
 */
export default async function NoRolePage() {
  const [user, t] = await Promise.all([currentUser(), serverTranslations()]);

  return (
    <PortalShell user={user}>
      <div className="flex flex-col gap-6 py-8">
        <Card className="mx-auto w-full max-w-xl">
          <CardHeader>
            <h1 className="atrium-h2 text-atrium-ink">{t('noRole.title')}</h1>
          </CardHeader>

          <CardBody className="flex flex-col gap-4">
            <p className="text-atrium-ink text-sm">{t('noRole.body')}</p>
            <p className="atrium-body text-atrium-body">{t('noRole.detail')}</p>

            <div className="flex flex-wrap items-center gap-3 pt-2">
              {/*
                A form, not a link: signing out changes state, so it must not be
                reachable by a prefetch or a crawler. Same action the navigation
                uses - there is no second sign-out path to keep in step.
              */}
              <form action={signOut}>
                <Button type="submit" variant="secondary">
                  {t('nav.signOut')}
                </Button>
              </form>

              <Link
                href="/"
                className="text-atrium-muted hover:text-atrium-ink focus-visible:outline-atrium-primary rounded-md text-sm focus-visible:outline-2 focus-visible:outline-offset-4"
              >
                {t('noRole.home')}
              </Link>
            </div>
          </CardBody>
        </Card>
      </div>
    </PortalShell>
  );
}
