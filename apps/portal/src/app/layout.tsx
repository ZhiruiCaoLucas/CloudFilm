import type { Metadata, Viewport } from 'next';
import { ThemeScript } from '@/components/theme-script';
import { TopNav } from '@/components/top-nav';
import { SiteFooter } from '@/components/site-footer';
import './globals.css';

export const metadata: Metadata = {
  title: 'CloudFilm',
  description: 'Secure browser access to hospital imaging studies.',
  robots: { index: false, follow: false },
};

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
};

export default function RootLayout({ children }: { readonly children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <head>
        <ThemeScript />
      </head>
      <body className="flex min-h-dvh flex-col antialiased">
        <a
          href="#main"
          className="focus:bg-accent focus:text-accent-foreground sr-only focus:not-sr-only focus:absolute focus:top-4 focus:left-4 focus:z-50 focus:rounded-md focus:px-4 focus:py-2"
        >
          Skip to content
        </a>

        <TopNav />

        <main id="main" className="mx-auto w-full max-w-5xl flex-1 px-6 py-10">
          {children}
        </main>

        <SiteFooter />
      </body>
    </html>
  );
}
