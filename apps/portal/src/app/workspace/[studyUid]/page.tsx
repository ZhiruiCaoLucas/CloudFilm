import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { formatModality } from '@cloudfilm/shared';
import { serverApiClient } from '@/lib/api';
import { env } from '@/lib/env';
import { StudyNavigator } from '@/components/workspace/study-navigator';
import { ViewerWorkspace } from '@/components/workspace/viewer-workspace';
import { WorkspaceHeader } from '@/components/workspace/workspace-header';

interface WorkspacePageProps {
  readonly params: Promise<{ readonly studyUid: string }>;
}

export async function generateMetadata({ params }: WorkspacePageProps): Promise<Metadata> {
  const { studyUid } = await params;
  const context = await serverApiClient().getStudy(decodeURIComponent(studyUid));

  if (context === null) {
    return { title: 'Study not found · CloudFilm' };
  }

  return {
    title: `${formatModality(context.selectedStudy.modality)} — ${context.patient.fullName} · CloudFilm`,
  };
}

/**
 * The imaging workspace.
 *
 * Full viewport, no page chrome and no page scroll: the reading area should get
 * every pixel that is not doing identification or navigation work.
 *
 * The viewer address is resolved here, on the server, and handed down as a prop.
 * That keeps it a runtime value rather than something inlined into the client
 * bundle at build time, so one portal image serves every hospital.
 */
export default async function WorkspacePage({ params }: WorkspacePageProps) {
  const { studyUid } = await params;
  const context = await serverApiClient().getStudy(decodeURIComponent(studyUid));

  if (context === null) {
    notFound();
  }

  return (
    <div id="main" className="flex h-dvh min-h-0 flex-col overflow-hidden">
      <WorkspaceHeader patient={context.patient} study={context.selectedStudy} />

      {/*
        isStudyImported is derived from the response rather than hard-coded: a
        study the catalog knows about but which carries no images is a real
        state, and the viewer has nothing to render for it. Keeping it truthful
        leaves ViewerPanel and its no-data state untouched.
      */}
      <ViewerWorkspace
        patient={context.patient}
        study={context.selectedStudy}
        studies={context.studies}
        series={context.series}
        viewerBaseUrl={env.viewerUrl}
        isStudyImported={context.selectedStudy.instanceCount > 0}
      >
        <StudyNavigator
          patient={context.patient}
          studies={context.studies}
          selectedStudyInstanceUid={context.selectedStudy.studyInstanceUid}
        />
      </ViewerWorkspace>
    </div>
  );
}
