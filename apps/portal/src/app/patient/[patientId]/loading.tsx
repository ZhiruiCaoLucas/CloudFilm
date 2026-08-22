import { Skeleton } from '@cloudfilm/ui';
import { PortalShell } from '@/components/portal-shell';
import { serverTranslations } from '@/lib/locale';

/**
 * Shown while a patient record is being resolved.
 *
 * The route is dynamic, so navigation previously left the previous page on
 * screen with no indication anything was happening. The shape mirrors the real
 * page so the layout does not jump when content arrives.
 */
export default async function PatientLoading() {
  const t = await serverTranslations();

  return (
    <PortalShell>
      <div className="flex flex-col gap-10">
        <Skeleton className="h-5 w-32" />

        {/* Mirrors PatientSummary: a masthead closed by a rule, not a tile. */}
        <div className="border-border border-b pb-6">
          <Skeleton className="h-7 w-48" />
          <div className="mt-5 grid grid-cols-1 gap-x-10 gap-y-4 sm:grid-cols-3">
            {['mrn', 'gender', 'birth-date'].map((field) => (
              <div key={field} className="flex flex-col gap-1.5">
                <Skeleton className="h-2.5 w-24" />
                <Skeleton className="h-4 w-32" />
              </div>
            ))}
          </div>
        </div>

        <div className="flex flex-col gap-4">
          <Skeleton className="h-4 w-20" />
          <div className="border-border flex flex-col gap-px overflow-hidden rounded-lg border">
            {['first', 'second', 'third'].map((row) => (
              <Skeleton key={row} className="h-12 rounded-none" />
            ))}
          </div>
        </div>
      </div>
      <span className="sr-only" role="status">
        {t('patient.loading')}
      </span>
    </PortalShell>
  );
}
