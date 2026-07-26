'use client';

import type { ReactNode } from 'react';
import type { Study } from '@cloudfilm/shared';
import { useViewerAvailability } from '@/hooks/use-viewer-availability';
import { ViewerPanel } from './viewer-panel';
import { WorkspaceStatusBar } from './workspace-status-bar';

/**
 * Owns viewer availability and shares it with everything that displays it.
 *
 * The reading area and the status bar are not adjacent in the layout, but both
 * need the same answer to "is the viewer up?". Holding that state in their
 * nearest common ancestor keeps one probe and one source of truth.
 *
 * The navigator arrives as `children` so it stays a server component: it is
 * static markup, and there is no reason to ship it to the browser as JavaScript
 * just because its sibling is interactive.
 */
export function ViewerWorkspace({
  study,
  viewerBaseUrl,
  children,
}: {
  readonly study: Study;
  readonly viewerBaseUrl: string;
  readonly children: ReactNode;
}) {
  const { status, retry } = useViewerAvailability(viewerBaseUrl);

  return (
    <>
      <div className="flex min-h-0 flex-1">
        {children}
        <ViewerPanel study={study} viewerBaseUrl={viewerBaseUrl} status={status} onRetry={retry} />
      </div>
      <WorkspaceStatusBar study={study} viewerStatus={status} />
    </>
  );
}
