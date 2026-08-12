import type { Metadata, Viewport } from 'next';
import { getTranslations } from '@cloudfilm/shared';
import { LocaleProvider } from '@/components/locale-provider';
import { ThemeScript } from '@/components/theme-script';
import { getLocale } from '@/lib/locale';
import './globals.css';

/**
 * Resolved per request rather than declared statically, so the document title
 * and description follow the reader's language.
 */
export async function generateMetadata(): Promise<Metadata> {
  const t = getTranslations(await getLocale());

  return {
    title: t('common.appName'),
    description: t('common.description'),
    robots: { index: false, follow: false },
  };
}

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
};

/**
 * Document shell only.
 *
 * Page chrome is chosen per route: {@link PortalShell} for the document-style
 * pages, and a full-viewport shell for the imaging workspace. Both define the
 * `#main` landmark the skip link targets.
 */
export default async function RootLayout({ children }: { readonly children: React.ReactNode }) {
  const locale = await getLocale();
  const t = getTranslations(locale);

  return (
    // `lang` is not decoration: it tells a screen reader which voice to use and
    // the browser which font stack and line-breaking rules apply. Getting it
    // wrong makes Chinese unreadable to assistive technology.
    <html lang={locale} suppressHydrationWarning>
      <head>
        <ThemeScript />
      </head>
      <body className="flex min-h-dvh flex-col antialiased">
        <a
          href="#main"
          className="focus:bg-accent focus:text-accent-foreground sr-only focus:not-sr-only focus:absolute focus:top-4 focus:left-4 focus:z-50 focus:rounded-md focus:px-4 focus:py-2"
        >
          {t('common.skipToContent')}
        </a>
        {/* Above every route, so the error boundaries - which are Client
            Components - can be read in the user's language too. */}
        <LocaleProvider locale={locale}>{children}</LocaleProvider>
      </body>
    </html>
  );
}
