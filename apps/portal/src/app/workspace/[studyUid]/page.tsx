import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { formatModality } from '@cloudfilm/shared';
import { findStudyContext, IS_SERVING_SEEDED_DATA } from '@cloudfilm/shared/mock';
import { env } from '@/lib/env';
import { StudyNavigator } from '@/components/workspace/study-navigator';
import { ViewerWorkspace } from '@/components/workspace/viewer-workspace';
import { WorkspaceHeader } from '@/components/workspace/workspace-header';

interface WorkspacePageProps {
  readonly params: Promise<{ readonly studyUid: string }>;
}

export async function generateMetadata({ params }: WorkspacePageProps): Promise<Metadata> {
  const { studyUid } = await params;
  const context = findStudyContext(decodeURIComponent(studyUid));

  if (context === undefined) {
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
  const context = findStudyContext(decodeURIComponent(studyUid));

  if (context === undefined) {
    notFound();
  }

  return (
    <div id="main" className="flex h-dvh min-h-0 flex-col overflow-hidden">
      <WorkspaceHeader patient={context.patient} study={context.selectedStudy} />

      <ViewerWorkspace
        patient={context.patient}
        study={context.selectedStudy}
        studies={context.studies}
        viewerBaseUrl={env.viewerUrl}
        isStudyImported={IS_SERVING_SEEDED_DATA}
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
