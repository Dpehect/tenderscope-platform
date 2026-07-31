'use client';

import { useEffect, useMemo, useState } from "react";
import type { Tender } from "../lib/api";
import {
  createWorkspaceItem,
  getAccessToken,
  listOrganizationMembers,
  listWorkspaceActivities,
  listWorkspaceItems,
  moveWorkspaceItem,
  opportunityStages,
  updateWorkspaceDetails,
  type OpportunityStage,
  type OrganizationMember,
  type WorkspaceActivity,
  type WorkspaceItem
} from "../lib/workspace-api";

type DetailDraft = {
  notes: string;
  tags: string;
  internalDeadline: string;
  assigneeUserId: string;
};

const emptyDraft: DetailDraft = { notes: "", tags: "", internalDeadline: "", assigneeUserId: "" };

export function WorkspaceBoard({ opportunities }: { opportunities: Tender[] }) {
  const [items, setItems] = useState<WorkspaceItem[]>([]);
  const [members, setMembers] = useState<OrganizationMember[]>([]);
  const [activities, setActivities] = useState<WorkspaceActivity[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [draggedId, setDraggedId] = useState<string | null>(null);
  const [draft, setDraft] = useState<DetailDraft>(emptyDraft);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [authRequired, setAuthRequired] = useState(false);

  const tenderById = useMemo(() => new Map(opportunities.map(item => [item.id, item])), [opportunities]);
  const selected = items.find(item => item.id === selectedId) ?? null;
  const selectedTender = selected ? tenderById.get(selected.tenderId) : null;

  useEffect(() => {
    let cancelled = false;
    async function load() {
      if (!getAccessToken()) {
        setAuthRequired(true);
        setLoading(false);
        return;
      }
      try {
        const [workspaceItems, organizationMembers, feed] = await Promise.all([
          listWorkspaceItems(),
          listOrganizationMembers(),
          listWorkspaceActivities()
        ]);
        if (cancelled) return;

        let resolvedItems = workspaceItems;
        if (workspaceItems.length === 0) {
          const legacyIds = readLegacySavedIds();
          const importable = legacyIds.filter(id => tenderById.has(id)).slice(0, 50);
          if (importable.length > 0) {
            const imported = await Promise.all(importable.map(id => createWorkspaceItem(id)));
            resolvedItems = imported;
          }
        }

        setItems(resolvedItems);
        setMembers(organizationMembers.filter(member => member.isActive));
        setActivities(feed);
      } catch (cause) {
        const message = cause instanceof Error ? cause.message : "Workspace could not be loaded.";
        if (message === "AUTH_REQUIRED") setAuthRequired(true);
        else setError(message);
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    load();
    return () => { cancelled = true; };
  }, [tenderById]);

  useEffect(() => {
    if (!selected) {
      setDraft(emptyDraft);
      return;
    }
    setDraft({
      notes: selected.notes ?? "",
      tags: selected.tags.join(", "),
      internalDeadline: selected.internalDeadline ? toLocalInput(selected.internalDeadline) : "",
      assigneeUserId: selected.assigneeUserId ?? ""
    });
    listWorkspaceActivities(selected.id).then(setActivities).catch(() => undefined);
  }, [selectedId]);

  const columns = useMemo(() => opportunityStages.map(stage => ({
    stage,
    items: items.filter(item => item.stage === stage).sort((a, b) => a.position - b.position)
  })), [items]);

  async function dropInto(stage: OpportunityStage) {
    if (!draggedId) return;
    const current = items.find(item => item.id === draggedId);
    if (!current) return;
    const targetItems = items.filter(item => item.stage === stage && item.id !== draggedId);
    const position = (targetItems.at(-1)?.position ?? 0) + 1000;
    const optimistic = { ...current, stage, position, updatedAt: new Date().toISOString() };
    setItems(all => all.map(item => item.id === draggedId ? optimistic : item));
    setDraggedId(null);
    try {
      const saved = await moveWorkspaceItem(current.id, stage, position);
      setItems(all => all.map(item => item.id === saved.id ? saved : item));
      setActivities(await listWorkspaceActivities(selectedId ?? undefined));
    } catch (cause) {
      setItems(all => all.map(item => item.id === current.id ? current : item));
      setError(cause instanceof Error ? cause.message : "Card could not be moved.");
    }
  }

  async function saveDetails() {
    if (!selected) return;
    setSaving(true);
    setError(null);
    try {
      const saved = await updateWorkspaceDetails(selected.id, {
        notes: draft.notes.trim() || null,
        tags: draft.tags.split(",").map(tag => tag.trim()).filter(Boolean).slice(0, 12),
        internalDeadline: draft.internalDeadline ? new Date(draft.internalDeadline).toISOString() : null,
        assigneeUserId: draft.assigneeUserId || null
      });
      setItems(all => all.map(item => item.id === saved.id ? saved : item));
      setActivities(await listWorkspaceActivities(saved.id));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Card details could not be saved.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <div className="workspaceState"><span className="kicker">WORKSPACE 2.0</span><h1>Loading organization board.</h1></div>;

  if (authRequired) return <div className="workspaceState">
    <span className="kicker">SECURE WORKSPACE</span>
    <h1>Sign in to open your organization board.</h1>
    <p>The new workspace is isolated by organization and no longer uses browser-only demo data.</p>
    <a href="/login" className="workspacePrimaryAction">Open sign in</a>
  </div>;

  return <div className="workspaceV2">
    <header className="workspaceV2Header">
      <div><span className="kicker">WORKSPACE 2.0 / LIVE PIPELINE</span><h1>Move opportunities<br/>toward a decision.</h1></div>
      <div className="workspaceMetrics"><strong>{items.length}</strong><span>active opportunities</span><small>{members.length} organization members</small></div>
    </header>

    {error && <div className="workspaceError"><span>{error}</span><button onClick={() => setError(null)}>Dismiss</button></div>}

    <div className="workspaceBoardShell">
      <section className="kanbanBoard" aria-label="Opportunity pipeline">
        {columns.map(column => <section
          className={`kanbanColumn ${draggedId ? "isDragTarget" : ""}`}
          key={column.stage}
          onDragOver={event => event.preventDefault()}
          onDrop={() => dropInto(column.stage)}
        >
          <header><div><span>{column.stage}</span><small>{stageDescription(column.stage)}</small></div><strong>{column.items.length}</strong></header>
          <div className="kanbanStack">
            {column.items.map(item => {
              const tender = tenderById.get(item.tenderId);
              return <article
                draggable
                className={`kanbanCard ${selectedId === item.id ? "isSelected" : ""}`}
                key={item.id}
                onDragStart={() => setDraggedId(item.id)}
                onDragEnd={() => setDraggedId(null)}
                onClick={() => setSelectedId(item.id)}
              >
                <div className="kanbanCardTop"><span>{tender?.countryCode ?? "--"} / {tender?.category ?? "General"}</span><i aria-hidden="true">⋮⋮</i></div>
                <h3>{tender?.title ?? "Opportunity unavailable"}</h3>
                <p>{tender?.buyerName ?? "Unknown buyer"}</p>
                {item.tags.length > 0 && <div className="kanbanTags">{item.tags.slice(0, 3).map(tag => <span key={tag}>{tag}</span>)}</div>}
                <footer>
                  <span>{item.assigneeUserId ? memberInitials(members, item.assigneeUserId) : "UN"}</span>
                  <time className={isLate(item.internalDeadline) ? "isLate" : ""}>{formatDeadline(item.internalDeadline)}</time>
                </footer>
              </article>;
            })}
            {column.items.length === 0 && <div className="kanbanEmpty">Drop an opportunity here</div>}
          </div>
        </section>)}
      </section>

      <aside className={`workspaceInspector ${selected ? "isOpen" : ""}`}>
        {selected ? <>
          <header><div><span className="kicker">CARD DETAILS</span><h2>{selectedTender?.title ?? "Opportunity"}</h2></div><button onClick={() => setSelectedId(null)} aria-label="Close details">×</button></header>
          <div className="inspectorMeta"><span>{selected.stage}</span><span>{selectedTender?.countryCode ?? "--"}</span><span>{selectedTender?.buyerName ?? "Unknown buyer"}</span></div>
          <label>Internal notes<textarea value={draft.notes} onChange={event => setDraft(value => ({ ...value, notes: event.target.value }))} rows={6}/></label>
          <label>Tags<input value={draft.tags} onChange={event => setDraft(value => ({ ...value, tags: event.target.value }))} placeholder="priority, framework, review"/></label>
          <label>Internal deadline<input type="datetime-local" value={draft.internalDeadline} onChange={event => setDraft(value => ({ ...value, internalDeadline: event.target.value }))}/></label>
          <label>Assignee<select value={draft.assigneeUserId} onChange={event => setDraft(value => ({ ...value, assigneeUserId: event.target.value }))}><option value="">Unassigned</option>{members.map(member => <option key={member.id} value={member.id}>{member.displayName}</option>)}</select></label>
          <button className="workspacePrimaryAction" onClick={saveDetails} disabled={saving}>{saving ? "Saving…" : "Save card details"}</button>
          <section className="activityPanel"><header><span>ACTIVITY</span><strong>{activities.length}</strong></header>{activities.length === 0 ? <p>No activity yet.</p> : activities.slice(0, 20).map(activity => <article key={activity.id}><i>{initials(activity.displayName)}</i><div><strong>{activity.displayName}</strong><span>{activityLabel(activity.action)}</span><time>{new Date(activity.createdAt).toLocaleString()}</time></div></article>)}</section>
        </> : <div className="inspectorEmpty"><span className="kicker">CARD DETAILS</span><h2>Select a card.</h2><p>Review notes, tags, owner, deadline and the complete activity trail.</p></div>}
      </aside>
    </div>
  </div>;
}

function readLegacySavedIds(): string[] {
  try { return JSON.parse(localStorage.getItem("tenderscope:saved") ?? "[]"); } catch { return []; }
}
function stageDescription(stage: OpportunityStage) {
  return ({ Review: "Initial triage", Qualified: "Fit confirmed", Preparing: "Response in progress", Submitted: "Awaiting result", Won: "Awarded", Lost: "Closed" })[stage];
}
function initials(name: string) { return name.split(/\s+/).slice(0, 2).map(part => part[0]).join("").toUpperCase(); }
function memberInitials(members: OrganizationMember[], id: string) { return initials(members.find(member => member.id === id)?.displayName ?? "UN"); }
function toLocalInput(value: string) { const date = new Date(value); return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16); }
function formatDeadline(value?: string | null) { return value ? new Date(value).toLocaleDateString(undefined, { month: "short", day: "numeric" }) : "No deadline"; }
function isLate(value?: string | null) { return Boolean(value && new Date(value).getTime() < Date.now()); }
function activityLabel(action: string) { return ({ "workspace.item.saved": "created or updated the card", "workspace.item.moved": "moved the card", "workspace.item.details_updated": "updated card details" } as Record<string, string>)[action] ?? action.replaceAll(".", " "); }
