import { buildInfo } from '@/lib/build-info';

/** Build provenance, so a support call can start with "which version are you on?". */
export function SiteFooter() {
  return (
    <footer className="border-border mt-16 border-t">
      <div className="text-muted mx-auto flex w-full max-w-5xl flex-col gap-2 px-6 py-6 text-xs sm:flex-row sm:items-center sm:justify-between">
        <span className="text-foreground font-medium">CloudFilm</span>
        <dl className="flex flex-wrap items-center gap-x-6 gap-y-1">
          <div className="flex items-center gap-1.5">
            <dt>Version</dt>
            <dd className="font-mono">{buildInfo.version}</dd>
          </div>
          <div className="flex items-center gap-1.5">
            <dt>Build</dt>
            <dd className="font-mono">{buildInfo.commit}</dd>
          </div>
          <div className="flex items-center gap-1.5">
            <dt className="sr-only">Built</dt>
            <dd>{buildInfo.builtAt}</dd>
          </div>
        </dl>
      </div>
    </footer>
  );
}
