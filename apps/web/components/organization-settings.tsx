'use client';

import { useEffect, useMemo, useState } from 'react';
import { getStoredSession } from '../lib/auth-api';
import {
  changeMemberRole,
  createInvitation,
  listInvitations,
  listMembers,
  normalizeRole,
  organizationRoles,
  removeMember,
  revokeInvitation,
  type CreatedInvitation,
  type OrganizationInvitation,
  type OrganizationMember,
  type OrganizationRole
} from '../lib/organization-api';

const manageableRoles = organizationRoles.filter(role => role !== 'Owner');

export function OrganizationSettings() {
  const session = getStoredSession();
  const currentRole = session?.user.role as OrganizationRole | undefined;
  const canManage = currentRole ? organizationRoles.indexOf(currentRole) >= organizationRoles.indexOf('Manager') : false;
  const [members, setMembers] = useState<OrganizationMember[]>([]);
  const [invitations, setInvitations] = useState<OrganizationInvitation[]>([]);
  const [email, setEmail] = useState('');
  const [role, setRole] = useState<OrganizationRole>('Viewer');
  const [expiresInDays, setExpiresInDays] = useState(7);
  const [created, setCreated] = useState<CreatedInvitation | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const currentUserId = session?.user.id;
  const activeMembers = useMemo(() => members.filter(member => member.isActive), [members]);

  useEffect(() => {
    let cancelled = false;
    Promise.all([listMembers(), canManage ? listInvitations() : Promise.resolve([])])
      .then(([memberRows, inviteRows]) => {
        if (cancelled) return;
        setMembers(memberRows);
        setInvitations(inviteRows);
      })
      .catch(cause => setError(cause instanceof Error ? cause.message : 'Organization settings could not be loaded.'))
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [canManage]);

  async function invite() {
    if (!email.trim()) return;
    setBusy('invite');
    setError(null);
    try {
      const result = await createInvitation(email.trim(), role, expiresInDays);
      setCreated(result);
      setEmail('');
      setInvitations(await listInvitations());
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Invitation could not be created.');
    } finally {
      setBusy(null);
    }
  }

  async function updateRole(member: OrganizationMember, nextRole: OrganizationRole) {
    setBusy(member.id);
    setError(null);
    try {
      await changeMemberRole(member.id, nextRole);
      setMembers(rows => rows.map(row => row.id === member.id ? { ...row, role: nextRole } : row));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Role could not be changed.');
    } finally {
      setBusy(null);
    }
  }

  async function remove(member: OrganizationMember) {
    if (!window.confirm(`Remove ${member.displayName} from this organization?`)) return;
    setBusy(member.id);
    setError(null);
    try {
      await removeMember(member.id);
      setMembers(rows => rows.filter(row => row.id !== member.id));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Member could not be removed.');
    } finally {
      setBusy(null);
    }
  }

  async function revoke(invitation: OrganizationInvitation) {
    setBusy(invitation.id);
    setError(null);
    try {
      await revokeInvitation(invitation.id);
      setInvitations(rows => rows.filter(row => row.id !== invitation.id));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Invitation could not be revoked.');
    } finally {
      setBusy(null);
    }
  }

  if (loading) return <div className="organizationState"><span className="kicker">ORGANIZATION</span><h1>Loading settings.</h1></div>;

  return <div className="organizationSettings">
    <header className="organizationHeader">
      <div><span className="kicker">ORGANIZATION CONTROL</span><h1>{session?.user.organizationName ?? 'Organization settings'}</h1><p>Manage access, roles and invitations for your shared procurement workspace.</p></div>
      <div className="organizationSummary"><strong>{activeMembers.length}</strong><span>active members</span><small>Your role: {currentRole ?? 'Viewer'}</small></div>
    </header>

    {error && <div className="organizationError"><span>{error}</span><button onClick={() => setError(null)}>Dismiss</button></div>}

    {canManage && <section className="settingsPanel invitePanel">
      <header><div><span>NEW INVITATION</span><h2>Invite a teammate.</h2></div><small>Manager access or above required</small></header>
      <div className="inviteForm">
        <label>Email<input type="email" value={email} onChange={event => setEmail(event.target.value)} placeholder="name@company.com" /></label>
        <label>Role<select value={role} onChange={event => setRole(event.target.value as OrganizationRole)}>{manageableRoles.map(item => <option key={item}>{item}</option>)}</select></label>
        <label>Expires<select value={expiresInDays} onChange={event => setExpiresInDays(Number(event.target.value))}><option value={1}>1 day</option><option value={7}>7 days</option><option value={14}>14 days</option><option value={30}>30 days</option></select></label>
        <button onClick={invite} disabled={busy === 'invite'}>{busy === 'invite' ? 'Creating…' : 'Create invitation'}</button>
      </div>
      {created && <div className="invitationToken"><div><strong>Invitation token</strong><span>Share this token securely. It is only shown after creation.</span></div><code>{created.token}</code><button onClick={() => navigator.clipboard.writeText(created.token)}>Copy</button></div>}
    </section>}

    <section className="settingsPanel">
      <header><div><span>MEMBERS</span><h2>Workspace access.</h2></div><small>{activeMembers.length} active</small></header>
      <div className="memberTable">
        {members.map(member => {
          const memberRole = normalizeRole(member.role);
          const isOwner = memberRole === 'Owner';
          const isSelf = member.id === currentUserId;
          return <article key={member.id}>
            <div className="memberIdentity"><i>{initials(member.displayName)}</i><div><strong>{member.displayName}{isSelf ? ' (you)' : ''}</strong><span>{member.email}</span></div></div>
            <div className="memberDates"><span>Joined {formatDate(member.joinedAt)}</span><small>{member.lastLoginAt ? `Last active ${formatDate(member.lastLoginAt)}` : 'No recorded login'}</small></div>
            <div className="memberActions">
              {canManage && !isOwner && !isSelf ? <select disabled={busy === member.id} value={memberRole} onChange={event => updateRole(member, event.target.value as OrganizationRole)}>{manageableRoles.map(item => <option key={item}>{item}</option>)}</select> : <span className="roleBadge">{memberRole}</span>}
              {canManage && !isOwner && !isSelf && <button disabled={busy === member.id} onClick={() => remove(member)}>Remove</button>}
            </div>
          </article>;
        })}
      </div>
    </section>

    {canManage && <section className="settingsPanel">
      <header><div><span>PENDING INVITATIONS</span><h2>Open access requests.</h2></div><small>{invitations.length} pending</small></header>
      <div className="invitationList">{invitations.length === 0 ? <p>No pending invitations.</p> : invitations.map(invitation => <article key={invitation.id}><div><strong>{invitation.email}</strong><span>{normalizeRole(invitation.role)} · expires {formatDate(invitation.expiresAt)}</span></div><button disabled={busy === invitation.id} onClick={() => revoke(invitation)}>Revoke</button></article>)}</div>
    </section>}
  </div>;
}

function initials(name: string) { return name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('').toUpperCase(); }
function formatDate(value: string) { return new Date(value).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }); }
