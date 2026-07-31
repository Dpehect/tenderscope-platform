import type { Metadata } from "next";
import { WorkspaceBoard } from "../../components/workspace-board";
import { WorkspaceAuthGate } from "../../components/workspace-auth-gate";
import { WatchlistPanel } from "../../components/watchlist-panel";
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
  return <main className="workspacePage"><WorkspaceAuthGate><WatchlistPanel /><WorkspaceBoard opportunities={result.items}/></WorkspaceAuthGate></main>;
}
