import type { Metadata } from 'next';
import { Suspense } from 'react';
import { AuthForm } from '../../components/auth-form';
import '../auth.css';

export const metadata: Metadata = { title: 'Create account', description: 'Create a secure TenderScope organization workspace.' };

export default function RegisterPage() {
  return <main className="authPage"><Suspense fallback={<div className="authLoading">Loading…</div>}><AuthForm mode="register" /></Suspense></main>;
}
