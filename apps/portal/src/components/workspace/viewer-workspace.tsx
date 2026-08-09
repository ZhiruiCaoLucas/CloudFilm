'use client';

import type { ReactNode } from 'react';
import type { Patient, Series, Study } from '@cloudfilm/shared';
import { useStudyShortcuts } from '@/hooks/use-study-shortcuts';
import { StudyMetadataPanel } from './study-metadata-panel';
import { ViewerPanel } from './viewer-panel';
import { WorkspaceStatusBar } from './workspace-status-bar';

/**
 * Composes the reading area: navigator, viewer and metadata, over a status bar.
 *
 * The client boundary exists for the keyboard shortcuts. Loading the viewer is
 * the iframe's own responsibility - the portal does not probe it first, because
 * the only probe available to it is blocked by a Cross-Origin-Resource-Policy
 * header and so could never succeed.
 *
 * The navigator arrives as `children` so it stays a server component: it is
 * static markup, and there is no reason to ship it to the browser as JavaScript
 * just because its sibling is interactive.
 */
export function ViewerWorkspace({
  patient,
  study,
  studies,
  series,
  viewerBaseUrl,
  isStudyImported,
  children,
}: {
  readonly patient: Patient;
  readonly study: Study;
  readonly studies: readonly Study[];
  readonly series: readonly Series[];
  readonly viewerBaseUrl: string;
  readonly isStudyImported: boolean;
  readonly children: ReactNode;
}) {
  useStudyShortcuts(studies, study.studyInstanceUid);

  return (
    <>
      <div className="flex min-h-0 flex-1">
        {children}
        <ViewerPanel
          study={study}
          viewerBaseUrl={viewerBaseUrl}
          isStudyImported={isStudyImported}
        />
        <StudyMetadataPanel patient={patient} study={study} series={series} />
      </div>
      <WorkspaceStatusBar study={study} />
    </>
  );
}
