import { notFound } from 'next/navigation';
import { buildViewerUrl } from '@cloudfilm/core';
import { env } from '@/lib/env';
import { PortalShell } from '@/components/portal-shell';

export const dynamic = 'force-dynamic';

interface ViewerPageProps {
  readonly params: Promise<{ readonly studyInstanceUid: string }>;
}

/**
 * Hosts the OHIF Viewer for a single study.
 *
 * OHIF runs as its own origin in an iframe rather than as a React dependency. That
 * boundary is deliberate: it keeps the viewer upgradeable without touching the
 * portal, and it means viewer code never shares a JavaScript context with the
 * portal's session.
 */
export default async function ViewerPage({ params }: ViewerPageProps) {
  const { studyInstanceUid } = await params;

  if (studyInstanceUid.trim().length === 0) {
    notFound();
  }

  const viewerUrl = buildViewerUrl({
    viewerBaseUrl: env.viewerUrl,
    studyInstanceUid,
  });

  return (
    <PortalShell>
      <div className="flex h-[calc(100dvh-14rem)] flex-col gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-lg font-semibold tracking-tight">Study viewer</h1>
          <p className="text-muted font-mono text-xs">{studyInstanceUid}</p>
        </div>
        <iframe
          title={`OHIF Viewer - study ${studyInstanceUid}`}
          src={viewerUrl}
          className="border-border h-full w-full rounded-lg border bg-black"
          allow="fullscreen"
          sandbox="allow-scripts allow-same-origin allow-forms allow-downloads"
        />
      </div>
    </PortalShell>
  );
}
