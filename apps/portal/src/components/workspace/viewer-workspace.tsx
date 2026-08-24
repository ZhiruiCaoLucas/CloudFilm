'use client';

import type { ReactNode } from 'react';
import type { Patient, Series, Study, StudyAnalysis } from '@cloudfilm/shared';
import { useStudyShortcuts } from '@/hooks/use-study-shortcuts';
import { StudyMetadataPanel } from './study-metadata-panel';
import { ViewerPanel } from './viewer-panel';
import { WorkspaceStatusBar } from './workspace-status-bar';

/**
 * The three-zone workstation: navigator, viewport, metadata rail.
 *
 * A grid rather than a row of flex children, and the difference is not
 * cosmetic. The two rails are fixed tracks and the viewport is
 * `minmax(0, 1fr)`, so the image column can never be squeezed by a long study
 * description or a wide Study UID in a rail beside it - which is exactly what a
 * flex row allowed. `min-h-0` on the grid and on each track is what lets the
 * rails scroll internally instead of growing the page.
 *
 * The status strip now lives *inside* the viewport column rather than spanning
 * all three zones. It reports the study on screen and the image count in it, so
 * it belongs to the image: it sits on the same dark ground, it ends where the
 * image ends, and the rails run full height beside it.
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
  isStudyImported,
  isRead,
  analysis,
  children,
}: {
  readonly patient: Patient;
  readonly study: Study;
  readonly studies: readonly Study[];
  readonly series: readonly Series[];
  readonly isStudyImported: boolean;
  readonly isRead: boolean;

  /** The stored analysis of this study, or `null` if it has never been analysed. */
  readonly analysis: StudyAnalysis | null;
  readonly children: ReactNode;
}) {
  useStudyShortcuts(studies, study.studyInstanceUid);

  return (
    <div
      className={[
        'grid min-h-0 flex-1',
        // One column below lg: the rails are hidden there, and a 240px rail
        // beside a diagnostic image on a tablet helps nobody.
        'grid-cols-[minmax(0,1fr)]',
        'lg:grid-cols-[264px_minmax(0,1fr)]',
        'xl:grid-cols-[264px_minmax(0,1fr)_auto]',
      ].join(' ')}
    >
      {children}

      {/*
        The viewport column. Dark ground, its own status strip, and nothing
        decorative: no dot field, no gradient, no atmospheric mark inside or
        behind the image.
      */}
      <main
        className="flex min-h-0 min-w-0 flex-col bg-[var(--atrium-viewport)]"
        aria-label={study.description}
      >
        <ViewerPanel study={study} isStudyImported={isStudyImported} />
        <WorkspaceStatusBar study={study} isRead={isRead} />
      </main>

      <StudyMetadataPanel patient={patient} study={study} series={series} analysis={analysis} />
    </div>
  );
}
