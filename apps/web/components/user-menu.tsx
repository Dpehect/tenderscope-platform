'use client';

import Link from 'next/link';
import { useEffect, useRef, useState } from 'react';
import { getStoredSession, listUserOrganizations, logout, refreshSession, switchOrganization, type AuthSession, type UserOrganization } from '../lib/auth-api';
import styles from './user-menu.module.css';

export function UserMenu() {
  const [session, setSession] = useState<AuthSession | null>(null);
  const [organizations, setOrganizations] = useState<UserOrganization[]>([]);
  const [open, setOpen] = useState(false);
  const [switching, setSwitching] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let active = true;
    const sync = () => setSession(getStoredSession());
    const loadOrganizations = () => listUserOrganizations().then(rows => { if (active) setOrganizations(rows); }).catch(() => undefined);
    sync();
    if (!getStoredSession()) refreshSession().then(value => { if (active) { setSession(value); if (value) loadOrganizations(); } });
    else loadOrganizations();
    window.addEventListener('tenderscope:auth-changed', sync);
    const close = (event: MouseEvent) => { if (menuRef.current && !menuRef.current.contains(event.target as Node)) setOpen(false); };
    document.addEventListener('mousedown', close);
    return () => { active = false; window.removeEventListener('tenderscope:auth-changed', sync); document.removeEventListener('mousedown', close); };
  }, []);

  if (!session) return <Link href="/login" className="headerAction">Sign in <span aria-hidden>↗</span></Link>;

  async function signOut() { await logout(); window.location.assign('/'); }
  async function changeOrganization(organizationId: string) {
    if (organizationId === session?.user.organizationId) return;
    setSwitching(true);
    try {
      const next = await switchOrganization(organizationId);
      setSession(next);
      setOrganizations(await listUserOrganizations());
      setOpen(false);
      window.location.assign('/workspace');
    } finally { setSwitching(false); }
  }

  return <div className={styles.menu} ref={menuRef}>
    <button className={styles.trigger} onClick={() => setOpen(value => !value)} aria-expanded={open}>
      <span>{initials(session.user.displayName)}</span>
      <div><strong>{session.user.displayName}</strong><small>{session.user.organizationName}</small></div>
    </button>
    {open && <div className={styles.panel}>
      <div className={styles.identity}><strong>{session.user.email}</strong><span>{session.user.role}</span></div>
      {organizations.length > 1 && <label className={styles.switcher}>
        <span>Active organization</span>
        <select value={session.user.organizationId} disabled={switching} onChange={event => changeOrganization(event.target.value)}>
          {organizations.map(organization => <option key={organization.id} value={organization.id}>{organization.name} · {organization.role}</option>)}
        </select>
      </label>}
      <Link href="/workspace" onClick={() => setOpen(false)}>Workspace</Link>
      <Link href="/notifications" onClick={() => setOpen(false)}>Notifications</Link>
      <Link href="/settings/organization" onClick={() => setOpen(false)}>Organization settings</Link>
      <Link href="/invitations/accept" onClick={() => setOpen(false)}>Accept invitation</Link>
      <button onClick={signOut}>Sign out</button>
    </div>}
  </div>;
}

function initials(name: string) { return name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('').toUpperCase(); }
