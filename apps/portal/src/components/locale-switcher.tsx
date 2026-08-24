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
    <div role="group" aria-label={t('locale.label')} className="flex items-center gap-3">
      {LOCALES.map((locale) => (
        <button
          key={locale}
          type="button"
          lang={locale}
          aria-pressed={locale === active}
          disabled={isPending || locale === active}
          onClick={() => startTransition(() => setLocale(locale))}
          className={cn(
            'focus-visible:outline-atrium-primary rounded text-[12.5px]',
            'transition-colors duration-[180ms]',
            'focus-visible:outline-2 focus-visible:outline-offset-4',
            'disabled:pointer-events-none',
            // Two words, not a segmented control. Atrium marks the active
            // language by weight and a rule under it, which is the same
            // treatment every other "you are here" in the product uses.
            locale === active
              ? 'text-atrium-ink font-semibold underline decoration-[var(--atrium-sky-300)] decoration-2 underline-offset-[6px]'
              : 'text-atrium-muted hover:text-atrium-primary font-medium',
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
