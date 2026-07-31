'use client';

import type { ReactNode } from 'react';
import type { Patient, Study } from '@cloudfilm/shared';
import { useStudyShortcuts } from '@/hooks/use-study-shortcuts';
import { useViewerAvailability } from '@/hooks/use-viewer-availability';
import { StudyMetadataPanel } from './study-metadata-panel';
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
  patient,
  study,
  studies,
  viewerBaseUrl,
  isStudyImported,
  children,
}: {
  readonly patient: Patient;
  readonly study: Study;
  readonly studies: readonly Study[];
  readonly viewerBaseUrl: string;
  readonly isStudyImported: boolean;
  readonly children: ReactNode;
}) {
  const { status, retry } = useViewerAvailability(viewerBaseUrl);
  useStudyShortcuts(studies, study.studyInstanceUid);

  return (
    <>
      <div className="flex min-h-0 flex-1">
        {children}
        <ViewerPanel
          study={study}
          viewerBaseUrl={viewerBaseUrl}
          status={status}
          isStudyImported={isStudyImported}
          onRetry={retry}
        />
        <StudyMetadataPanel patient={patient} study={study} />
      </div>
      <WorkspaceStatusBar study={study} viewerStatus={status} />
    </>
  );
}
