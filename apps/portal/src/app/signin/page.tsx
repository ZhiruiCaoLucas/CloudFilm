import type { Metadata } from 'next';
import { redirect } from 'next/navigation';
import { CloudFilmLogo } from '@/components/cloudfilm-logo';
import { EnvironmentBadge } from '@/components/environment-badge';
import { LocaleSwitcher } from '@/components/locale-switcher';
import { currentUser } from '@/lib/api';
import { isDevelopment } from '@/lib/runtime';
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
 *
 * The one page in CloudFilm that does not use {@link PortalShell}. The shell's
 * navigation exists to move a signed-in reader between patients and studies, and
 * none of it is reachable here - so it would be chrome around the only control
 * on the page. What the shell does carry that this page still needs is the
 * language switcher and the environment badge, and both are placed deliberately
 * below the form instead: which instance you are signing in to matters, and
 * which language you read in has to be changeable before you have an account
 * session to store a preference against.
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
    <main id="main" className="relative flex flex-1 flex-col overflow-hidden">
      {/*
        Two decorative layers, both inert and both hidden from assistive
        technology. They carry no information; they exist so the form has
        somewhere to sit other than a flat field of white.
      */}
      <div aria-hidden="true" className="auth-glow pointer-events-none absolute inset-0" />
      <div aria-hidden="true" className="auth-backdrop pointer-events-none absolute inset-0" />

      <div className="relative flex flex-1 items-center justify-center px-6 py-14">
        <div className="w-full max-w-[23rem]">
          {/* Identity, at the size a workstation states it: enough to know
              which system you are entering, not a masthead. */}
          <div className="mb-9 flex items-center gap-3">
            <CloudFilmLogo className="h-8 w-8 shrink-0" />
            <span className="flex min-w-0 flex-col leading-tight">
              <span className="text-foreground text-[0.9375rem] font-semibold tracking-tight">
                {t('common.appName')}
              </span>
              <span className="text-muted text-[0.6875rem] tracking-[0.09em] uppercase">
                {t('common.tagline')}
              </span>
            </span>
          </div>

          <section className="border-border bg-surface-raised rounded-lg border p-7 shadow-[0_1px_2px_rgba(15,23,42,0.04),0_8px_24px_-16px_rgba(15,23,42,0.18)] dark:shadow-[0_1px_2px_rgba(0,0,0,0.4),0_8px_24px_-16px_rgba(0,0,0,0.7)]">
            <h1 className="text-foreground text-base font-semibold tracking-tight">
              {t('auth.title')}
            </h1>

            {/* The system descriptor, taken from the product's own one-line
                description rather than written for this page. */}
            <p className="text-muted mt-1.5 text-[0.8125rem] leading-relaxed">
              {t('common.description')}
            </p>

            <hr className="border-border my-6" />

            <SignInForm returnUrl={destination} />
          </section>

          <p className="text-muted mt-5 px-1 text-xs leading-relaxed">{t('auth.body')}</p>

          <div className="border-border mt-8 flex items-center justify-between gap-3 border-t pt-5">
            <LocaleSwitcher />
            <EnvironmentBadge
              isProduction={!isDevelopment}
              label={t(isDevelopment ? 'nav.environmentDevelopment' : 'nav.environmentProduction')}
            />
          </div>
        </div>
      </div>
    </main>
  );
}
