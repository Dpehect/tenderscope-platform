import type { Metadata } from "next";
import { PortalShell } from "../components/portal-shell";
import "./globals.css";

export const metadata: Metadata = {
  title: { default: "TenderScope — Public Opportunity Intelligence", template: "%s" },
  description: "Open-data public procurement intelligence, market signals and qualification workspace.",
  metadataBase: new URL("https://tenderscope-platform.vercel.app"),
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en"><body><PortalShell>{children}</PortalShell></body></html>;
}
