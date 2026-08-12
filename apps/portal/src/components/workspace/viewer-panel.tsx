'use client';

import { buildViewerUrl } from '@cloudfilm/core';
import type { Study } from '@cloudfilm/shared';
import { ViewerNoData } from './viewer-no-data';
import { useTranslations } from '@/components/locale-provider';

/**
 * The reading area.
 *
 * Always black regardless of theme: diagnostic images are read on a black
 * surround so that surrounding brightness does not bias perceived contrast.
 * That is a viewing condition, not a colour scheme preference.
 *
 * The iframe mounts whenever a viewer address is configured, and loading it is
 * the iframe's own responsibility.
 *
 * It used to be gated on a reachability probe, which was wrong in a way that
 * could only ever fail closed: the probe is a cross-origin `no-cors` fetch, and
 * OHIF's nginx returns `Cross-Origin-Resource-Policy: same-origin`, so the
 * browser blocks it. That header governs subresource loads; it does not apply to
 * frame navigation. The probe therefore tested a strictly stricter condition
 * than the iframe needs, and reported a working viewer as offline. Viewer
 * reachability is still probed for the status bar, where being wrong costs a
 * misleading label rather than a blank reading area.
 */
export function ViewerPanel({
  study,
  viewerBaseUrl,
  isStudyImported,
}: {
  readonly study: Study;
  readonly viewerBaseUrl: string;
  readonly isStudyImported: boolean;
}) {
  const t = useTranslations();

  return (
    <section
      aria-label={t('viewer.label')}
      className="relative flex min-w-0 flex-1 flex-col bg-black"
    >
      {isStudyImported ? (
        <iframe
          title={t('viewer.title', { description: study.description })}
          src={buildViewerUrl({ viewerBaseUrl, studyInstanceUid: study.studyInstanceUid })}
          className="h-full w-full border-0"
          allow="fullscreen"
          sandbox="allow-scripts allow-same-origin allow-forms allow-downloads"
        />
      ) : (
        <div className="flex h-full w-full items-center justify-center">
          <ViewerNoData studyInstanceUid={study.studyInstanceUid} />
        </div>
      )}
    </section>
  );
}
