import type { Metadata } from 'next';
import { OrganizationSettings } from '../../../components/organization-settings';
import { WorkspaceAuthGate } from '../../../components/workspace-auth-gate';
import './organization.css';

export const metadata: Metadata = {
  title: 'Organization settings',
  description: 'Manage TenderScope organization members, roles and invitations.',
  robots: { index: false, follow: false }
};

export default function OrganizationSettingsPage() {
  return <main className="organizationPage">
    <WorkspaceAuthGate next="/settings/organization" label="SECURE SETTINGS">
      <OrganizationSettings />
    </WorkspaceAuthGate>
  </main>;
}
