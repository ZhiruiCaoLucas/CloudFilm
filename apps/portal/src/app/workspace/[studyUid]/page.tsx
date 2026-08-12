import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { serverApiClient } from '@/lib/api';
import { serverTranslations } from '@/lib/locale';
import { env } from '@/lib/env';
import { StudyNavigator } from '@/components/workspace/study-navigator';
import { ViewerWorkspace } from '@/components/workspace/viewer-workspace';
import { WorkspaceHeader } from '@/components/workspace/workspace-header';

interface WorkspacePageProps {
  readonly params: Promise<{ readonly studyUid: string }>;
}

/**
 * The document title, derived without touching patient data.
 *
 * This used to resolve the study and title the tab with the modality and the
 * patient's name. Two things were wrong with that.
 *
 * The first is measurable. Next runs `generateMetadata` and the page component
 * as separate passes, and in this deployment they do not share a `React.cache`
 * scope: one workspace load produced two `GET /api/v1/studies/{uid}` requests
 * with distinct request ids, and therefore six QIDO queries against Orthanc to
 * show one study. Memoisation was tried and measured, and it did not deduplicate
 * them.
 *
 * The second matters more. A patient's name in `document.title` is written to
 * the tab strip, the window title, the OS task bar, browser history - which a
 * signed-in Chrome or Edge profile syncs to its vendor - and every screen share
 * and recording. None of those are surfaces CloudFilm can audit, which is the
 * guarantee the rest of the system exists to provide.
 *
 * The patient page already works this way: its title comes from the route
 * parameter and it makes no API call. This brings the workspace into line, and
 * the duplicate request disappears by construction rather than by trusting a
 * cache to behave.
 *
 * Still resolved per request, so the title follows the reader's language.
 */
export async function generateMetadata(): Promise<Metadata> {
  const t = await serverTranslations();

  return { title: t('workspace.pageTitle') };
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
  const client = await serverApiClient();
  const context = await client.getStudy(decodeURIComponent(studyUid));

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
