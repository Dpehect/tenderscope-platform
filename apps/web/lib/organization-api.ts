'use client';

import { authRequest } from './auth-api';

export const organizationRoles = ['Viewer', 'Analyst', 'Manager', 'Admin', 'Owner'] as const;
export type OrganizationRole = typeof organizationRoles[number];

const roleNumbers: Record<OrganizationRole, number> = { Viewer: 0, Analyst: 1, Manager: 2, Admin: 3, Owner: 4 };

export type OrganizationMember = {
  membershipId: string; id: string; email: string; displayName: string; role: number | string;
  joinedAt: string; lastLoginAt?: string | null; isActive: boolean;
};
export type OrganizationInvitation = {
  id: string; email: string; role: number | string; createdAt: string; expiresAt: string;
  invitedByUserId: string; isActive: boolean;
};
export type CreatedInvitation = OrganizationInvitation & { token: string; acceptPath: string };

export function normalizeRole(role: number | string): OrganizationRole {
  if (typeof role === 'number') return organizationRoles[role] ?? 'Viewer';
  return organizationRoles.includes(role as OrganizationRole) ? role as OrganizationRole : 'Viewer';
}
export function listMembers() { return authRequest<OrganizationMember[]>('/api/organization/members'); }
export function listInvitations() { return authRequest<OrganizationInvitation[]>('/api/organization/invitations'); }
export function createInvitation(email: string, role: OrganizationRole, expiresInDays = 7) {
  return authRequest<CreatedInvitation>('/api/organization/invitations', { method: 'POST', body: JSON.stringify({ email, role: roleNumbers[role], expiresInDays }) });
}
export function acceptInvitation(token: string) {
  return authRequest<{ organizationId: string; role: string }>('/api/organization/invitations/accept', { method: 'POST', body: JSON.stringify({ token }) });
}
export function revokeInvitation(id: string) { return authRequest<void>(`/api/organization/invitations/${id}`, { method: 'DELETE' }); }
export function changeMemberRole(userId: string, role: OrganizationRole) {
  return authRequest<{ userId: string; role: string }>(`/api/organization/members/${userId}/role`, { method: 'PATCH', body: JSON.stringify({ role: roleNumbers[role] }) });
}
export function removeMember(userId: string) { return authRequest<void>(`/api/organization/members/${userId}`, { method: 'DELETE' }); }
