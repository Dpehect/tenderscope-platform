'use client';

import { useEffect, useState } from 'react';
import { getOrganizationAnalytics, type OrganizationAnalytics } from '../lib/intelligence-api';

export function OrganizationAnalyticsDashboard() {
  const [data, setData] = useState<OrganizationAnalytics | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => { getOrganizationAnalytics().then(setData).catch(cause => setError(cause instanceof Error ? cause.message : 'Analytics could not be loaded.')); }, []);
  if (error) return <div className="orgAnalyticsState"><span className="kicker">ORGANIZATION ANALYTICS</span><h1>Data unavailable.</h1><p>{error}</p></div>;
  if (!data) return <div className="orgAnalyticsState"><span className="kicker">ORGANIZATION ANALYTICS</span><h1>Loading performance.</h1></div>;
  const max = Math.max(...data.pipeline.map(x => x.count), 1);

  return <div className="orgAnalytics">
    <header><div><span className="kicker">ORGANIZATION ANALYTICS</span><h1>Pipeline health at a glance.</h1><p>Live conversion, workload and deadline pressure for the active organization.</p></div><div className="winMetric"><strong>{data.winRate}%</strong><span>win rate</span></div></header>
    <section className="orgMetricGrid">
      <Metric label="Total opportunities" value={data.total}/><Metric label="Active pipeline" value={data.active}/><Metric label="Won" value={data.won}/><Metric label="Lost" value={data.lost}/><Metric label="Due in 7 days" value={data.dueNext7Days}/><Metric label="Overdue" value={data.overdue}/>
    </section>
    <section className="orgAnalyticsGrid">
      <article className="orgPanel"><header><span>PIPELINE</span><h2>Stage distribution</h2></header><div className="pipelineBars">{data.pipeline.map(row => <div key={row.stage}><span>{row.stage}</span><i><b style={{ width: `${(row.count / max) * 100}%` }}/></i><strong>{row.count}</strong></div>)}</div></article>
      <article className="orgPanel"><header><span>TEAM LOAD</span><h2>Assigned opportunities</h2></header><div className="workloadList">{data.workload.length === 0 ? <p>No members found.</p> : data.workload.map(row => <div key={row.id}><div><strong>{row.displayName}</strong><span>{row.role}</span></div><b>{row.assigned}</b><small>{row.overdue} overdue</small></div>)}</div></article>
    </section>
  </div>;
}

function Metric({ label, value }: { label: string; value: number }) { return <article><span>{label}</span><strong>{value}</strong></article>; }
