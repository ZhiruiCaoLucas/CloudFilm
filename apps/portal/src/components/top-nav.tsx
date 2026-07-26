import Link from 'next/link';
import { CloudFilmLogo } from './cloudfilm-logo';
import { EnvironmentBadge } from './environment-badge';
import { isDevelopment } from '@/lib/env';

/** Application chrome: identity of the system, and which instance of it. */
export function TopNav() {
  return (
    <header className="border-border bg-surface-raised border-b">
      <div className="mx-auto flex w-full max-w-5xl items-center justify-between gap-4 px-6 py-4">
        <Link
          href="/"
          className="focus-visible:outline-accent flex items-center gap-3 rounded-md focus-visible:outline-2 focus-visible:outline-offset-4"
        >
          <CloudFilmLogo className="h-8 w-8 shrink-0" />
          <span className="flex flex-col leading-tight">
            <span className="text-foreground text-sm font-semibold tracking-tight">CloudFilm</span>
            <span className="text-muted text-xs">Cloud Imaging Platform</span>
          </span>
        </Link>

        <EnvironmentBadge environment={isDevelopment ? 'Development' : 'Production'} />
      </div>
    </header>
  );
}
