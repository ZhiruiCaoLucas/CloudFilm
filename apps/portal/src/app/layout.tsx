import type { Metadata, Viewport } from 'next';
import { IBM_Plex_Mono, Instrument_Sans } from 'next/font/google';
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
 * Atrium's interface face.
 *
 * Self-hosted by `next/font`, which downloads the files at build time and serves
 * them from CloudFilm's own origin. A hospital deployment therefore makes no
 * request to Google at run time, and no font request carries a referrer naming
 * the page a clinician is on.
 *
 * `display: 'swap'` on purpose: a portal that renders a blank patient name until
 * a webfont arrives is worse than one that renders it in the fallback for a
 * frame.
 */
const instrumentSans = Instrument_Sans({
  subsets: ['latin'],
  weight: ['400', '500', '600', '700'],
  variable: '--font-instrument-sans',
  display: 'swap',
});

/**
 * The technical face.
 *
 * Reserved for values read character by character - MRNs, accession numbers,
 * Study and Series UIDs, dates, counts, build strings. Two weights, because
 * that is all those values need.
 */
const ibmPlexMono = IBM_Plex_Mono({
  subsets: ['latin'],
  weight: ['400', '500'],
  variable: '--font-ibm-plex-mono',
  display: 'swap',
});

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
    <html
      lang={locale}
      suppressHydrationWarning
      className={`${instrumentSans.variable} ${ibmPlexMono.variable}`}
    >
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
