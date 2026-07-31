import Link from "next/link";
import { getOpportunities, getStats } from "../lib/api";

export default async function Home() {
  const [result, stats] = await Promise.all([getOpportunities("pageSize=6&sort=deadline-asc"), getStats()]);
  return <main className="homePage">
    <section className="homeHero">
      <div className="heroTopline"><span>PUBLIC PROCUREMENT / OPEN INTELLIGENCE</span><span>EU + GLOBAL SOURCES</span></div>
      <div className="heroGrid">
        <h1>Turn public demand<br/>into <em>qualified</em><br/>opportunity.</h1>
        <div className="heroAside"><p>TenderScope transforms fragmented official notices into a traceable, searchable decision system for serious teams.</p><Link href="/opportunities" className="primaryCta">Enter opportunity index <span>↗</span></Link></div>
      </div>
      <div className="heroTicker"><span>LIVE INDEX</span><strong>{stats.totalTenders.toLocaleString()}</strong><i/> <span>HEALTHY SOURCES</span><strong>{stats.healthySources}/{stats.totalSources}</strong><i/> <span>MARKETS</span><strong>{Object.keys(result.countries).length}</strong></div>
    </section>
    <section className="thesisSection">
      <div className="sectionLabel">WHY TENDERSCOPE</div>
      <div><h2>Less noise.<br/>More conviction.</h2><p>Every opportunity is normalized, deduplicated and linked to its official origin. No paid data gate. No opaque recommendation layer.</p></div>
      <div className="thesisSteps"><article><span>01</span><strong>Discover</strong><p>Collect from official and openly accessible procurement sources.</p></article><article><span>02</span><strong>Normalize</strong><p>Align markets, dates, categories, values and institutional names.</p></article><article><span>03</span><strong>Decide</strong><p>Shortlist, qualify and move the right notices through your pipeline.</p></article></div>
    </section>
    <section className="featuredSection">
      <header><div><span className="kicker">SELECTED SIGNALS</span><h2>Open now.</h2></div><Link href="/opportunities">View full index ↗</Link></header>
      <div className="featuredGrid">{result.items.map((item,index)=><Link href={`/opportunities/${encodeURIComponent(item.id)}`} className="featuredCard" key={item.id}><span className="featuredIndex">0{index+1}</span><div className="featuredMeta"><span>{item.countryCode}</span><span>{item.category ?? "General"}</span></div><h3>{item.title}</h3><p>{item.buyerName}</p><footer><span>{item.estimatedValue ? `${new Intl.NumberFormat("en",{notation:"compact"}).format(item.estimatedValue)} ${item.currency ?? ""}` : "Value undisclosed"}</span><span>{item.deadlineAt ? new Date(item.deadlineAt).toLocaleDateString("en",{month:"short",day:"numeric"}) : "Open"}</span></footer></Link>)}</div>
    </section>
    <section className="closingStatement"><span>BUILD A BETTER PIPELINE</span><h2>The next public contract<br/>should not be hidden<br/>in a bad website.</h2><Link href="/workspace">Start qualifying opportunities <span>↗</span></Link></section>
  </main>;
}
