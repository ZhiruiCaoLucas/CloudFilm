'use client';

import { buildInfo } from '@/lib/build-info';
import { useTranslations } from './locale-provider';

/**
 * Build provenance, so a support call can start with "which version are you on?".
 *
 * A Client Component for the same reason as {@link TopNav}: `PortalShell`
 * renders it inside client error boundaries as well as inside server pages.
 *
 * The values themselves - version, commit, build time - are never translated.
 */
export function SiteFooter() {
  const t = useTranslations();

  return (
    <footer className="border-border mt-20 border-t">
      <div className="text-muted mx-auto flex w-full max-w-5xl flex-col gap-2 px-6 py-5 text-[0.6875rem] sm:flex-row sm:items-center sm:justify-between">
        <span className="text-foreground font-medium">{t('common.appName')}</span>
        <dl className="flex flex-wrap items-center gap-x-6 gap-y-1">
          <div className="flex items-center gap-1.5">
            <dt>{t('common.version')}</dt>
            <dd className="font-mono">{buildInfo.version}</dd>
          </div>
          <div className="flex items-center gap-1.5">
            <dt>{t('common.build')}</dt>
            <dd className="font-mono">{buildInfo.commit}</dd>
          </div>
          <div className="flex items-center gap-1.5">
            <dt className="sr-only">{t('common.built')}</dt>
            <dd>{buildInfo.builtAt}</dd>
          </div>
        </dl>
      </div>
    </footer>
  );
}
