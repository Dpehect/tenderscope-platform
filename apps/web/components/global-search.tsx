'use client';

import Link from 'next/link';
import { FormEvent, useState } from 'react';
import { globalSearch, type GlobalSearchResult } from '../lib/intelligence-api';

export function GlobalSearch() {
  const [query, setQuery] = useState('');
  const [country, setCountry] = useState('');
  const [category, setCategory] = useState('');
  const [result, setResult] = useState<GlobalSearchResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError(null);
    try { setResult(await globalSearch(query, country, category)); }
    catch (cause) { setError(cause instanceof Error ? cause.message : 'Search failed.'); }
    finally { setBusy(false); }
  }

  return <div className="globalSearch">
    <header><span className="kicker">GLOBAL SEARCH</span><h1>Find every signal.</h1><p>Search public notices, active workspace items and organization watchlists from one surface.</p></header>
    <form onSubmit={submit}>
      <label className="searchMain">Search<input value={query} onChange={event => setQuery(event.target.value)} placeholder="Buyer, tender, category or keyword" autoFocus/></label>
      <label>Country<input value={country} onChange={event => setCountry(event.target.value.toUpperCase())} maxLength={3} placeholder="EU"/></label>
      <label>Category<input value={category} onChange={event => setCategory(event.target.value)} placeholder="Technology"/></label>
      <button disabled={busy}>{busy ? 'Searching…' : 'Search'}</button>
    </form>
    {error && <div className="searchError">{error}</div>}
    {result && <section className="searchResults">
      <ResultGroup title="Public opportunities" items={result.tenders}/>
      <ResultGroup title="Workspace" items={result.workspace}/>
      <ResultGroup title="Watchlists" items={result.watchlists}/>
    </section>}
  </div>;
}

function ResultGroup({ title, items }: { title: string; items: GlobalSearchResult['tenders'] }) {
  return <article className="searchGroup"><header><h2>{title}</h2><span>{items.length}</span></header>{items.length === 0 ? <p>No matching records.</p> : items.map(item => <Link href={item.href} key={`${item.type}-${item.id}`}><div><span>{item.type}</span><strong>{item.title}</strong><small>{item.subtitle}</small></div><div className="searchMeta"><span>{item.countryCode ?? '—'}</span><span>{item.category ?? 'Uncategorized'}</span>{item.deadlineAt && <time>{new Date(item.deadlineAt).toLocaleDateString()}</time>}</div></Link>)}</article>;
}
