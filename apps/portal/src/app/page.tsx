import { Card, CardBody, CardHeader, StatusBadge } from '@cloudfilm/ui';
import type { SystemInfo } from '@cloudfilm/shared';
import { serverApiClient } from '@/lib/api';
import { env, isDevelopment } from '@/lib/env';

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

      {/* First-run guidance. The portal runs standalone by design, so an
          unreachable API in development is an expected state, not an error -
          but a new contributor should be told exactly how to fix it. */}
      {isDevelopment && systemInfo === null ? (
        <Card>
          <CardHeader>
            <h2 className="text-sm font-medium">The API is not running</h2>
          </CardHeader>
          <CardBody className="text-muted flex flex-col gap-3 text-sm">
            <p>
              The portal is looking for the API at{' '}
              <code className="bg-surface-overlay text-foreground rounded px-1.5 py-0.5 font-mono text-xs">
                {env.apiUrl}
              </code>
              . Start the backing services, then reload this page:
            </p>
            <pre className="bg-surface-overlay text-foreground overflow-x-auto rounded-md p-3 font-mono text-xs leading-relaxed">
              {
                '# everything, in containers\ndocker compose up -d\n\n# or just the dependencies, running the API from source\ndocker compose up -d postgres redis orthanc\ndotnet run --project apps/api/src/CloudFilm.Api'
              }
            </pre>
            <p>
              Override the address with{' '}
              <code className="text-foreground font-mono text-xs">CLOUDFILM_API_URL</code> in{' '}
              <code className="text-foreground font-mono text-xs">apps/portal/.env.local</code>.
              This panel is shown in development only.
            </p>
          </CardBody>
        </Card>
      ) : null}
    </div>
  );
}
