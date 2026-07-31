import type { Metadata } from 'next';
import { TenderIntelligencePanel } from '../../../components/tender-intelligence';
import { WorkspaceAuthGate } from '../../../components/workspace-auth-gate';
import './intelligence.css';

export const metadata: Metadata = { title: 'Tender Intelligence', description: 'Risk, buyer and comparable tender intelligence.', robots: { index: false, follow: false } };

export default async function TenderIntelligencePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <main className="tenderIntelligencePage"><WorkspaceAuthGate next={`/intelligence/${id}`} label="SECURE INTELLIGENCE"><TenderIntelligencePanel id={id}/></WorkspaceAuthGate></main>;
}
