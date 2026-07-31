import type { Metadata } from 'next';
import { NotificationCenter } from '../../components/notification-center';
import { WorkspaceAuthGate } from '../../components/workspace-auth-gate';
import './notifications.css';

export const metadata: Metadata = {
  title: 'Notifications',
  description: 'TenderScope watchlist matches and workspace notifications.',
  robots: { index: false, follow: false }
};

export default function NotificationsPage() {
  return <main className="notificationsPage"><WorkspaceAuthGate next="/notifications" label="SECURE NOTIFICATIONS"><NotificationCenter /></WorkspaceAuthGate></main>;
}
