import type { Metadata } from 'next';
import { OrganizationAnalyticsDashboard } from '../../../components/organization-analytics-dashboard';
import { WorkspaceAuthGate } from '../../../components/workspace-auth-gate';
import './organization-analytics.css';

export const metadata: Metadata = { title: 'Organization Analytics', description: 'Pipeline and team performance analytics.', robots: { index: false, follow: false } };

export default function OrganizationAnalyticsPage() {
  return <main className="organizationAnalyticsPage"><WorkspaceAuthGate next="/analytics/organization" label="SECURE ANALYTICS"><OrganizationAnalyticsDashboard/></WorkspaceAuthGate></main>;
}
