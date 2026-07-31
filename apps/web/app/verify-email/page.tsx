import type { Metadata } from 'next';
import { VerifyEmailForm } from '../../components/account-recovery-form';
import '../auth.css';

export const metadata: Metadata = { title: 'Verify email', robots: { index: false, follow: false } };
export default async function VerifyEmailPage({ searchParams }: { searchParams: Promise<{ token?: string }> }) { const { token = '' } = await searchParams; return <VerifyEmailForm token={token} />; }
