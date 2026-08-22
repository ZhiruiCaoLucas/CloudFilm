'use client';

import { useActionState, useId } from 'react';
import { useFormStatus } from 'react-dom';
import { Button, Input } from '@cloudfilm/ui';
import { useTranslations } from '@/components/locale-provider';
import { signIn } from './actions';
import { initialSignInState } from './sign-in-state';

/**
 * Credential entry.
 *
 * A real `<form>` wired to a Server Action, so it works before hydration and
 * submits on Enter. The password never reaches any client-side state: it goes
 * straight from the field into the action's `FormData`, and the browser is left
 * holding nothing.
 */
export function SignInForm({ returnUrl }: { readonly returnUrl: string }) {
  const t = useTranslations();
  const [state, formAction] = useActionState(signIn, initialSignInState);

  const emailId = useId();
  const passwordId = useId();
  const errorId = useId();

  return (
    <form action={formAction} className="flex flex-col gap-5" noValidate>
      <input type="hidden" name="returnUrl" value={returnUrl} />

      <div className="flex flex-col gap-1.5">
        <label htmlFor={emailId} className="text-foreground text-xs font-medium">
          {t('auth.email')}
        </label>
        <Input
          id={emailId}
          name="email"
          type="email"
          autoComplete="username"
          required
          invalid={state.error !== null}
          {...(state.error === null ? {} : { 'aria-describedby': errorId })}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor={passwordId} className="text-foreground text-xs font-medium">
          {t('auth.password')}
        </label>
        <Input
          id={passwordId}
          name="password"
          type="password"
          autoComplete="current-password"
          required
          invalid={state.error !== null}
          {...(state.error === null ? {} : { 'aria-describedby': errorId })}
        />
      </div>

      {/*
        A rule in the error colour rather than a filled panel. The message is
        already announced, already red, and already attached to both fields by
        aria-describedby; a tinted box around it would be the loudest thing on a
        page whose whole job is to stay calm.
      */}
      {state.error === null ? null : (
        <p
          id={errorId}
          role="alert"
          className="border-l-2 border-red-500 pl-3 text-[0.8125rem] leading-relaxed text-red-600 dark:text-red-400"
        >
          {t(state.error)}
        </p>
      )}

      <SubmitButton />
    </form>
  );
}

/**
 * Split out because `useFormStatus` reports the status of the form *above* it,
 * so it has to live in a child of the form rather than beside the action.
 */
function SubmitButton() {
  const t = useTranslations();
  const { pending } = useFormStatus();

  return (
    <Button type="submit" disabled={pending} aria-busy={pending} className="mt-1 w-full">
      {pending ? t('auth.submitting') : t('auth.submit')}
    </Button>
  );
}
