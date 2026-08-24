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
      <div className="atrium-swap flex flex-col gap-[34px]">
        <Skeleton className="h-5 w-32" />

        {/* Mirrors PatientSummary: an icon tile beside the name, closed by a
            rule, then the three-column definition list. */}
        <div className="border-atrium-border border-b pb-7">
          <div className="flex items-center gap-4">
            <Skeleton className="h-[52px] w-[52px] rounded-xl" />
            <Skeleton className="h-8 w-56" />
          </div>
          <div className="mt-7 grid grid-cols-1 gap-x-10 gap-y-5 sm:grid-cols-3">
            {['mrn', 'gender', 'birth-date'].map((field) => (
              <div key={field} className="flex flex-col gap-2">
                <Skeleton className="h-2.5 w-24" />
                <Skeleton className="h-4 w-32" />
              </div>
            ))}
          </div>
        </div>

        <div className="flex flex-col gap-[18px]">
          <Skeleton className="h-4 w-20" />
          <div className="border-atrium-border flex flex-col gap-px overflow-hidden rounded-lg border">
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
