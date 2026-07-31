'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { getStoredSession } from '../lib/auth-api';
import { listNotifications } from '../lib/notification-api';
import styles from './notification-badge.module.css';

export function NotificationBadge() {
  const [unread, setUnread] = useState(0);
  const [signedIn, setSignedIn] = useState(false);

  useEffect(() => {
    let active = true;
    const load = () => {
      const session = getStoredSession();
      setSignedIn(Boolean(session));
      if (!session) { setUnread(0); return; }
      listNotifications(true, 1).then(result => { if (active) setUnread(result.unread); }).catch(() => undefined);
    };
    load();
    const interval = window.setInterval(load, 60000);
    window.addEventListener('tenderscope:auth-changed', load);
    return () => { active = false; window.clearInterval(interval); window.removeEventListener('tenderscope:auth-changed', load); };
  }, []);

  if (!signedIn) return null;
  return <Link href="/notifications" className={styles.badge} aria-label={`${unread} unread notifications`}><span>Alerts</span>{unread > 0 && <strong>{unread > 99 ? '99+' : unread}</strong>}</Link>;
}
