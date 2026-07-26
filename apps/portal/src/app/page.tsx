import { Card, CardBody, CardHeader, StatusBadge } from '@cloudfilm/ui';
import type { SystemInfo } from '@cloudfilm/shared';
import { serverApiClient } from '@/lib/api';

// The landing page reports live platform state, so it must never be prerendered
// at build time - the API is not reachable from the build container.
export const dynamic = 'force-dynamic';

async function readSystemInfo(): Promise<SystemInfo | null> {
  try {
    return await serverApiClient().getSystemInfo();
  } catch {
    return null;
  }
}

export default async function HomePage() {
  const systemInfo = await readSystemInfo();

  return (
    <div className="flex flex-col gap-8">
      <section className="flex flex-col gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">Imaging portal</h1>
        <p className="text-muted max-w-2xl text-sm leading-relaxed">
          CloudFilm gives patients and clinicians browser access to imaging studies without exposing
          the hospital PACS network. Authentication, study retrieval and the viewer are being
          delivered sprint by sprint.
        </p>
      </section>

      <Card>
        <CardHeader className="flex items-center justify-between">
          <h2 className="text-sm font-medium">Platform status</h2>
          <StatusBadge tone={systemInfo ? 'healthy' : 'unhealthy'}>
            {systemInfo ? 'API reachable' : 'API unreachable'}
          </StatusBadge>
        </CardHeader>
        <CardBody>
          <dl className="grid grid-cols-1 gap-4 text-sm sm:grid-cols-3">
            <div>
              <dt className="text-muted text-xs tracking-wide uppercase">Service</dt>
              <dd className="mt-1 font-medium">{systemInfo?.name ?? 'Unknown'}</dd>
            </div>
            <div>
              <dt className="text-muted text-xs tracking-wide uppercase">Version</dt>
              <dd className="mt-1 font-medium">{systemInfo?.version ?? '-'}</dd>
            </div>
            <div>
              <dt className="text-muted text-xs tracking-wide uppercase">Environment</dt>
              <dd className="mt-1 font-medium">{systemInfo?.environment ?? '-'}</dd>
            </div>
          </dl>
        </CardBody>
      </Card>
    </div>
  );
}
