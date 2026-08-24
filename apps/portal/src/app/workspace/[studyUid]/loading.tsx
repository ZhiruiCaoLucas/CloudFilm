import { Skeleton } from '@cloudfilm/ui';
import { serverTranslations } from '@/lib/locale';

/**
 * Shown while a study is being resolved.
 *
 * Reproduces the workstation's three regions so switching studies does not
 * collapse the layout and rebuild it. The reading area stays black throughout,
 * because that is the surround a clinician's eyes are already adapted to.
 */
export default async function WorkspaceLoading() {
  const t = await serverTranslations();

  return (
    <div className="atrium-swap flex h-dvh min-h-0 flex-col overflow-hidden">
      <div className="border-atrium-border bg-atrium-white flex shrink-0 items-center gap-6 border-b px-4 py-2.5">
        <Skeleton className="h-6 w-6" />
        <Skeleton className="h-4 w-24" />
        <div className="border-atrium-border h-6 w-px shrink-0 border-l" />
        <Skeleton className="h-4 w-40" />
        <Skeleton className="h-4 w-28" />
      </div>

      <div className="flex min-h-0 flex-1">
        <div className="border-atrium-border bg-atrium-white flex w-80 shrink-0 flex-col gap-3 border-r p-4">
          <Skeleton className="h-4 w-20" />
          <Skeleton className="h-4 w-36" />
          <div className="mt-4 flex flex-col gap-2">
            {['first', 'second', 'third'].map((row) => (
              <Skeleton key={row} className="h-20" />
            ))}
          </div>
        </div>
        <div className="flex flex-1 items-center justify-center bg-black">
          <p className="text-sm text-slate-500" role="status">
            {t('workspace.loading')}
          </p>
        </div>
      </div>

      <div className="border-atrium-border bg-atrium-white flex shrink-0 items-center gap-6 border-t px-4 py-2">
        <Skeleton className="h-3 w-24" />
        <Skeleton className="h-3 w-64" />
      </div>
    </div>
  );
}
