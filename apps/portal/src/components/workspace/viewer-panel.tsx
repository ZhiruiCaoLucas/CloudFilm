import { buildViewerUrl } from '@cloudfilm/core';
import type { Study } from '@cloudfilm/shared';
import type { ViewerAvailability } from '@/hooks/use-viewer-availability';
import { ViewerConnecting } from './viewer-connecting';
import { ViewerNoData } from './viewer-no-data';
import { ViewerOffline } from './viewer-offline';

/**
 * The reading area.
 *
 * Always black regardless of theme: diagnostic images are read on a black
 * surround so that surrounding brightness does not bias perceived contrast.
 * That is a viewing condition, not a colour scheme preference.
 *
 * The iframe is mounted only once the viewer is known to be reachable, so the
 * browser's own connection-error page can never appear here.
 */
export function ViewerPanel({
  study,
  viewerBaseUrl,
  status,
  isStudyImported,
  onRetry,
}: {
  readonly study: Study;
  readonly viewerBaseUrl: string;
  readonly status: ViewerAvailability;
  readonly isStudyImported: boolean;
  readonly onRetry: () => void;
}) {
  return (
    <section aria-label="Image viewer" className="relative flex min-w-0 flex-1 flex-col bg-black">
      {!isStudyImported ? (
        <div className="flex h-full w-full items-center justify-center">
          <ViewerNoData studyInstanceUid={study.studyInstanceUid} />
        </div>
      ) : status === 'online' ? (
        <iframe
          title={`OHIF Viewer — ${study.description}`}
          src={buildViewerUrl({ viewerBaseUrl, studyInstanceUid: study.studyInstanceUid })}
          className="h-full w-full border-0"
          allow="fullscreen"
          sandbox="allow-scripts allow-same-origin allow-forms allow-downloads"
        />
      ) : (
        <div className="flex h-full w-full items-center justify-center">
          {status === 'connecting' ? <ViewerConnecting /> : <ViewerOffline onRetry={onRetry} />}
        </div>
      )}
    </section>
  );
}
