type Tender = { id: string; title: string; buyerName: string; countryCode: string; category?: string; estimatedValue?: number; currency?: string; deadlineAt?: string; sourceUrl: string };

async function getTenders(): Promise<Tender[]> {
  const api = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";
  try {
    const response = await fetch(`${api}/api/tenders?take=30`, { cache: "no-store" });
    if (!response.ok) return [];
    return response.json();
  } catch { return []; }
}

export default async function Home() {
  const tenders = await getTenders();
  return <main>
    <header className="hero">
      <div className="eyebrow">PUBLIC PROCUREMENT INTELLIGENCE</div>
      <h1>Find serious public-sector opportunities before they disappear.</h1>
      <p>TenderScope collects, normalizes and verifies open tender data without paid data providers.</p>
    </header>
    <section className="metrics"><article><strong>{tenders.length}</strong><span>active records</span></article><article><strong>0</strong><span>paid APIs</span></article><article><strong>24/7</strong><span>source monitoring</span></article></section>
    <section className="panel"><div className="panelHeader"><h2>Latest opportunities</h2><span>Normalized feed</span></div>
      <div className="grid">{tenders.length === 0 ? <div className="empty">The crawler worker has not completed its first cycle yet.</div> : tenders.map(tender => <a className="card" href={tender.sourceUrl} key={tender.id} target="_blank" rel="noreferrer"><div className="meta"><span>{tender.countryCode}</span><span>{tender.category ?? "General"}</span></div><h3>{tender.title}</h3><p>{tender.buyerName}</p><footer><span>{tender.estimatedValue ? `${tender.estimatedValue.toLocaleString()} ${tender.currency ?? ""}` : "Value undisclosed"}</span><span>{tender.deadlineAt ? new Date(tender.deadlineAt).toLocaleDateString() : "Open"}</span></footer></a>)}</div>
    </section>
  </main>;
}
