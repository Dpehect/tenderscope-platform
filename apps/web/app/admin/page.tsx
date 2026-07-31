import type { Metadata } from 'next';
import { AdminConsole } from '../../components/admin-console';
import { WorkspaceAuthGate } from '../../components/workspace-auth-gate';
import './admin.css';

export const dynamic = 'force-dynamic';
export const metadata: Metadata = { title: 'Admin Console', robots: { index: false, follow: false } };

export default function AdminPage() {
  return <main className="adminPage"><WorkspaceAuthGate next="/admin" label="ADMIN CONSOLE"><AdminConsole /></WorkspaceAuthGate></main>;
}
