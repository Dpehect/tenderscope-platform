'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { getTenderIntelligence, type TenderIntelligence } from '../lib/intelligence-api';

export function TenderIntelligencePanel({ id }: { id: string }) {
  const [data, setData] = useState<TenderIntelligence | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => { getTenderIntelligence(id).then(setData).catch(cause => setError(cause instanceof Error ? cause.message : 'Intelligence could not be loaded.')); }, [id]);
  if (error) return <div className="intelligenceState"><h1>Intelligence unavailable.</h1><p>{error}</p></div>;
  if (!data) return <div className="intelligenceState"><h1>Building intelligence profile.</h1></div>;

  return <div className="tenderIntelligence">
    <header><div><span className="kicker">TENDER INTELLIGENCE</span><h1>{data.tender.title}</h1><p>{data.tender.buyerName} · {data.tender.countryCode} · {data.tender.category ?? 'Uncategorized'}</p></div><div className="fitScore"><strong>{data.score}</strong><span>opportunity score</span><small>{data.riskScore} risk</small></div></header>
    <section className="intelligenceMetrics"><article><span>Buyer notices</span><strong>{data.buyer.notices}</strong></article><article><span>Buyer disclosed value</span><strong>{formatMoney(data.buyer.disclosedValue, data.tender.currency)}</strong></article><article><span>Category average</span><strong>{formatMoney(data.category.averageValue, data.tender.currency)}</strong></article><article><span>Comparable sample</span><strong>{data.category.sampleSize}</strong></article></section>
    <section className="intelligenceGrid">
      <article className="intelligencePanel"><header><span>RISK REVIEW</span><h2>Decision factors</h2></header>{data.risks.length === 0 ? <p>No material risk flags detected.</p> : <ul>{data.risks.map(risk => <li key={risk}>{risk}</li>)}</ul>}</article>
      <article className="intelligencePanel"><header><span>SIMILAR NOTICES</span><h2>Comparable opportunities</h2></header><div className="similarList">{data.similar.length === 0 ? <p>No comparable notices found.</p> : data.similar.map(item => <Link key={item.id} href={`/intelligence/${item.id}`}><div><strong>{item.title}</strong><span>{item.buyerName}</span></div><small>{item.countryCode} · {item.category ?? 'Other'}</small></Link>)}</div></article>
    </section>
  </div>;
}

function formatMoney(value: number, currency?: string) { if (!value) return '—'; return new Intl.NumberFormat('en', { notation: 'compact', maximumFractionDigits: 1, style: currency ? 'currency' : 'decimal', currency: currency || undefined }).format(value); }
