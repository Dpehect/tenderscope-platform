import { TenderExplorer } from "../../components/tender-explorer";
import { getOpportunities } from "../../lib/api";

export const metadata = { title: "Opportunities — TenderScope", description: "Search normalized public procurement opportunities." };

export default async function OpportunitiesPage() {
  const result = await getOpportunities("pageSize=80&sort=deadline-asc");
  return <main className="pageFrame">
    <section className="pageIntro splitIntro">
      <div><span className="kicker">01 / OPPORTUNITY INDEX</span><h1>Signals worth<br/>acting on.</h1></div>
      <div className="introAside"><p>Search normalized public procurement notices across markets, buyers and sectors. Every record links back to its official source.</p><div className="microStats"><span><strong>{result.total}</strong> indexed</span><span><strong>{Object.keys(result.countries).length}</strong> markets</span></div></div>
    </section>
    <TenderExplorer result={result} />
  </main>;
}
