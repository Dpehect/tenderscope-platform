'use client';

import { useEffect, useState } from 'react';
import { getAccessToken, refreshSession } from '../lib/auth-api';

export function WorkspaceAuthGate({ children }: { children: React.ReactNode }) {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let active = true;
    async function verify() {
      const valid = Boolean(getAccessToken()) || Boolean(await refreshSession());
      if (!active) return;
      if (!valid) {
        window.location.replace('/login?next=/workspace');
        return;
      }
      setReady(true);
    }
    verify();
    return () => { active = false; };
  }, []);

  if (!ready) return <div className="workspaceState"><span className="kicker">SECURE WORKSPACE</span><h1>Verifying your session.</h1></div>;
  return children;
}
