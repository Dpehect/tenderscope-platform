'use client';

import { useMemo, useState } from "react";
import type { Tender } from "../lib/api";

const stages = ["Review", "Qualified", "Preparing", "Submitted"] as const;
type Stage = typeof stages[number];

export function WorkspaceBoard({ opportunities }: { opportunities: Tender[] }) {
  const [saved] = useState<string[]>(() => {
    if (typeof window === "undefined") return [];
    try { return JSON.parse(localStorage.getItem("tenderscope:saved") ?? "[]"); } catch { return []; }
  });
  const [stagesById, setStagesById] = useState<Record<string, Stage>>({});
  const [searches, setSearches] = useState(["Cybersecurity in EU", "Digital public services", "Transport data platforms"]);
  const selected = useMemo(() => opportunities.filter(x => saved.includes(x.id)), [opportunities, saved]);
  const move = (id: string, stage: Stage) => setStagesById(current => ({...current,[id]:stage}));

  return <div className="workspaceLayout">
    <aside className="workspaceSidebar">
      <span className="kicker">ACTIVE PROFILE</span>
      <div className="profileCard"><div className="avatar">YG</div><div><strong>Strategy workspace</strong><span>Local demo identity</span></div></div>
      <div className="sidebarSection"><span>SAVED SEARCHES</span>{searches.map(item=><button key={item}>{item}<i>↗</i></button>)}<button className="addSearch" onClick={()=>setSearches([...searches,`New watch ${searches.length+1}`])}>+ Add watch</button></div>
      <div className="workspaceNote"><strong>Zero-key mode</strong><p>Your shortlist is stored in this browser until account authentication is connected during production setup.</p></div>
    </aside>
    <section className="workspaceMain">
      <header className="workspaceHeader"><div><span className="kicker">03 / COMPANY WORKSPACE</span><h1>Move opportunities<br/>toward a decision.</h1></div><div className="workspaceScore"><strong>{selected.length}</strong><span>saved signals</span></div></header>
      <div className="pipeline">
        {stages.map(stage => <section className="pipelineColumn" key={stage}><header><span>{stage}</span><strong>{selected.filter(x => (stagesById[x.id] ?? "Review") === stage).length}</strong></header>{selected.filter(x => (stagesById[x.id] ?? "Review") === stage).map(item=><article className="pipelineCard" key={item.id}><span>{item.countryCode} / {item.category ?? "General"}</span><h3>{item.title}</h3><p>{item.buyerName}</p><select value={stagesById[item.id] ?? "Review"} onChange={event=>move(item.id,event.target.value as Stage)}>{stages.map(s=><option key={s}>{s}</option>)}</select></article>)}{selected.filter(x => (stagesById[x.id] ?? "Review") === stage).length===0&&<div className="columnEmpty">No opportunities</div>}</section>)}
      </div>
    </section>
  </div>;
}
