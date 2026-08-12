import 'server-only';
import { cookies } from 'next/headers';
import { getTranslations, toLocale, type Locale, type Translator } from '@cloudfilm/shared';

/**
 * The browser's language preference.
 *
 * A separate cookie from `cloudfilm_session` on purpose, and deliberately not
 * `HttpOnly`: it carries no token, no password and nothing that identifies a
 * user - only `en` or `zh`. Merging it into the session cookie would tie a
 * display preference to a credential's lifetime, so choosing a language would
 * be lost at sign-out and changing one would rewrite the other.
 */
export const LOCALE_COOKIE = 'cloudfilm_locale';

/**
 * How long a language choice is remembered.
 *
 * A year. It is a preference, not a session: a clinician who reads in Chinese
 * should not have to say so again next week.
 */
export const LOCALE_MAX_AGE_SECONDS = 60 * 60 * 24 * 365;

/**
 * Reads the active locale.
 *
 * @returns The stored locale, or English when none is stored or the stored
 * value is not one CloudFilm ships.
 */
export async function getLocale(): Promise<Locale> {
  const store = await cookies();

  return toLocale(store.get(LOCALE_COOKIE)?.value);
}

/**
 * The translator for the active locale, for use in Server Components.
 *
 * @returns A function that resolves message keys.
 * @remarks
 * Client Components cannot call this - it reads a cookie. They take the locale
 * from `LocaleProvider`, which the root layout seeds with this same value, so
 * both sides resolve identical text and hydration matches.
 */
export async function serverTranslations(): Promise<Translator> {
  return getTranslations(await getLocale());
}
