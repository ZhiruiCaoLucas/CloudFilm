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
    <form action={formAction} className="flex flex-col gap-4" noValidate>
      <input type="hidden" name="returnUrl" value={returnUrl} />

      <div className="flex flex-col gap-2">
        <label htmlFor={emailId} className="text-foreground text-sm font-medium">
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

      <div className="flex flex-col gap-2">
        <label htmlFor={passwordId} className="text-foreground text-sm font-medium">
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

      {state.error === null ? null : (
        <p id={errorId} role="alert" className="text-sm text-red-500">
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
    <Button type="submit" disabled={pending}>
      {pending ? t('auth.submitting') : t('auth.submit')}
    </Button>
  );
}
