'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import {
  createWatchlist,
  deleteWatchlist,
  listWatchlists,
  setWatchlistNotifications,
  updateWatchlist,
  type Watchlist,
  type WatchlistInput
} from '../lib/watchlist-api';

const emptyDraft: WatchlistInput = { name: '', query: '', country: '', category: '', notificationsEnabled: true };

export function WatchlistPanel() {
  const [items, setItems] = useState<Watchlist[]>([]);
  const [draft, setDraft] = useState<WatchlistInput>(emptyDraft);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState<string | null>('load');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    listWatchlists().then(setItems).catch(cause => setError(message(cause))).finally(() => setBusy(null));
  }, []);

  function beginEdit(item: Watchlist) {
    setEditingId(item.id);
    setDraft({ name: item.name, query: item.query ?? '', country: item.country ?? '', category: item.category ?? '', notificationsEnabled: item.notificationsEnabled });
    setOpen(true);
  }

  function reset() {
    setEditingId(null);
    setDraft(emptyDraft);
    setOpen(false);
  }

  async function save() {
    if (!draft.name.trim()) return;
    setBusy('save');
    setError(null);
    try {
      const input = {
        name: draft.name.trim(),
        query: draft.query?.trim() || null,
        country: draft.country?.trim().toUpperCase() || null,
        category: draft.category?.trim() || null,
        notificationsEnabled: draft.notificationsEnabled
      };
      const saved = editingId ? await updateWatchlist(editingId, input) : await createWatchlist(input);
      setItems(rows => editingId ? rows.map(row => row.id === saved.id ? saved : row) : [saved, ...rows]);
      reset();
    } catch (cause) {
      setError(message(cause));
    } finally {
      setBusy(null);
    }
  }

  async function toggle(item: Watchlist) {
    setBusy(item.id);
    try {
      const saved = await setWatchlistNotifications(item.id, !item.notificationsEnabled);
      setItems(rows => rows.map(row => row.id === saved.id ? saved : row));
    } catch (cause) {
      setError(message(cause));
    } finally {
      setBusy(null);
    }
  }

  async function remove(item: Watchlist) {
    if (!window.confirm(`Delete watchlist “${item.name}”?`)) return;
    setBusy(item.id);
    try {
      await deleteWatchlist(item.id);
      setItems(rows => rows.filter(row => row.id !== item.id));
    } catch (cause) {
      setError(message(cause));
    } finally {
      setBusy(null);
    }
  }

  return <section className="watchlistPanel">
    <header className="watchlistHeader">
      <div><span className="kicker">WATCHLISTS / SAVED SEARCHES</span><h2>Monitor procurement signals.</h2></div>
      <button onClick={() => open ? reset() : setOpen(true)}>{open ? 'Close' : '+ New watchlist'}</button>
    </header>

    {error && <div className="watchlistError"><span>{error}</span><button onClick={() => setError(null)}>Dismiss</button></div>}

    {open && <div className="watchlistEditor">
      <label>Name<input value={draft.name} onChange={event => setDraft(value => ({ ...value, name: event.target.value }))} placeholder="EU cybersecurity opportunities" /></label>
      <label>Keywords<input value={draft.query ?? ''} onChange={event => setDraft(value => ({ ...value, query: event.target.value }))} placeholder="cybersecurity framework" /></label>
      <label>Country<input value={draft.country ?? ''} onChange={event => setDraft(value => ({ ...value, country: event.target.value }))} maxLength={3} placeholder="EU" /></label>
      <label>Category<input value={draft.category ?? ''} onChange={event => setDraft(value => ({ ...value, category: event.target.value }))} placeholder="IT services" /></label>
      <label className="watchlistCheck"><input type="checkbox" checked={draft.notificationsEnabled} onChange={event => setDraft(value => ({ ...value, notificationsEnabled: event.target.checked }))} /> Enable notifications</label>
      <button onClick={save} disabled={busy === 'save'}>{busy === 'save' ? 'Saving…' : editingId ? 'Update watchlist' : 'Create watchlist'}</button>
    </div>}

    <div className="watchlistGrid">
      {busy === 'load' && <div className="watchlistEmpty">Loading watchlists…</div>}
      {busy !== 'load' && items.length === 0 && <div className="watchlistEmpty">No saved searches yet.</div>}
      {items.map(item => <article className="watchlistCard" key={item.id}>
        <div className="watchlistCardTop"><span>{item.notificationsEnabled ? 'NOTIFICATIONS ON' : 'NOTIFICATIONS OFF'}</span><button onClick={() => toggle(item)} disabled={busy === item.id}>{item.notificationsEnabled ? 'Pause' : 'Enable'}</button></div>
        <h3>{item.name}</h3>
        <p>{[item.query, item.country, item.category].filter(Boolean).join(' · ') || 'All opportunities'}</p>
        <footer>
          <Link href={searchHref(item)}>Open results ↗</Link>
          <div><button onClick={() => beginEdit(item)}>Edit</button><button onClick={() => remove(item)} disabled={busy === item.id}>Delete</button></div>
        </footer>
      </article>)}
    </div>
  </section>;
}

function searchHref(item: Watchlist) {
  const params = new URLSearchParams();
  if (item.query) params.set('q', item.query);
  if (item.country) params.set('country', item.country);
  if (item.category) params.set('category', item.category);
  return `/opportunities${params.size ? `?${params.toString()}` : ''}`;
}
function message(cause: unknown) { return cause instanceof Error ? cause.message : 'Watchlist operation failed.'; }
