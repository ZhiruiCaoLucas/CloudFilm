'use client';

import Link from 'next/link';
import type { CurrentUser } from '@cloudfilm/shared';
import { Button } from '@cloudfilm/ui';
import { CloudFilmLogo } from './cloudfilm-logo';
import { EnvironmentBadge } from './environment-badge';
import { LocaleSwitcher } from './locale-switcher';
import { ThemeSwitcher } from './theme-switcher';
import { useTranslations } from './locale-provider';
import { signOut } from '@/app/signin/actions';
import { isDevelopment } from '@/lib/runtime';

/**
 * Application chrome: identity of the system, which instance of it, who is
 * signed in, and which language the UI is in.
 *
 * A Client Component, because it is rendered in both trees: server pages render
 * it through `PortalShell`, and so do the `error.tsx` boundaries, which are
 * Client Components. A component on both sides of that boundary can use neither
 * `await serverTranslations()` nor a hook unless it picks a side, so it picks
 * the client and reads the locale from `LocaleProvider`.
 *
 * `user` stays a prop for the same reason: reading a cookie is server work.
 *
 * Atrium sets this bar at 70px on white, closed by one hairline, with the
 * dividers drawn as their own elements rather than as borders on flex children -
 * a divider that belongs to a neighbour disappears the moment that neighbour is
 * conditionally rendered.
 */
export function TopNav({ user }: { readonly user?: CurrentUser | null | undefined }) {
  const t = useTranslations();

  return (
    <header className="border-atrium-border bg-atrium-white sticky top-0 z-30 border-b">
      <div className="mx-auto flex h-[70px] w-full max-w-[1180px] items-center justify-between gap-6 px-8">
        <div className="flex min-w-0 items-center gap-[18px]">
          <Link
            href="/"
            className="focus-visible:outline-atrium-primary group/mark flex shrink-0 items-center gap-3 rounded-lg focus-visible:outline-2 focus-visible:outline-offset-4"
          >
            <CloudFilmLogo className="h-9 w-9 shrink-0 transition-transform duration-[180ms] group-hover/mark:scale-[1.04]" />
            <span className="text-atrium-ink text-[16px] font-semibold tracking-[-0.02em]">
              {t('common.appName')}
            </span>
          </Link>

          <span aria-hidden="true" className="bg-atrium-border h-7 w-px shrink-0" />

          <span className="atrium-eyebrow text-atrium-muted hidden truncate sm:block">
            {t('common.tagline')}
          </span>
        </div>

        <div className="flex shrink-0 items-center gap-[18px]">
          <LocaleSwitcher />
          <ThemeSwitcher />

          <span aria-hidden="true" className="bg-atrium-border hidden h-7 w-px sm:block" />

          <EnvironmentBadge
            isProduction={!isDevelopment}
            label={t(isDevelopment ? 'nav.environmentDevelopment' : 'nav.environmentProduction')}
          />

          {user === null || user === undefined ? (
            <Link
              href="/signin"
              className="text-atrium-primary hover:text-atrium-deep focus-visible:outline-atrium-primary rounded text-[13px] font-semibold underline decoration-[var(--atrium-sky-300)] decoration-2 underline-offset-4 transition-colors focus-visible:outline-2 focus-visible:outline-offset-4"
            >
              {t('nav.signIn')}
            </Link>
          ) : (
            <div className="flex items-center gap-[18px]">
              <span className="hidden flex-col items-end leading-tight sm:flex">
                <span className="text-atrium-ink text-[13px] font-semibold">
                  {user.displayName}
                </span>
                <span className="text-atrium-muted text-[11.5px]">{user.email}</span>
              </span>

              {/* A form, not a link: signing out changes state, so it must not
                  be reachable by a prefetch or a crawler. */}
              <form action={signOut}>
                <Button
                  type="submit"
                  variant="ghost"
                  size="sm"
                  className="text-atrium-primary min-h-0 px-0 font-semibold underline decoration-[var(--atrium-sky-300)] decoration-2 underline-offset-4 hover:bg-transparent"
                >
                  {t('nav.signOut')}
                </Button>
              </form>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
