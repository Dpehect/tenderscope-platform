import { getOpportunities, getStats } from "../../lib/api";

export const metadata = { title: "Intelligence — TenderScope", description: "Public procurement market intelligence and source health." };

function Bars({ data }: { data: Record<string, number> }) {
  const entries = Object.entries(data).sort((a,b) => b[1]-a[1]).slice(0,8);
  const max = Math.max(...entries.map(x => x[1]), 1);
  return <div className="barChart">{entries.map(([label,value],index) => <div className="barRow" key={label}><span className="barIndex">0{index+1}</span><strong>{label}</strong><div className="barTrack"><i style={{width:`${(value/max)*100}%`}} /></div><span>{value}</span></div>)}</div>;
}

export default async function AnalyticsPage() {
  const [stats, result] = await Promise.all([getStats(), getOpportunities("pageSize=100")]);
  const disclosed = result.items.filter(x => x.estimatedValue).reduce((sum,x) => sum + (x.estimatedValue ?? 0),0);
  return <main className="pageFrame analyticsPage">
    <section className="pageIntro"><span className="kicker">02 / MARKET INTELLIGENCE</span><h1>A clearer view of<br/>public demand.</h1><p className="lede">Live distribution, source quality and opportunity density from the normalized TenderScope index.</p></section>
    <section className="metricRibbon">
      <article><span>Indexed notices</span><strong>{stats.totalTenders.toLocaleString()}</strong><small>normalized records</small></article>
      <article><span>Source network</span><strong>{stats.healthySources}/{stats.totalSources}</strong><small>healthy collectors</small></article>
      <article><span>Visible value</span><strong>{new Intl.NumberFormat("en",{notation:"compact",maximumFractionDigits:1}).format(disclosed)}</strong><small>disclosed contract value</small></article>
      <article><span>Market reach</span><strong>{Object.keys(result.countries).length}</strong><small>countries represented</small></article>
    </section>
    <section className="analyticsGrid">
      <article className="dataPanel wide"><header><span>MARKET DENSITY</span><h2>Opportunity volume by country</h2></header><Bars data={result.countries}/></article>
      <article className="dataPanel"><header><span>SECTOR SIGNAL</span><h2>Demand categories</h2></header><div className="categoryCloud">{Object.entries(result.categories).sort((a,b)=>b[1]-a[1]).slice(0,10).map(([key,value])=><div key={key}><span>{key}</span><strong>{value}</strong></div>)}</div></article>
      <article className="dataPanel manifesto"><span>OPERATING PRINCIPLE</span><blockquote>“Source truth first. Every signal remains traceable to the official notice.”</blockquote><p>Generated {new Date(stats.generatedAt).toLocaleDateString("en",{dateStyle:"long"})}</p></article>
    </section>
  </main>;
}
