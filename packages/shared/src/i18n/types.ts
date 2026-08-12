import type { Messages } from './en';

export type { Messages };

/** The UI languages CloudFilm ships. */
export type Locale = 'en' | 'zh';

/** Every supported locale, in the order the switcher presents them. */
export const LOCALES: readonly Locale[] = ['en', 'zh'];

/**
 * The locale used when none has been chosen, or when the stored one is not one
 * CloudFilm ships.
 */
export const DEFAULT_LOCALE: Locale = 'en';

/**
 * Every leaf path in a message catalogue, as a dotted string.
 *
 * This is what makes `t('study.accession')` safe: a typo, a renamed section or
 * a deleted message is a compile error at the call site rather than an empty
 * span in a clinical UI.
 */
export type MessagePath<T> = {
  [K in keyof T & string]: T[K] extends string ? K : `${K}.${MessagePath<T[K]>}`;
}[keyof T & string];

/** Every message key CloudFilm defines. */
export type MessageKey = MessagePath<Messages>;

/**
 * Values substituted into a message's `{placeholder}` slots.
 *
 * Numbers are accepted because counts are the common case and forcing every
 * call site to stringify them adds noise without adding safety.
 */
export type MessageValues = Readonly<Record<string, string | number>>;

/**
 * Resolves a message key in the active locale.
 *
 * Named `t` at every call site by convention.
 */
export type Translator = (key: MessageKey, values?: MessageValues) => string;
