import type { Metadata, Viewport } from 'next';
import { ThemeScript } from '@/components/theme-script';
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
      <body className="min-h-dvh antialiased">
        <a
          href="#main"
          className="focus:bg-accent focus:text-accent-foreground sr-only focus:not-sr-only focus:absolute focus:top-4 focus:left-4 focus:z-50 focus:rounded-md focus:px-4 focus:py-2"
        >
          Skip to content
        </a>
        <div className="mx-auto flex min-h-dvh w-full max-w-5xl flex-col px-6">
          <header className="border-border flex items-center justify-between border-b py-5">
            <span className="text-lg font-semibold tracking-tight">CloudFilm</span>
            <span className="text-muted text-xs">Imaging portal</span>
          </header>
          <main id="main" className="flex-1 py-10">
            {children}
          </main>
          <footer className="border-border text-muted border-t py-5 text-xs">
            CloudFilm never stores imaging data permanently. Cached studies expire automatically.
          </footer>
        </div>
      </body>
    </html>
  );
}
