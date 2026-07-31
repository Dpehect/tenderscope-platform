'use client';

import { useEffect, useState } from 'react';
import { switchOrganization } from '../lib/auth-api';
import { acceptInvitation } from '../lib/organization-api';

export function InvitationAcceptance() {
  const [token, setToken] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const value = new URLSearchParams(window.location.search).get('token');
    if (value) setToken(value);
  }, []);

  async function accept() {
    if (!token.trim()) return;
    setBusy(true);
    setError(null);
    try {
      const result = await acceptInvitation(token.trim());
      await switchOrganization(result.organizationId);
      window.location.assign('/workspace');
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Invitation could not be accepted.');
    } finally { setBusy(false); }
  }

  return <section className="invitationAcceptCard">
    <span className="kicker">ORGANIZATION INVITATION</span>
    <h1>Join a shared workspace.</h1>
    <p>Paste the invitation token provided by your organization administrator. After acceptance, TenderScope will switch directly to the new organization.</p>
    {error && <div className="invitationAcceptError">{error}</div>}
    <label>Invitation token<textarea rows={5} value={token} onChange={event => setToken(event.target.value)} placeholder="Paste the invitation token" /></label>
    <button onClick={accept} disabled={busy || !token.trim()}>{busy ? 'Accepting…' : 'Accept invitation'}</button>
    <small>The signed-in account email must match the email used for the invitation.</small>
  </section>;
}
