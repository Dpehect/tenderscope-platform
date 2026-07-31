import type { Metadata } from "next";
import Link from "next/link";
import { getOpportunity } from "../../../lib/api";
import "./detail.css";

const siteUrl = process.env.NEXT_PUBLIC_SITE_URL ?? "https://tenderscope-platform.vercel.app";

type PageProps = { params: Promise<{ id: string }> };

function formatDate(value?: string) {
  if (!value) return "Not disclosed";
  return new Intl.DateTimeFormat("en", { dateStyle: "long" }).format(new Date(value));
}

function formatMoney(value?: number, currency?: string) {
  if (!value) return "Not disclosed";
  return new Intl.NumberFormat("en", { style: "currency", currency: currency || "EUR", maximumFractionDigits: 0 }).format(value);
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { id } = await params;
  const tender = await getOpportunity(decodeURIComponent(id));
  if (!tender) return { title: "Opportunity not found", robots: { index: false, follow: false } };

  const canonical = `/opportunities/${encodeURIComponent(tender.id)}`;
  return {
    title: tender.title,
    description: tender.description?.slice(0, 155) ?? `${tender.buyerName} public procurement opportunity.`,
    alternates: { canonical },
    openGraph: {
      title: tender.title,
      description: tender.description?.slice(0, 180) ?? `${tender.buyerName} procurement opportunity.`,
      url: new URL(canonical, siteUrl).toString(),
      type: "article"
    }
  };
}

export default async function OpportunityDetailPage({ params }: PageProps) {
  const { id } = await params;
  const tender = await getOpportunity(decodeURIComponent(id));

  if (!tender) {
    return <main className="detailPage detailMissing">
      <div><span className="kicker">SIGNAL NOT FOUND</span><h1>This opportunity is unavailable.</h1><p>It may have expired, moved at source or not completed synchronization.</p><Link href="/opportunities">Return to opportunity index</Link></div>
    </main>;
  }

  return <main className="detailPage">
    <Link className="detailBack" href="/opportunities">← Back to opportunity index</Link>
    <section className="detailHero">
      <div>
        <span className="kicker">{tender.countryCode} / {tender.category ?? "PUBLIC PROCUREMENT"}</span>
        <h1>{tender.title}</h1>
        <p className="detailSummary">{tender.description || "This public procurement notice was collected from an official source and normalized by TenderScope for qualification and review."}</p>
      </div>
      <aside className="detailAside">
        <div className="detailMetric"><span>Buyer</span><strong>{tender.buyerName}</strong></div>
        <div className="detailMetric"><span>Estimated value</span><strong>{formatMoney(tender.estimatedValue, tender.currency)}</strong></div>
        <div className="detailMetric"><span>Deadline</span><strong>{formatDate(tender.deadlineAt)}</strong></div>
        <div className="detailActions">
          <a className="primary" href={tender.sourceUrl} target="_blank" rel="noopener noreferrer">Open official notice ↗</a>
          <Link href="/workspace">Review in workspace</Link>
        </div>
      </aside>
    </section>
    <section className="detailGrid">
      <article className="detailPanel">
        <span>OPPORTUNITY BRIEF</span>
        <h2>Decision context</h2>
        <p>{tender.description || "No extended summary was disclosed by the source. Review the official notice for complete eligibility, scope and submission requirements."}</p>
      </article>
      <aside className="detailPanel detailFacts">
        <span>NOTICE FACTS</span>
        <h2>Source record</h2>
        <div className="detailFact"><span>Market</span><strong>{tender.countryCode}</strong></div>
        <div className="detailFact"><span>Region</span><strong>{tender.region || "Not specified"}</strong></div>
        <div className="detailFact"><span>Published</span><strong>{formatDate(tender.publishedAt)}</strong></div>
        <div className="detailFact"><span>Deadline</span><strong>{formatDate(tender.deadlineAt)}</strong></div>
        <div className="detailFact"><span>Category</span><strong>{tender.category || "General procurement"}</strong></div>
        <div className="detailFact"><span>Record ID</span><strong>{tender.id}</strong></div>
      </aside>
    </section>
  </main>;
}
