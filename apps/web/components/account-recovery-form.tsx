'use client';

import Link from 'next/link';
import { FormEvent, useState } from 'react';
import { confirmEmail, confirmPasswordReset, requestPasswordReset } from '../lib/account-recovery-api';

export function ForgotPasswordForm() {
  const [email, setEmail] = useState(''); const [message, setMessage] = useState(''); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) { event.preventDefault(); setBusy(true); setMessage(''); try { const result = await requestPasswordReset(email); setMessage(result.actionUrl ? `Reset link: ${result.actionUrl}` : 'If this account exists, a reset instruction has been created.'); } catch (error) { setMessage(error instanceof Error ? error.message : 'Request failed.'); } finally { setBusy(false); } }
  return <AuthFrame title="Recover access." copy="Create a short-lived reset instruction without revealing whether an account exists."><form className="authForm" onSubmit={submit}><label>Email<input type="email" value={email} onChange={e => setEmail(e.target.value)} required /></label>{message && <div className="authError">{message}</div>}<button className="authSubmit" disabled={busy}>{busy ? 'Submitting…' : 'Request reset'}</button><p className="authSwitch"><Link href="/login">Back to sign in</Link></p></form></AuthFrame>;
}

export function ResetPasswordForm({ token }: { token: string }) {
  const [password, setPassword] = useState(''); const [message, setMessage] = useState(''); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) { event.preventDefault(); setBusy(true); setMessage(''); try { await confirmPasswordReset(token, password); setMessage('Password updated. You can sign in now.'); } catch (error) { setMessage(error instanceof Error ? error.message : 'Reset failed.'); } finally { setBusy(false); } }
  return <AuthFrame title="Set a new password." copy="The reset token is single-use and expires after 30 minutes."><form className="authForm" onSubmit={submit}><label>New password<input type="password" minLength={10} value={password} onChange={e => setPassword(e.target.value)} required /></label>{message && <div className="authError">{message}</div>}<button className="authSubmit" disabled={busy || !token}>{busy ? 'Updating…' : 'Update password'}</button><p className="authSwitch"><Link href="/login">Return to sign in</Link></p></form></AuthFrame>;
}

export function VerifyEmailForm({ token }: { token: string }) {
  const [message, setMessage] = useState(''); const [busy, setBusy] = useState(false);
  async function verify() { setBusy(true); setMessage(''); try { await confirmEmail(token); setMessage('Email address verified.'); } catch (error) { setMessage(error instanceof Error ? error.message : 'Verification failed.'); } finally { setBusy(false); } }
  return <AuthFrame title="Verify your email." copy="Confirm the address attached to your TenderScope account."><div className="authForm">{message && <div className="authError">{message}</div>}<button className="authSubmit" disabled={busy || !token} onClick={verify}>{busy ? 'Verifying…' : 'Verify email'}</button><p className="authSwitch"><Link href="/workspace">Open workspace</Link></p></div></AuthFrame>;
}

function AuthFrame({ title, copy, children }: { title: string; copy: string; children: React.ReactNode }) { return <main className="authPage"><section className="authPanel"><div className="authIntro"><span className="kicker">ACCOUNT SECURITY</span><h1>{title}</h1><p>{copy}</p></div>{children}</section></main>; }
