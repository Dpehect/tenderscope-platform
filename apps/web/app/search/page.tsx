import type { Metadata } from 'next';
import { GlobalSearch } from '../../components/global-search';
import { WorkspaceAuthGate } from '../../components/workspace-auth-gate';
import './search.css';

export const metadata: Metadata = { title: 'Global Search', description: 'Search tenders, workspace records and watchlists.', robots: { index: false, follow: false } };

export default function SearchPage() {
  return <main className="globalSearchPage"><WorkspaceAuthGate next="/search" label="SECURE SEARCH"><GlobalSearch/></WorkspaceAuthGate></main>;
}
