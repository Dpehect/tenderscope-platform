import type { Metadata } from 'next';
import { ForgotPasswordForm } from '../../components/account-recovery-form';
import '../auth.css';

export const metadata: Metadata = { title: 'Forgot password', robots: { index: false, follow: false } };
export default function ForgotPasswordPage() { return <ForgotPasswordForm />; }
