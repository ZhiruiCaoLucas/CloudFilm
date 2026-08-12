import type { StudyStatus } from '../study';
import { en, type Messages } from './en';
import { zh } from './zh';
import {
  DEFAULT_LOCALE,
  LOCALES,
  type Locale,
  type MessageKey,
  type MessageValues,
  type Translator,
} from './types';

export { DEFAULT_LOCALE, LOCALES };
export type { Locale, MessageKey, MessageValues, Messages, Translator };

/**
 * The message key that labels each study lifecycle state.
 *
 * Lives here, once. Three components render a study's status, and a lookup
 * copied into each of them is three places for English to survive a
 * translation. Exhaustive over {@link StudyStatus}, so adding a status will not
 * compile until its label exists.
 */
export const STUDY_STATUS_MESSAGE_KEYS: Readonly<Record<StudyStatus, MessageKey>> = {
  Completed: 'study.statusCompleted',
  InProgress: 'study.statusInProgress',
  Cancelled: 'study.statusCancelled',
};

/** Every catalogue, keyed by locale. */
const CATALOGUES: Readonly<Record<Locale, Messages>> = { en, zh };

/**
 * Narrows an arbitrary string to a locale CloudFilm ships.
 *
 * @param value A candidate locale, typically from a cookie.
 * @returns The locale, or {@link DEFAULT_LOCALE} when the value is absent or unknown.
 * @remarks
 * The cookie is attacker-controllable, so this never trusts it: an unrecognised
 * value falls back to English rather than producing a catalogue lookup against
 * an object that does not exist.
 */
export function toLocale(value: string | undefined): Locale {
  return LOCALES.find((locale) => locale === value?.trim()) ?? DEFAULT_LOCALE;
}

/**
 * Builds the translator for a locale.
 *
 * @param locale The active locale.
 * @returns A function that resolves message keys.
 * @remarks
 * Both the server and the browser call this, over the same catalogues, so a
 * Server Component and a Client Component rendering the same key cannot
 * disagree - which would otherwise show up as a hydration mismatch.
 */
export function getTranslations(locale: Locale): Translator {
  const messages = CATALOGUES[locale];

  return (key: MessageKey, values?: MessageValues): string => {
    const template = resolve(messages, key);

    if (template === undefined) {
      // Should be unreachable: MessageKey is derived from the catalogue. Return
      // the key rather than throwing, because a missing label must never take
      // down a page a clinician is reading.
      return key;
    }

    return values === undefined ? template : interpolate(template, values);
  };
}

/** Walks a dotted path to its leaf string. */
function resolve(messages: Messages, key: string): string | undefined {
  let current: unknown = messages;

  for (const segment of key.split('.')) {
    if (typeof current !== 'object' || current === null) {
      return undefined;
    }

    current = (current as Record<string, unknown>)[segment];
  }

  return typeof current === 'string' ? current : undefined;
}

/** Substitutes `{name}` placeholders. Unknown placeholders are left in place. */
function interpolate(template: string, values: MessageValues): string {
  return template.replace(/\{(\w+)\}/g, (match, name: string) => {
    const value = values[name];

    return value === undefined ? match : String(value);
  });
}
