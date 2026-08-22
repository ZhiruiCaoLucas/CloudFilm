'use client';

import { useTransition } from 'react';
import { LOCALES, type Locale } from '@cloudfilm/shared';
import { cn } from '@cloudfilm/ui';
import { setLocale } from '@/app/locale-actions';
import { useLocale, useTranslations } from './locale-provider';

/**
 * Switches the UI language.
 *
 * Buttons rather than a link per language: choosing a language changes stored
 * state, so it must not be something a crawler or a prefetch can trigger, and
 * it must not put a language into the URL - CloudFilm has no `/en` or `/zh`
 * routes, and a study's address should not change because someone reads in a
 * different language.
 *
 * The action rewrites one cookie and revalidates, so the current route, the
 * study on screen and the session all survive.
 */
export function LocaleSwitcher() {
  const active = useLocale();
  const t = useTranslations();
  const [isPending, startTransition] = useTransition();

  return (
    <div
      role="group"
      aria-label={t('locale.label')}
      className="border-border bg-surface-raised flex items-center overflow-hidden rounded-md border shadow-[inset_0_1px_0_0_var(--glass-edge)]"
    >
      {LOCALES.map((locale) => (
        <button
          key={locale}
          type="button"
          lang={locale}
          aria-pressed={locale === active}
          disabled={isPending || locale === active}
          onClick={() => startTransition(() => setLocale(locale))}
          className={cn(
            'focus-visible:outline-accent min-h-9 px-2.5 text-xs',
            'transition-[color,background-color,box-shadow] duration-200',
            'focus-visible:outline-2 focus-visible:-outline-offset-2',
            'disabled:pointer-events-none',
            // The selected segment is lit from inside rather than merely filled:
            // a tint of the accent plus a hairline under the top edge, so the
            // pair reads as one machined control with one position pressed.
            locale === active
              ? 'bg-accent-soft text-foreground font-medium shadow-[inset_0_1px_0_0_var(--glass-highlight)]'
              : 'text-muted hover:bg-surface-overlay hover:text-foreground',
          )}
        >
          {t(localeLabelKey(locale))}
        </button>
      ))}
    </div>
  );
}

/** Maps a locale onto the message key that names it. */
function localeLabelKey(locale: Locale): 'locale.en' | 'locale.zh' {
  return locale === 'zh' ? 'locale.zh' : 'locale.en';
}
