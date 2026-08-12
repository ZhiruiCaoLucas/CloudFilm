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
  children,
}: {
  readonly user?: CurrentUser | null | undefined;
  readonly children: ReactNode;
}) {
  return (
    <>
      <TopNav user={user} />
      <main id="main" className="mx-auto w-full max-w-5xl flex-1 px-6 py-10">
        {children}
      </main>
      <SiteFooter />
    </>
  );
}
