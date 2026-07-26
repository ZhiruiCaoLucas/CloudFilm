import Link from 'next/link';
import { buttonClasses, Card, CardBody, CardHeader } from '@cloudfilm/ui';

/**
 * Shown when no study matches the requested Study Instance UID.
 *
 * Rendered inside the workspace's own full-viewport shell rather than the portal
 * chrome, so a bad study link does not drop the user into a differently shaped
 * page than the one they were navigating to.
 */
export default function WorkspaceNotFound() {
  return (
    <div id="main" className="flex h-dvh items-center justify-center px-6">
      <Card className="max-w-lg">
        <CardHeader>
          <h1 className="text-foreground text-lg font-semibold tracking-tight">Study not found</h1>
        </CardHeader>
        <CardBody className="flex flex-col gap-4">
          <p className="text-muted text-sm">
            No study with that Study Instance UID is available. It may belong to a patient this
            build does not carry, or the link may be incomplete.
          </p>
          <div>
            <Link href="/" className={buttonClasses()}>
              Back to patient search
            </Link>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
