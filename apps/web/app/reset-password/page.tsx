import type { Metadata } from 'next';
import { ResetPasswordForm } from '../../components/account-recovery-form';
import '../auth.css';

export const metadata: Metadata = { title: 'Reset password', robots: { index: false, follow: false } };
export default async function ResetPasswordPage({ searchParams }: { searchParams: Promise<{ token?: string }> }) { const { token = '' } = await searchParams; return <ResetPasswordForm token={token} />; }
