'use client';
import { useSyncExternalStore } from 'react';
import { cn } from '@cloudfilm/ui';
import { useTranslations } from './locale-provider';

/** What the reader has asked for. `system` means "no preference stored". */
type ThemeChoice = 'system' | 'light' | 'dark';

const STORAGE_KEY = 'cloudfilm-theme';
const CHOICES: readonly ThemeChoice[] = ['system', 'light', 'dark'];

/**
 * Subscribes to the stored preference.
 *
 * `localStorage` is an external store, so it is read through
 * `useSyncExternalStore` rather than copied into state inside an effect: the
 * value can change in another tab, and a component that mirrors it in state has
 * two copies of one fact that will eventually disagree.
 */
function subscribe(onChange: () => void): () => void {
  window.addEventListener('cloudfilm:theme', onChange);
  window.addEventListener('storage', onChange);

  return () => {
    window.removeEventListener('cloudfilm:theme', onChange);
    window.removeEventListener('storage', onChange);
  };
}

function readChoice(): ThemeChoice {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);

    return stored === 'dark' || stored === 'light' ? stored : 'system';
  } catch {
    /* Storage is unavailable in private browsing; `system` is correct there. */
    return 'system';
  }
}

/** The server has no storage, and following the system is the default anyway. */
function readServerChoice(): ThemeChoice {
  return 'system';
}

/**
 * Chooses the colour scheme, or hands the choice back to the operating system.
 *
 * Three positions rather than a toggle, because "follow the system" is a real
 * answer and a two-state switch cannot express it: once a toggle has been
 * touched there is no way back to following the desktop.
 *
 * The component writes one key and dispatches one event. Everything that
 * actually applies the theme lives in the blocking script in `<head>`, so there
 * is exactly one mechanism and no second definition of what dark means.
 */
export function ThemeSwitcher() {
  const t = useTranslations();
  const choice = useSyncExternalStore(subscribe, readChoice, readServerChoice);

  function choose(next: ThemeChoice) {
    // Write, then announce. The store is localStorage; the snapshot above
    // re-reads it when the event lands, so there is nothing to set here.
    try {
      if (next === 'system') {
        window.localStorage.removeItem(STORAGE_KEY);
      } else {
        window.localStorage.setItem(STORAGE_KEY, next);
      }
    } catch {
      /* The class still flips for this page; only persistence is lost. */
    }

    window.dispatchEvent(new Event('cloudfilm:theme'));
  }

  return (
    <div
      role="group"
      aria-label={t('theme.label')}
      className="border-atrium-border bg-atrium-panel inline-flex items-center gap-0.5 rounded-lg border p-0.5"
    >
      {CHOICES.map((option) => (
        <button
          key={option}
          type="button"
          aria-pressed={choice === option}
          onClick={() => choose(option)}
          title={t(`theme.${option}`)}
          className={cn(
            'focus-visible:outline-atrium-primary inline-flex h-6 w-6 items-center justify-center rounded-md',
            'transition-[background-color,color,box-shadow] duration-[180ms]',
            'focus-visible:outline-2 focus-visible:outline-offset-2',
            choice === option
              ? 'bg-atrium-white text-atrium-primary shadow-[var(--shadow-sm)]'
              : 'text-atrium-muted hover:text-atrium-ink hover:bg-atrium-white/60',
          )}
        >
          <ThemeGlyph choice={option} />
          <span className="sr-only">{t(`theme.${option}`)}</span>
        </button>
      ))}
    </div>
  );
}

/**
 * One 14px mark per position.
 *
 * Drawn rather than imported: three glyphs do not justify an icon dependency,
 * and these inherit `currentColor` so they follow the button's own transition.
 */
function ThemeGlyph({ choice }: { readonly choice: ThemeChoice }) {
  return (
    <svg
      width="14"
      height="14"
      viewBox="0 0 16 16"
      fill="none"
      aria-hidden="true"
      focusable="false"
      stroke="currentColor"
      strokeWidth="1.4"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {choice === 'light' ? (
        <>
          <circle cx="8" cy="8" r="3.1" />
          <path d="M8 1.4v1.5M8 13.1v1.5M1.4 8h1.5M13.1 8h1.5M3.4 3.4l1 1M11.6 11.6l1 1M12.6 3.4l-1 1M4.4 11.6l-1 1" />
        </>
      ) : null}

      {choice === 'dark' ? (
        <path d="M13.2 9.6A5.6 5.6 0 0 1 6.4 2.8a5.6 5.6 0 1 0 6.8 6.8Z" />
      ) : null}

      {choice === 'system' ? (
        <>
          <rect x="1.8" y="2.8" width="12.4" height="8.4" rx="1.4" />
          <path d="M5.6 13.4h4.8" />
        </>
      ) : null}
    </svg>
  );
}
