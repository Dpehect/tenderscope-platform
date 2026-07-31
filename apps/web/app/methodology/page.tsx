import type { Metadata } from "next";
import { getSources } from "../../lib/api";
import "./methodology.css";

export const metadata: Metadata = {
  title: "Data methodology",
  description: "How TenderScope sources, normalizes and verifies public procurement opportunities.",
  alternates: { canonical: "/methodology" }
};

const principles = [
  ["01", "Official by default", "Records originate from openly accessible institutional procurement systems. Every opportunity retains a link to its source notice."],
  ["02", "Canonical structure", "Dates, buyers, markets, categories, values and currencies are mapped into a consistent tender model without rewriting the legal notice."],
  ["03", "Deterministic identity", "Source identifiers and content fingerprints prevent duplicate notices while preserving subsequent observations and updates."],
  ["04", "Operational transparency", "Source health, crawl history, failures and dead-letter records remain inspectable by authorized operators."],
] as const;

export default async function MethodologyPage() {
  const sources = await getSources();
  return <main className="methodologyPage">
    <section className="methodologyHero">
      <span className="kicker">DATA TRUST / METHODOLOGY</span>
      <h1>Evidence before<br/><em>prediction.</em></h1>
      <p>TenderScope is designed as a traceable intelligence layer, not an opaque tender marketplace. The official notice remains the final authority.</p>
    </section>
    <section className="methodologyPrinciples">
      {principles.map(([index, title, body]) => <article key={index}><span>{index}</span><h2>{title}</h2><p>{body}</p></article>)}
    </section>
    <section className="sourceRegistry">
      <header><div><span className="kicker">SOURCE REGISTRY</span><h2>Connected public systems.</h2></div><p>Health reflects the most recent ingestion state. A degraded source never removes its existing traceable records.</p></header>
      <div className="sourceTable">
        <div className="sourceRow sourceHead"><span>Source</span><span>Market</span><span>Status</span><span>Last successful sync</span></div>
        {sources.length ? sources.map(source => <div className="sourceRow" key={source.id}><strong>{source.name}</strong><span>{source.countryCode}</span><span className={source.health === 1 ? "healthy" : "degraded"}>{source.health === 1 ? "Operational" : "Monitored"}</span><span>{source.lastSuccessAt ? new Date(source.lastSuccessAt).toLocaleString("en", { dateStyle: "medium", timeStyle: "short" }) : "Awaiting first sync"}</span></div>) : <div className="sourceEmpty">Source status becomes available when the API is connected.</div>}
      </div>
    </section>
    <section className="methodologyNotice"><span>IMPORTANT</span><p>TenderScope supports discovery and qualification. Teams must verify deadlines, eligibility, documents and submission rules on the linked official source before acting.</p></section>
  </main>;
}
