import Link from "next/link";
import { NotificationBadge } from "./notification-badge";
import { UserMenu } from "./user-menu";

const nav = [
  ["Opportunities", "/opportunities"],
  ["Market", "/analytics"],
  ["Methodology", "/methodology"],
  ["Organization", "/analytics/organization"],
  ["Search", "/search"],
  ["Workspace", "/workspace"],
] as const;

export function PortalShell({ children }: { children: React.ReactNode }) {
  return (
    <div className="siteShell">
      <header className="siteHeader">
        <Link href="/" className="brand" aria-label="TenderScope home">
          <span className="brandMark">TS</span>
          <span>TenderScope</span>
        </Link>
        <nav aria-label="Primary navigation">
          {nav.map(([label, href]) => <Link key={href} href={href}>{label}</Link>)}
        </nav>
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}><NotificationBadge/><UserMenu /></div>
      </header>
      {children}
      <footer className="siteFooter">
        <div>
          <span className="footerIndex">TS / 2026</span>
          <strong>Public opportunity intelligence without paid data walls.</strong>
        </div>
        <div className="footerLinks">
          <Link href="/opportunities">Explore</Link>
          <Link href="/analytics">Signals</Link>
          <Link href="/search">Search</Link>
          <Link href="/methodology">Methodology</Link>
          <a href="https://github.com/Dpehect/tenderscope-platform" target="_blank" rel="noreferrer">Source</a>
        </div>
      </footer>
    </div>
  );
}
