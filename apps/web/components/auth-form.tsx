'use client';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { FormEvent, useState } from 'react';
import { login, register } from '../lib/auth-api';

type Mode = 'login' | 'register';

export function AuthForm({ mode }: { mode: Mode }) {
  const router = useRouter();
  const search = useSearchParams();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    const form = new FormData(event.currentTarget);
    try {
      if (mode === 'login') {
        await login({ email: String(form.get('email') ?? ''), password: String(form.get('password') ?? '') });
      } else {
        await register({ email: String(form.get('email') ?? ''), password: String(form.get('password') ?? ''), displayName: String(form.get('displayName') ?? ''), organizationName: String(form.get('organizationName') ?? '') || undefined });
      }
      const next = search.get('next');
      router.replace(next?.startsWith('/') ? next : '/workspace');
      router.refresh();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Authentication failed.');
    } finally { setBusy(false); }
  }

  return <section className="authPanel">
    <div className="authIntro"><span className="kicker">SECURE ORGANIZATION ACCESS</span><h1>{mode === 'login' ? 'Return to your procurement workspace.' : 'Create your TenderScope workspace.'}</h1><p>{mode === 'login' ? 'Continue your organization pipeline, assignments and audit trail.' : 'Create an isolated organization workspace with owner-level access.'}</p></div>
    <form className="authForm" onSubmit={submit}>
      {mode === 'register' && <><label>Full name<input name="displayName" autoComplete="name" minLength={2} maxLength={160} required /></label><label>Organization name<input name="organizationName" autoComplete="organization" maxLength={180} placeholder="Optional" /></label></>}
      <label>Email address<input name="email" type="email" autoComplete="email" required /></label>
      <label>Password<input name="password" type="password" autoComplete={mode === 'login' ? 'current-password' : 'new-password'} minLength={10} required /></label>
      {mode === 'login' && <p className="authSwitch"><Link href="/forgot-password">Forgot password?</Link></p>}
      {error && <div className="authError" role="alert">{error}</div>}
      <button className="authSubmit" disabled={busy}>{busy ? 'Please wait…' : mode === 'login' ? 'Sign in' : 'Create workspace'}</button>
      <p className="authSwitch">{mode === 'login' ? <>New to TenderScope? <Link href="/register">Create an account</Link></> : <>Already have an account? <Link href="/login">Sign in</Link></>}</p>
    </form>
  </section>;
}
