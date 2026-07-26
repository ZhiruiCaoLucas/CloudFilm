import type { ReactNode } from 'react';
import { TopNav } from './top-nav';
import { SiteFooter } from './site-footer';

/**
 * Chrome for the document-style pages: search, patient detail, standalone viewer.
 *
 * Lives in a component rather than the root layout because the imaging
 * workspace needs the full viewport — no centred column, no page footer, no
 * scrolling. Two shells, applied per page, keeps that difference explicit
 * instead of hiding it behind CSS overrides.
 */
export function PortalShell({ children }: { readonly children: ReactNode }) {
  return (
    <>
      <TopNav />
      <main id="main" className="mx-auto w-full max-w-5xl flex-1 px-6 py-10">
        {children}
      </main>
      <SiteFooter />
    </>
  );
}
