import { Card, CardBody, CardHeader, Skeleton } from '@cloudfilm/ui';
import { PortalShell } from '@/components/portal-shell';

/**
 * Shown while a patient record is being resolved.
 *
 * The route is dynamic, so navigation previously left the previous page on
 * screen with no indication anything was happening. The shape mirrors the real
 * page so the layout does not jump when content arrives.
 */
export default function PatientLoading() {
  return (
    <PortalShell>
      <div className="flex flex-col gap-10">
        <Skeleton className="h-5 w-32" />

        <Card>
          <CardHeader>
            <Skeleton className="h-6 w-48" />
          </CardHeader>
          <CardBody>
            <div className="grid grid-cols-1 gap-6 sm:grid-cols-3">
              {['mrn', 'gender', 'birth-date'].map((field) => (
                <div key={field} className="flex flex-col gap-2">
                  <Skeleton className="h-3 w-24" />
                  <Skeleton className="h-4 w-32" />
                </div>
              ))}
            </div>
          </CardBody>
        </Card>

        <div className="flex flex-col gap-4">
          <Skeleton className="h-4 w-20" />
          <div className="border-border flex flex-col gap-px overflow-hidden rounded-lg border">
            {['first', 'second', 'third'].map((row) => (
              <Skeleton key={row} className="h-16 rounded-none" />
            ))}
          </div>
        </div>
      </div>
      <span className="sr-only" role="status">
        Loading patient record
      </span>
    </PortalShell>
  );
}
