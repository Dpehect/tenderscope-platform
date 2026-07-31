type Tender = { id: string; title: string; buyerName: string; countryCode: string; category?: string; estimatedValue?: number; currency?: string; deadlineAt?: string; sourceUrl: string };
type SearchResult = { items: Tender[]; total: number; countries: Record<string, number>; categories: Record<string, number> };

async function getTenders(): Promise<SearchResult> {
  const api = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";
  try {
    const response = await fetch(`${api}/api/tenders?page=1&pageSize=30&sort=published-desc`, { cache: "no-store" });
    if (!response.ok) return { items: [], total: 0, countries: {}, categories: {} };
    return response.json();
  } catch { return { items: [], total: 0, countries: {}, categories: {} }; }
}

export default async function Home() {
  const result = await getTenders();
  const tenders = result.items;
  return <main>
    <header className="hero">
      <div className="eyebrow">PUBLIC PROCUREMENT INTELLIGENCE</div>
      <h1>Find serious public-sector opportunities before they disappear.</h1>
      <p>TenderScope collects, normalizes and verifies open tender data without paid data providers.</p>
    </header>
    <section className="metrics"><article><strong>{result.total}</strong><span>normalized records</span></article><article><strong>{Object.keys(result.countries).length}</strong><span>countries indexed</span></article><article><strong>{Object.keys(result.categories).length}</strong><span>market categories</span></article></section>
    <section className="panel"><div className="panelHeader"><h2>Latest opportunities</h2><span>Faceted open-data feed</span></div>
      <div className="grid">{tenders.length === 0 ? <div className="empty">The crawler worker has not completed its first cycle yet.</div> : tenders.map(tender => <a className="card" href={tender.sourceUrl} key={tender.id} target="_blank" rel="noreferrer"><div className="meta"><span>{tender.countryCode}</span><span>{tender.category ?? "General"}</span></div><h3>{tender.title}</h3><p>{tender.buyerName}</p><footer><span>{tender.estimatedValue ? `${tender.estimatedValue.toLocaleString()} ${tender.currency ?? ""}` : "Value undisclosed"}</span><span>{tender.deadlineAt ? new Date(tender.deadlineAt).toLocaleDateString() : "Open"}</span></footer></a>)}</div>
    </section>
  </main>;
}
