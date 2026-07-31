import { getAnalytics, getSources } from "../../lib/api";
import "./admin.css";

export const dynamic = "force-dynamic";

export default async function AdminPage() {
  const [analytics, sources] = await Promise.all([getAnalytics(), getSources()]);
  const healthy = sources.filter((source) => source.health === 0).length;
  return <main className="adminPage">
    <section className="adminHero">
      <div><span className="kicker">CONTROL ROOM / OPERATIONS</span><h1>Source health, ingestion risk and system control.</h1></div>
      <div className="statusSeal"><strong>{healthy}/{sources.length}</strong><span>healthy sources</span></div>
    </section>
    <section className="adminMetrics">
      <article><span>Indexed notices</span><strong>{analytics.total.toLocaleString()}</strong></article>
      <article><span>Disclosed value</span><strong>€{Math.round(analytics.disclosedValue).toLocaleString()}</strong></article>
      <article><span>Markets</span><strong>{Object.keys(analytics.countries).length}</strong></article>
      <article><span>Categories</span><strong>{Object.keys(analytics.categories).length}</strong></article>
    </section>
    <section className="adminTableWrap">
      <div className="sectionHeading"><div><span>Source registry</span><h2>Live ingestion estate</h2></div><p>Operational status, scheduling and failure telemetry for every managed source.</p></div>
      <div className="adminTable">
        <div className="adminRow adminHead"><span>Source</span><span>Market</span><span>Status</span><span>Failures</span><span>Next crawl</span></div>
        {sources.map((source) => <div className="adminRow" key={source.id}><span><b>{source.name}</b><small>{source.key}</small></span><span>{source.countryCode}</span><span><i className={`healthDot health-${source.health}`} />{source.health === 0 ? "Healthy" : source.health === 1 ? "Degraded" : source.health === 2 ? "Failing" : "Disabled"}</span><span>{source.consecutiveFailures}</span><span>{source.nextCrawlAt ? new Date(source.nextCrawlAt).toLocaleString() : "Pending"}</span></div>)}
      </div>
    </section>
  </main>;
}
