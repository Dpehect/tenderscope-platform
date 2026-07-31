import type { Metadata } from 'next';
import { Suspense } from 'react';
import { AuthForm } from '../../components/auth-form';
import '../auth.css';

export const metadata: Metadata = { title: 'Sign in', description: 'Sign in to your TenderScope organization workspace.' };

export default function LoginPage() {
  return <main className="authPage"><Suspense fallback={<div className="authLoading">Loading…</div>}><AuthForm mode="login" /></Suspense></main>;
}
