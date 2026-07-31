'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import {
  getNotificationPreferences,
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
  runWatchlistMatching,
  updateNotificationPreferences,
  type NotificationItem,
  type NotificationPreferences
} from '../lib/notification-api';

const defaults: NotificationPreferences = { inAppEnabled: true, watchlistMatchesEnabled: true, deadlineRemindersEnabled: true };

export function NotificationCenter() {
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [unread, setUnread] = useState(0);
  const [preferences, setPreferences] = useState(defaults);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  async function load() {
    const [feed, prefs] = await Promise.all([listNotifications(), getNotificationPreferences()]);
    setItems(feed.items);
    setUnread(feed.unread);
    setPreferences(prefs);
  }

  useEffect(() => { load().catch(() => setMessage('Notifications could not be loaded.')); }, []);

  async function read(item: NotificationItem) {
    if (!item.isRead) {
      await markNotificationRead(item.id);
      setItems(rows => rows.map(row => row.id === item.id ? { ...row, isRead: true, readAt: new Date().toISOString() } : row));
      setUnread(value => Math.max(0, value - 1));
    }
  }

  async function readAll() {
    setBusy(true);
    await markAllNotificationsRead();
    setItems(rows => rows.map(row => ({ ...row, isRead: true, readAt: row.readAt ?? new Date().toISOString() })));
    setUnread(0);
    setBusy(false);
  }

  async function savePreferences(next: NotificationPreferences) {
    setPreferences(next);
    try { setPreferences(await updateNotificationPreferences(next)); }
    catch { setMessage('Preferences could not be saved.'); }
  }

  async function matchNow() {
    setBusy(true);
    setMessage(null);
    try {
      const result = await runWatchlistMatching();
      setMessage(`${result.created} new matches created.`);
      await load();
    } catch (cause) {
      setMessage(cause instanceof Error ? cause.message : 'Matching could not be started.');
    } finally { setBusy(false); }
  }

  return <div className="notificationCenter">
    <header>
      <div><span className="kicker">NOTIFICATION CENTER</span><h1>Signals that need attention.</h1><p>Watchlist matches, deadline reminders and organization activity in one queue.</p></div>
      <div className="notificationMetric"><strong>{unread}</strong><span>unread</span></div>
    </header>

    {message && <div className="notificationMessage">{message}</div>}

    <section className="notificationControls">
      <button onClick={readAll} disabled={busy || unread === 0}>Mark all read</button>
      <button onClick={matchNow} disabled={busy}>{busy ? 'Working…' : 'Run watchlist matching'}</button>
    </section>

    <section className="notificationPreferences">
      <h2>Preferences</h2>
      <Toggle label="In-app notifications" checked={preferences.inAppEnabled} onChange={value => savePreferences({ ...preferences, inAppEnabled: value })}/>
      <Toggle label="Watchlist matches" checked={preferences.watchlistMatchesEnabled} onChange={value => savePreferences({ ...preferences, watchlistMatchesEnabled: value })}/>
      <Toggle label="Deadline reminders" checked={preferences.deadlineRemindersEnabled} onChange={value => savePreferences({ ...preferences, deadlineRemindersEnabled: value })}/>
    </section>

    <section className="notificationFeed">
      {items.length === 0 ? <div className="notificationEmpty">No notifications yet.</div> : items.map(item => <article key={item.id} className={item.isRead ? 'isRead' : ''}>
        <button onClick={() => read(item)} aria-label="Mark notification read"><i /></button>
        <div><span>{item.type}</span><h2>{item.title}</h2><p>{item.message}</p><time>{new Date(item.createdAt).toLocaleString()}</time></div>
        {item.resourceUrl && <Link href={item.resourceUrl} onClick={() => read(item)}>Open ↗</Link>}
      </article>)}
    </section>
  </div>;
}

function Toggle({ label, checked, onChange }: { label: string; checked: boolean; onChange: (value: boolean) => void }) {
  return <label><span>{label}</span><input type="checkbox" checked={checked} onChange={event => onChange(event.target.checked)}/><i /></label>;
}
