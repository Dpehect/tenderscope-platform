import { WorkspaceBoard } from "../../components/workspace-board";
import { getOpportunities } from "../../lib/api";

export const metadata = { title: "Workspace — TenderScope", description: "Qualify and track public procurement opportunities." };

export default async function WorkspacePage() {
  const result = await getOpportunities("pageSize=100&sort=deadline-asc");
  return <main className="workspacePage"><WorkspaceBoard opportunities={result.items}/></main>;
}
