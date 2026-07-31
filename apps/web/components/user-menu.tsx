'use client';

import Link from 'next/link';
import { useEffect, useRef, useState } from 'react';
import { getStoredSession, logout, refreshSession, type AuthSession } from '../lib/auth-api';

export function UserMenu() {
  const [session, setSession] = useState<AuthSession | null>(null);
  const [open, setOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let active = true;
    const sync = () => setSession(getStoredSession());
    sync();
    if (!getStoredSession()) refreshSession().then(value => { if (active) setSession(value); });
    window.addEventListener('tenderscope:auth-changed', sync);
    const close = (event: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', close);
    return () => {
      active = false;
      window.removeEventListener('tenderscope:auth-changed', sync);
      document.removeEventListener('mousedown', close);
    };
  }, []);

  if (!session) return <Link href="/login" className="headerAction">Sign in <span aria-hidden>↗</span></Link>;

  async function signOut() {
    await logout();
    window.location.assign('/');
  }

  return <div className="userMenu" ref={menuRef}>
    <button className="userMenuTrigger" onClick={() => setOpen(value => !value)} aria-expanded={open}>
      <span>{initials(session.user.displayName)}</span>
      <div><strong>{session.user.displayName}</strong><small>{session.user.organizationName}</small></div>
    </button>
    {open && <div className="userMenuPanel">
      <div className="userMenuIdentity"><strong>{session.user.email}</strong><span>{session.user.role}</span></div>
      <Link href="/workspace" onClick={() => setOpen(false)}>Workspace</Link>
      <Link href="/settings/organization" onClick={() => setOpen(false)}>Organization settings</Link>
      <button onClick={signOut}>Sign out</button>
    </div>}
  </div>;
}

function initials(name: string) {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('').toUpperCase();
}
