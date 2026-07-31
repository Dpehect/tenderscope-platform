import { authRequest, getAccessToken } from './auth-api';

export const opportunityStages = ["Review", "Qualified", "Preparing", "Submitted", "Won", "Lost"] as const;
export type OpportunityStage = typeof opportunityStages[number];

const stageNumbers: Record<OpportunityStage, number> = { Review: 0, Qualified: 1, Preparing: 2, Submitted: 3, Won: 4, Lost: 5 };

export type WorkspaceItem = {
  id: string; organizationId: string; createdByUserId: string; tenderId: string;
  stage: OpportunityStage; position: number; notes?: string | null; tags: string[];
  internalDeadline?: string | null; assigneeUserId?: string | null; createdAt: string; updatedAt: string;
};
export type OrganizationMember = { id: string; email: string; displayName: string; role: number | string; isActive: boolean; };
export type WorkspaceActivity = { id: string; workspaceItemId?: string; action: string; detail?: string | null; createdAt: string; actorUserId: string; displayName: string; };
export type WorkspaceDetailsInput = { notes?: string | null; tags?: string[]; internalDeadline?: string | null; assigneeUserId?: string | null; };

function normalizeStage(stage: number | string): OpportunityStage {
  if (typeof stage === "number") return opportunityStages[stage] ?? "Review";
  return opportunityStages.includes(stage as OpportunityStage) ? stage as OpportunityStage : "Review";
}
function normalizeItem(item: Omit<WorkspaceItem, "stage"> & { stage: number | string }): WorkspaceItem {
  return { ...item, stage: normalizeStage(item.stage), tags: item.tags ?? [], position: Number(item.position ?? 0) };
}

export { getAccessToken };
export async function listWorkspaceItems() {
  const items = await authRequest<Array<Omit<WorkspaceItem, "stage"> & { stage: number | string }>>("/api/workspace/v2/items");
  return items.map(normalizeItem);
}
export async function createWorkspaceItem(tenderId: string, stage: OpportunityStage = "Review") {
  const item = await authRequest<Omit<WorkspaceItem, "stage"> & { stage: number | string }>(`/api/workspace/v2/items/${tenderId}`, { method: "PUT", body: JSON.stringify({ stage: stageNumbers[stage], notes: null, tags: [], internalDeadline: null, assigneeUserId: null }) });
  return normalizeItem(item);
}
export async function moveWorkspaceItem(id: string, stage: OpportunityStage, position: number) {
  const item = await authRequest<Omit<WorkspaceItem, "stage"> & { stage: number | string }>(`/api/workspace/v2/items/${id}/move`, { method: "PATCH", body: JSON.stringify({ stage: stageNumbers[stage], position }) });
  return normalizeItem(item);
}
export async function updateWorkspaceDetails(id: string, input: WorkspaceDetailsInput) {
  const item = await authRequest<Omit<WorkspaceItem, "stage"> & { stage: number | string }>(`/api/workspace/v2/items/${id}/details`, { method: "PATCH", body: JSON.stringify(input) });
  return normalizeItem(item);
}
export function listWorkspaceActivities(itemId?: string) {
  return authRequest<WorkspaceActivity[]>(itemId ? `/api/workspace/v2/items/${itemId}/activities?take=100` : "/api/workspace/v2/activities?take=100");
}
export function listOrganizationMembers() { return authRequest<OrganizationMember[]>("/api/organization/members"); }
