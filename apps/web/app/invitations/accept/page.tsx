import type { Metadata } from 'next';
import { InvitationAcceptance } from '../../../components/invitation-acceptance';
import { WorkspaceAuthGate } from '../../../components/workspace-auth-gate';
import './invitation.css';

export const metadata: Metadata = {
  title: 'Accept invitation',
  description: 'Join a TenderScope organization workspace.',
  robots: { index: false, follow: false }
};

export default function AcceptInvitationPage() {
  return <main className="invitationAcceptPage">
    <WorkspaceAuthGate next="/invitations/accept" label="ORGANIZATION INVITATION">
      <InvitationAcceptance />
    </WorkspaceAuthGate>
  </main>;
}
