'use client';

import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { DEFAULT_LOCALE, getTranslations, type Locale, type Translator } from '@cloudfilm/shared';

/**
 * The active locale, made available to Client Components.
 *
 * Only the locale crosses the boundary, not the catalogue: both catalogues are
 * small and already in the bundle, so serialising the active one into every RSC
 * payload would pay a per-request cost for data the browser can hold once.
 */
const LocaleContext = createContext<Locale>(DEFAULT_LOCALE);

/**
 * Seeds the locale for the client tree.
 *
 * Mounted in the root layout, which puts it above every route - including the
 * `error.tsx` boundaries, which are Client Components rendered inside their
 * parent layout. Without that placement an error page would be the one screen a
 * user could not read in their own language, which is precisely the screen they
 * most need to.
 */
export function LocaleProvider({
  locale,
  children,
}: {
  readonly locale: Locale;
  readonly children: ReactNode;
}) {
  return <LocaleContext value={locale}>{children}</LocaleContext>;
}

/** The active locale, inside a Client Component. */
export function useLocale(): Locale {
  return useContext(LocaleContext);
}

/**
 * The translator, inside a Client Component.
 *
 * The server counterpart is `serverTranslations()` in `lib/locale.ts`. Both
 * resolve against the same catalogues, so a label rendered on the server and
 * re-rendered on the client is identical and hydration does not warn.
 */
export function useTranslations(): Translator {
  const locale = useLocale();

  return useMemo(() => getTranslations(locale), [locale]);
}
