import type { Metadata } from "next";
import { WorkspaceBoard } from "../../components/workspace-board";
import { getOpportunities } from "../../lib/api";
import "./workspace.css";

export const metadata: Metadata = {
  title: "Workspace",
  description: "Qualify and track public procurement opportunities.",
  alternates: { canonical: "/workspace" },
  openGraph: { url: "/workspace", title: "Workspace | TenderScope" }
};

export default async function WorkspacePage() {
  const result = await getOpportunities("pageSize=100&sort=deadline-asc");
  return <main className="workspacePage"><WorkspaceBoard opportunities={result.items}/></main>;
}
