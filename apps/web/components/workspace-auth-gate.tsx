'use client';

import { useEffect, useState } from 'react';
import { getAccessToken, refreshSession } from '../lib/auth-api';

export function WorkspaceAuthGate({ children, next = '/workspace', label = 'SECURE WORKSPACE' }: { children: React.ReactNode; next?: string; label?: string }) {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let active = true;
    async function verify() {
      const valid = Boolean(getAccessToken()) || Boolean(await refreshSession());
      if (!active) return;
      if (!valid) {
        window.location.replace(`/login?next=${encodeURIComponent(next)}`);
        return;
      }
      setReady(true);
    }
    verify();
    return () => { active = false; };
  }, [next]);

  if (!ready) return <div className="workspaceState"><span className="kicker">{label}</span><h1>Verifying your session.</h1></div>;
  return children;
}
