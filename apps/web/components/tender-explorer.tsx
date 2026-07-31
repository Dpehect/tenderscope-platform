'use client';

import Link from "next/link";
import { useMemo, useState } from "react";
import type { SearchResult, Tender } from "../lib/api";

function formatMoney(value?: number, currency?: string) {
  if (!value) return "Value undisclosed";
  return new Intl.NumberFormat("en", { notation: "compact", maximumFractionDigits: 1 }).format(value) + ` ${currency ?? ""}`;
}

function daysUntil(date?: string) {
  if (!date) return null;
  return Math.ceil((new Date(date).getTime() - Date.now()) / 86400000);
}

function TenderRow({ tender, saved, onSave }: { tender: Tender; saved: boolean; onSave: () => void }) {
  const days = daysUntil(tender.deadlineAt);
  return (
    <article className="opportunityRow">
      <div className="rowNumber">{tender.countryCode}</div>
      <div className="rowMain">
        <div className="rowMeta"><span>{tender.category ?? "General procurement"}</span><span>{tender.buyerName}</span></div>
        <h3><Link href={`/opportunities/${encodeURIComponent(tender.id)}`}>{tender.title}</Link></h3>
        <p>{tender.description?.slice(0, 180) || "Public procurement opportunity collected and normalized by TenderScope."}</p>
      </div>
      <div className="rowValue"><strong>{formatMoney(tender.estimatedValue, tender.currency)}</strong><span>{days === null ? "Open deadline" : days < 0 ? "Closed" : `${days} days left`}</span></div>
      <div className="rowActions">
        <Link className="detailButton" href={`/opportunities/${encodeURIComponent(tender.id)}`}>Review</Link>
        <button onClick={onSave} className={saved ? "saveButton saved" : "saveButton"} aria-label={saved ? "Remove from workspace" : "Save to workspace"}>{saved ? "Saved" : "Save"}</button>
        <a href={tender.sourceUrl} target="_blank" rel="noreferrer" aria-label="Open official notice">↗</a>
      </div>
    </article>
  );
}

export function TenderExplorer({ result }: { result: SearchResult }) {
  const [query, setQuery] = useState("");
  const [country, setCountry] = useState("All");
  const [saved, setSaved] = useState<string[]>(() => {
    if (typeof window === "undefined") return [];
    try { return JSON.parse(localStorage.getItem("tenderscope:saved") ?? "[]"); } catch { return []; }
  });

  const filtered = useMemo(() => result.items.filter(item => {
    const text = `${item.title} ${item.buyerName} ${item.category}`.toLowerCase();
    return text.includes(query.toLowerCase()) && (country === "All" || item.countryCode === country);
  }), [result.items, query, country]);

  const toggle = (id: string) => {
    const next = saved.includes(id) ? saved.filter(x => x !== id) : [...saved, id];
    setSaved(next);
    localStorage.setItem("tenderscope:saved", JSON.stringify(next));
  };

  const countries = ["All", ...Object.keys(result.countries).sort()];

  return (
    <section className="explorerSection">
      <div className="explorerControls">
        <label className="searchField"><span>Search intelligence</span><input value={query} onChange={event => setQuery(event.target.value)} placeholder="Cybersecurity, rail, cloud…" /></label>
        <label><span>Market</span><select value={country} onChange={event => setCountry(event.target.value)}>{countries.map(item => <option key={item}>{item}</option>)}</select></label>
        <div className="resultCount"><strong>{filtered.length}</strong><span>visible opportunities</span></div>
      </div>
      <div className="opportunityList">
        {filtered.length ? filtered.map(item => <TenderRow key={item.id} tender={item} saved={saved.includes(item.id)} onSave={() => toggle(item.id)} />) : <div className="emptyState"><span>00</span><h3>No signals matched.</h3><p>Broaden the market or remove a search term.</p></div>}
      </div>
    </section>
  );
}
