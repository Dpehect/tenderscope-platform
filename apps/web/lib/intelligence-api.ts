'use client';

import { authRequest } from './auth-api';

export type OrganizationAnalytics = {
  total: number; active: number; won: number; lost: number; winRate: number;
  dueNext7Days: number; overdue: number;
  pipeline: { stage: string; count: number }[];
  workload: { id: string; displayName: string; role: string; assigned: number; overdue: number }[];
};

export type GlobalSearchItem = {
  type: 'tender' | 'workspace' | 'watchlist'; id: string; title: string; subtitle: string;
  countryCode?: string | null; category?: string | null; deadlineAt?: string | null;
  estimatedValue?: number | null; currency?: string | null; href: string;
};

export type GlobalSearchResult = { query: string; tenders: GlobalSearchItem[]; workspace: GlobalSearchItem[]; watchlists: GlobalSearchItem[] };

export type TenderIntelligence = {
  tender: { id: string; title: string; buyerName: string; countryCode: string; category?: string; estimatedValue?: number; currency?: string; publishedAt: string; deadlineAt?: string };
  score: number; riskScore: number; risks: string[];
  buyer: { notices: number; disclosedValue: number; lastPublishedAt: string };
  category: { averageValue: number; sampleSize: number };
  similar: { id: string; title: string; buyerName: string; countryCode: string; category?: string; estimatedValue?: number; currency?: string; deadlineAt?: string }[];
};

export function getOrganizationAnalytics() { return authRequest<OrganizationAnalytics>('/api/analytics/organization'); }
export function globalSearch(query: string, country = '', category = '') {
  const params = new URLSearchParams({ q: query });
  if (country) params.set('country', country);
  if (category) params.set('category', category);
  return authRequest<GlobalSearchResult>(`/api/search/global?${params.toString()}`);
}
export function getTenderIntelligence(id: string) { return authRequest<TenderIntelligence>(`/api/intelligence/tenders/${id}`); }
