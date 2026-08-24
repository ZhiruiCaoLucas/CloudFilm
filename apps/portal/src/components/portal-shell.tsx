'use client';

import type { ReactNode } from 'react';
import type { CurrentUser } from '@cloudfilm/shared';
import { TopNav } from './top-nav';
import { SiteFooter } from './site-footer';

/**
 * Chrome for the document-style pages: search, patient detail, standalone viewer.
 *
 * Lives in a component rather than the root layout because the imaging
 * workspace needs the full viewport — no centred column, no page footer, no
 * scrolling. Two shells, applied per page, keeps that difference explicit
 * instead of hiding it behind CSS overrides.
 *
 * `user` is forwarded to the navigation. Omitting it renders the signed-out
 * state, which is what a client error boundary or a loading skeleton must do:
 * neither can read a cookie.
 */
export function PortalShell({
  user,
  wide = false,
  children,
}: {
  readonly user?: CurrentUser | null | undefined;

  /**
   * Opens the page with a band rather than a heading.
   *
   * The home page starts with a full-width hero, which needs less air above it
   * than a page that starts with a breadcrumb and a title. The column measure is
   * the same either way - it has to be, or the navigation's logo and the page's
   * first character would sit on two different left edges.
   */
  readonly wide?: boolean;
  readonly children: ReactNode;
}) {
  return (
    <>
      <TopNav user={user} />
      <main
        id="main"
        className={`mx-auto w-full max-w-[1180px] flex-1 px-6 sm:px-8 ${
          wide ? 'pt-8 pb-[72px]' : 'pt-10 pb-[72px]'
        }`}
      >
        {children}
      </main>
      <SiteFooter />
    </>
  );
}
