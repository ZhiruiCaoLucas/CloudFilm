'use client';

import Link from 'next/link';
import { Button, buttonClasses, Card, CardBody, CardHeader } from '@cloudfilm/ui';

/**
 * Catches unexpected failures while rendering the workspace.
 *
 * Rendered in the workstation's own full-viewport shell rather than the portal
 * chrome, so a failure does not drop the user into a differently shaped page
 * than the one they were navigating to.
 */
export default function WorkspaceError({
  error,
  reset,
}: {
  readonly error: Error & { digest?: string };
  readonly reset: () => void;
}) {
  return (
    <div className="flex h-dvh items-center justify-center px-6">
      <Card className="max-w-lg">
        <CardHeader>
          <h1 className="text-foreground text-lg font-semibold tracking-tight">
            This study could not be opened
          </h1>
        </CardHeader>
        <CardBody className="flex flex-col gap-4">
          <p className="text-muted text-sm">
            The workspace failed to load. No imaging data has been altered.
          </p>
          {error.digest === undefined ? null : (
            <p className="text-muted text-xs">
              Reference <code className="text-foreground font-mono">{error.digest}</code> — quote
              this when reporting the problem.
            </p>
          )}
          <div className="flex gap-3">
            <Button onClick={reset}>Retry</Button>
            <Link href="/" className={buttonClasses('secondary')}>
              Patient search
            </Link>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
