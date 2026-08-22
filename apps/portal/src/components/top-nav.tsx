'use client';

import Link from 'next/link';
import type { CurrentUser } from '@cloudfilm/shared';
import { Button, buttonClasses } from '@cloudfilm/ui';
import { CloudFilmLogo } from './cloudfilm-logo';
import { EnvironmentBadge } from './environment-badge';
import { LocaleSwitcher } from './locale-switcher';
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
 */
export function TopNav({ user }: { readonly user?: CurrentUser | null | undefined }) {
  const t = useTranslations();

  return (
    <header className="border-border bg-surface-raised border-b">
      <div className="mx-auto flex h-14 w-full max-w-5xl items-center justify-between gap-4 px-6">
        <Link
          href="/"
          className="focus-visible:outline-accent flex items-center gap-3 rounded-md focus-visible:outline-2 focus-visible:outline-offset-4"
        >
          <CloudFilmLogo className="h-7 w-7 shrink-0" />
          <span className="flex flex-col leading-tight">
            <span className="text-foreground text-[0.8125rem] font-semibold tracking-tight">
              {t('common.appName')}
            </span>
            <span className="text-muted text-[0.625rem] tracking-[0.08em] uppercase">
              {t('common.tagline')}
            </span>
          </span>
        </Link>

        <div className="flex items-center gap-3">
          <LocaleSwitcher />

          <EnvironmentBadge
            isProduction={!isDevelopment}
            label={t(isDevelopment ? 'nav.environmentDevelopment' : 'nav.environmentProduction')}
          />

          {user === null || user === undefined ? (
            <Link href="/signin" className={buttonClasses('secondary', 'min-h-9 px-3 text-xs')}>
              {t('nav.signIn')}
            </Link>
          ) : (
            <div className="flex items-center gap-3">
              <span className="hidden flex-col items-end leading-tight sm:flex">
                <span className="text-foreground text-xs font-medium">{user.displayName}</span>
                <span className="text-muted text-[0.6875rem]">{user.email}</span>
              </span>

              {/* A form, not a link: signing out changes state, so it must not
                  be reachable by a prefetch or a crawler. */}
              <form action={signOut}>
                <Button type="submit" variant="ghost" className="min-h-9 px-3 text-xs">
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
