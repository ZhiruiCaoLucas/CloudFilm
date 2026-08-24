import type { Metadata } from 'next';
import Image from 'next/image';
import { redirect } from 'next/navigation';
import { CrossOutline, DotField, EcgHairline } from '@cloudfilm/ui';
import { CloudFilmLogo } from '@/components/cloudfilm-logo';
import { EnvironmentBadge } from '@/components/environment-badge';
import { LocaleSwitcher } from '@/components/locale-switcher';
import { ThemeSwitcher } from '@/components/theme-switcher';
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
 * on the page. What the shell carries that this page still needs is the language
 * switcher and the environment badge, and both sit under the form: which
 * instance you are signing in to matters, and which language you read in has to
 * be changeable before there is a session to store a preference against.
 *
 * Atrium gives it two panes. The left is identity and assurance on a deep blue
 * ground; the right is the form and nothing else. Below the `lg` breakpoint the
 * left pane is dropped entirely rather than stacked - it carries no information
 * a person needs in order to sign in, and a phone should show the form.
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
    <main id="main" className="flex flex-1 flex-col lg:flex-row">
      {/* ---------------------------------------------------------------- */}
      {/* Identity pane. Decorative throughout, and hidden below lg.        */}
      {/* ---------------------------------------------------------------- */}
      <section className="atrium-scrim relative hidden shrink-0 overflow-hidden lg:flex lg:w-[46%] lg:max-w-[812px] lg:flex-col lg:justify-between lg:p-14">
        {/*
          The building, filling the pane and dissolved into the blue ground at
          its bottom and inner edges - the same masking language as the home
          hero, turned ninety degrees. Inert and hidden from assistive
          technology: it carries no information a person needs in order to sign
          in.
        */}
        <div aria-hidden="true" className="atrium-auth-photo pointer-events-none absolute inset-0">
          <Image
            src="/images/hospital-exterior.jpg"
            alt=""
            fill
            sizes="(min-width: 1024px) 46vw, 0px"
            priority
            className="object-cover object-[58%_30%]"
          />
        </div>
        <div aria-hidden="true" className="atrium-auth-tint pointer-events-none absolute inset-0" />

        <DotField className="opacity-25 mix-blend-screen" />

        {/*
          The cross crops into the picture from the outer edge rather than
          floating in the corner, so it reads against the building's own lines.
        */}
        <CrossOutline size={380} className="absolute top-[22%] -right-24 text-white/12" />

        <div className="atrium-enter-1 relative flex items-center gap-3">
          <CloudFilmLogo className="h-10 w-10 shrink-0" />
          <span className="text-[17px] font-semibold tracking-[-0.02em] text-white">
            {t('common.appName')}
          </span>
        </div>

        <div className="atrium-enter-2 relative max-w-[30rem]">
          <span className="atrium-eyebrow text-[var(--atrium-sky-300)]">{t('common.tagline')}</span>
          <h2 className="atrium-h1 mt-5 text-white">{t('auth.heroTitle')}</h2>
          <EcgHairline width={300} height={22} className="mt-6 text-white/40" />
          <p className="atrium-body mt-5 text-white/80">{t('common.description')}</p>

          <ul className="mt-9 flex flex-wrap gap-3">
            {[t('auth.trustCompliance'), t('auth.trustAccess')].map((claim) => (
              <li
                key={claim}
                className="cf-control inline-flex items-center gap-2 rounded-lg bg-white/12 px-3.5 py-2 text-[12px] font-medium text-white/90 shadow-[inset_0_0_0_1px_rgba(255,255,255,0.18)] backdrop-blur-[2px]"
              >
                <span
                  aria-hidden="true"
                  className="h-1.5 w-1.5 rounded-full bg-[var(--atrium-sky-300)]"
                />
                {claim}
              </li>
            ))}
          </ul>
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/* Form pane.                                                        */}
      {/* ---------------------------------------------------------------- */}
      <section className="bg-atrium-surface relative flex flex-1 items-center justify-center px-6 py-14 sm:px-10">
        <div className="atrium-enter-1 w-full max-w-[25rem]">
          {/* The mark repeats here for the narrow layout, where the identity
              pane is not rendered at all. */}
          <div className="mb-9 flex items-center gap-3 lg:hidden">
            <CloudFilmLogo className="h-10 w-10 shrink-0" />
            <span className="text-atrium-ink text-[17px] font-semibold tracking-[-0.02em]">
              {t('common.appName')}
            </span>
          </div>

          <h1 className="atrium-h2 text-atrium-ink">{t('auth.title')}</h1>
          <p className="atrium-body text-atrium-body mt-2">{t('common.description')}</p>

          <div className="mt-9">
            <SignInForm returnUrl={destination} />
          </div>

          <p className="text-atrium-muted mt-6 text-[12.5px] leading-relaxed">{t('auth.body')}</p>

          <div className="border-atrium-border mt-10 flex items-center justify-between gap-3 border-t pt-6">
            <div className="flex items-center gap-3">
              <LocaleSwitcher />
              <ThemeSwitcher />
            </div>
            <EnvironmentBadge
              isProduction={!isDevelopment}
              label={t(isDevelopment ? 'nav.environmentDevelopment' : 'nav.environmentProduction')}
            />
          </div>
        </div>
      </section>
    </main>
  );
}
