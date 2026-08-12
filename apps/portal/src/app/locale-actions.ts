'use server';

import { cookies } from 'next/headers';
import { revalidatePath } from 'next/cache';
import { toLocale } from '@cloudfilm/shared';
import { LOCALE_COOKIE, LOCALE_MAX_AGE_SECONDS } from '@/lib/locale';

/**
 * Stores a language preference.
 *
 * @param value The requested locale. Anything unrecognised falls back to English.
 * @remarks
 * Writes one cookie and revalidates. It does not redirect, does not read or
 * write `cloudfilm_session`, and does not touch the URL - so the current
 * pathname, the patient or study being viewed, the `returnUrl` on the sign-in
 * page and the session all survive a language change untouched. Re-rendering
 * the current route is enough, because every label is resolved on the server
 * from this cookie.
 *
 * The whole layout is revalidated rather than a single path, since the language
 * affects the navigation and the document's `lang` attribute as well as the
 * page.
 */
export async function setLocale(value: string): Promise<void> {
  const store = await cookies();

  store.set({
    name: LOCALE_COOKIE,
    value: toLocale(value),

    // Readable by script, unlike the session cookie. There is nothing here to
    // protect: the value is `en` or `zh`, and it is written by this action
    // rather than trusted from the browser.
    httpOnly: false,
    sameSite: 'lax',
    path: '/',
    maxAge: LOCALE_MAX_AGE_SECONDS,
  });

  revalidatePath('/', 'layout');
}
