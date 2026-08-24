'use client';

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
 * The frame does not point at the viewer directly. It points at a portal route
 * that obtains a viewer session for this study, sets the cookie the viewer's
 * proxy checks, and redirects on. The viewer's address is still built by
 * `buildViewerUrl`, on the server, in that route.
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
  isStudyImported,
}: {
  readonly study: Study;
  readonly isStudyImported: boolean;
}) {
  const t = useTranslations();

  return (
    <section
      aria-label={t('viewer.label')}
      // The viewport ground and nothing else. No dot field, no gradient, no
      // atmospheric mark may appear inside or behind the image.
      //
      // Inset from the column edges rather than bled to them: the image now sits
      // in a framed aperture with a hairline of its own, which is what separates
      // "the image" from "the chrome around it" on a single dark ground. The
      // iframe still fills whatever it is given, so the viewer's own layout is
      // untouched.
      className="relative flex min-h-0 min-w-0 flex-1 flex-col p-3 xl:p-4"
    >
      <div className="min-h-0 flex-1 overflow-hidden rounded-lg bg-black ring-1 ring-white/8">
        {isStudyImported ? (
          <iframe
            title={t('viewer.title', { description: study.description })}
            src={`/viewer-session/${encodeURIComponent(study.studyInstanceUid)}`}
            className="h-full w-full border-0"
            allow="fullscreen"
            sandbox="allow-scripts allow-same-origin allow-forms allow-downloads"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center">
            <ViewerNoData studyInstanceUid={study.studyInstanceUid} />
          </div>
        )}
      </div>
    </section>
  );
}
