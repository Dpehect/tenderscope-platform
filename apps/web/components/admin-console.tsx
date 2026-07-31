'use client';

import { useEffect, useState } from 'react';
import {
  downloadAdminReport,
  getAdminAudit,
  getAdminOrganizations,
  getAdminOverview,
  getAdminSources,
  getAdminUsers,
  runAdminSource,
  updateAdminSource,
  type AdminOverview,
  type AdminSource
} from '../lib/admin-api';

export function AdminConsole() {
  const [overview,setOverview]=useState<AdminOverview|null>(null);
  const [sources,setSources]=useState<AdminSource[]>([]);
  const [organizations,setOrganizations]=useState<any[]>([]);
  const [users,setUsers]=useState<any[]>([]);
  const [audit,setAudit]=useState<any[]>([]);
  const [tab,setTab]=useState<'sources'|'organizations'|'users'|'audit'>('sources');
  const [busy,setBusy]=useState<string|null>(null);
  const [error,setError]=useState<string|null>(null);

  async function load(){
    try{
      const [o,s,org,u,a]=await Promise.all([getAdminOverview(),getAdminSources(),getAdminOrganizations(),getAdminUsers(),getAdminAudit()]);
      setOverview(o);setSources(s);setOrganizations(org);setUsers(u);setAudit(a);
    }catch(e){setError(e instanceof Error?e.message:'Admin console failed to load.');}
  }
  useEffect(()=>{load();},[]);

  async function save(source:AdminSource,enabled=source.isEnabled,interval=source.crawlIntervalMinutes){
    setBusy(source.id);setError(null);
    try{const next=await updateAdminSource(source.id,enabled,interval);setSources(rows=>rows.map(x=>x.id===source.id?next:x));}
    catch(e){setError(e instanceof Error?e.message:'Source update failed.');}
    finally{setBusy(null);}
  }
  async function run(source:AdminSource){
    setBusy(source.id);setError(null);
    try{await runAdminSource(source.id);await load();}
    catch(e){setError(e instanceof Error?e.message:'Source run failed.');}
    finally{setBusy(null);}
  }

  if(!overview)return <div className="adminState"><span className="kicker">CONTROL ROOM</span><h1>Loading operations.</h1>{error&&<p>{error}</p>}</div>;
  return <div className="adminConsole">
    <header className="adminHero"><div><span className="kicker">CONTROL ROOM / OPERATIONS</span><h1>Operate the procurement intelligence estate.</h1></div><div className="statusSeal"><strong>{overview.healthySources}/{overview.sources}</strong><span>healthy sources</span></div></header>
    {error&&<div className="adminError">{error}<button onClick={()=>setError(null)}>Dismiss</button></div>}
    <section className="adminMetrics">
      <article><span>Indexed notices</span><strong>{overview.tenders.toLocaleString()}</strong></article>
      <article><span>Organizations</span><strong>{overview.organizations}</strong></article>
      <article><span>Users</span><strong>{overview.users}</strong></article>
      <article><span>Failing sources</span><strong>{overview.failingSources}</strong></article>
    </section>
    <section className="adminToolbar">
      <div>{(['sources','organizations','users','audit'] as const).map(x=><button key={x} className={tab===x?'active':''} onClick={()=>setTab(x)}>{x}</button>)}</div>
      <div><button onClick={()=>downloadAdminReport('csv')}>CSV</button><button onClick={()=>downloadAdminReport('excel')}>Excel</button><button onClick={()=>downloadAdminReport('pdf')}>PDF</button></div>
    </section>
    {tab==='sources'&&<section className="adminTableWrap"><div className="sectionHeading"><div><span>CRAWLER MANAGEMENT</span><h2>Source registry and scheduling</h2></div></div><div className="adminTable"><div className="adminRow adminHead"><span>Source</span><span>Status</span><span>Interval</span><span>Next crawl</span><span>Actions</span></div>{sources.map(source=><div className="adminRow" key={source.id}><span><b>{source.name}</b><small>{source.key} · {source.countryCode}</small></span><span>{source.isEnabled?'Enabled':'Disabled'} · H{source.health}</span><span><input type="number" defaultValue={source.crawlIntervalMinutes} min={15} onBlur={e=>save(source,source.isEnabled,Number(e.target.value))}/></span><span>{source.nextCrawlAt?new Date(source.nextCrawlAt).toLocaleString():'Pending'}</span><span className="adminActions"><button disabled={busy===source.id} onClick={()=>save(source,!source.isEnabled,source.crawlIntervalMinutes)}>{source.isEnabled?'Disable':'Enable'}</button><button disabled={busy===source.id} onClick={()=>run(source)}>Run</button></span></div>)}</div></section>}
    {tab==='organizations'&&<section className="adminTableWrap"><div className="adminTable"><div className="adminRow adminHead"><span>Organization</span><span>Members</span><span>Workspace</span><span>Status</span><span>Created</span></div>{organizations.map(x=><div className="adminRow" key={x.id}><span><b>{x.name}</b><small>{x.slug}</small></span><span>{x.members}</span><span>{x.workspaceItems}</span><span>{x.isActive?'Active':'Inactive'}</span><span>{new Date(x.createdAt).toLocaleDateString()}</span></div>)}</div></section>}
    {tab==='users'&&<section className="adminTableWrap"><div className="adminTable"><div className="adminRow adminHead"><span>User</span><span>Organizations</span><span>Status</span><span>Last login</span><span>Created</span></div>{users.map(x=><div className="adminRow" key={x.id}><span><b>{x.displayName}</b><small>{x.email}</small></span><span>{x.organizations}</span><span>{x.isActive?'Active':'Inactive'}</span><span>{x.lastLoginAt?new Date(x.lastLoginAt).toLocaleString():'Never'}</span><span>{new Date(x.createdAt).toLocaleDateString()}</span></div>)}</div></section>}
    {tab==='audit'&&<section className="adminTableWrap"><div className="adminTable"><div className="adminRow adminHead"><span>Action</span><span>Resource</span><span>Actor</span><span>Detail</span><span>Time</span></div>{audit.map(x=><div className="adminRow" key={x.id}><span>{x.action}</span><span>{x.resource}</span><span>{x.actorKey}</span><span>{x.detail??'—'}</span><span>{new Date(x.createdAt).toLocaleString()}</span></div>)}</div></section>}
  </div>;
}
