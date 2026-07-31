export type Tender = {
  id: string;
  title: string;
  buyerName: string;
  countryCode: string;
  region?: string;
  description?: string;
  category?: string;
  estimatedValue?: number;
  currency?: string;
  publishedAt: string;
  deadlineAt?: string;
  sourceUrl: string;
};

export type SearchResult = {
  items: Tender[];
  total: number;
  page: number;
  pageSize: number;
  countries: Record<string, number>;
  categories: Record<string, number>;
};

export type PlatformStats = {
  totalTenders: number;
  totalSources: number;
  healthySources: number;
  generatedAt: string;
};

export type TenderSource = {
  id: string;
  key: string;
  name: string;
  countryCode: string;
  health: number;
  consecutiveFailures: number;
  nextCrawlAt?: string;
  lastSuccessAt?: string;
  lastError?: string;
};

export type AnalyticsSnapshot = {
  total: number;
  disclosedValue: number;
  countries: Record<string, number>;
  categories: Record<string, number>;
};

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";

async function request<T>(path: string, fallback: T): Promise<T> {
  try {
    const response = await fetch(`${API_URL}${path}`, { cache: "no-store" });
    if (!response.ok) return fallback;
    return (await response.json()) as T;
  } catch {
    return fallback;
  }
}

export function getOpportunities(query = "pageSize=24") {
  return request<SearchResult>(`/api/tenders?${query}`, {
    items: [], total: 0, page: 1, pageSize: 24, countries: {}, categories: {}
  });
}

export async function getOpportunity(id: string) {
  return request<Tender | null>(`/api/tenders/${encodeURIComponent(id)}`, null);
}

export function getStats() {
  return request<PlatformStats>("/api/stats", {
    totalTenders: 0, totalSources: 0, healthySources: 0, generatedAt: new Date(0).toISOString()
  });
}

export function getSources() {
  return request<TenderSource[]>("/api/sources", []);
}

export async function getAnalytics(): Promise<AnalyticsSnapshot> {
  const result = await getOpportunities("page=1&pageSize=100&sort=value-desc");
  return {
    total: result.total,
    disclosedValue: result.items.reduce((sum, item) => sum + (item.estimatedValue ?? 0), 0),
    countries: result.countries,
    categories: result.categories
  };
}
