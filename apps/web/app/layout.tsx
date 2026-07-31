import type { Metadata, Viewport } from "next";
import { PortalShell } from "../components/portal-shell";
import "./globals.css";

const siteUrl = process.env.NEXT_PUBLIC_SITE_URL ?? "https://tenderscope-platform.vercel.app";

export const metadata: Metadata = {
  title: { default: "TenderScope — Public Opportunity Intelligence", template: "%s — TenderScope" },
  description: "Open-data public procurement intelligence, market signals and qualification workspace.",
  metadataBase: new URL(siteUrl),
  applicationName: "TenderScope",
  alternates: { canonical: "/" },
  openGraph: {
    type: "website",
    url: siteUrl,
    siteName: "TenderScope",
    title: "TenderScope — Public Opportunity Intelligence",
    description: "Discover, qualify and monitor public-sector opportunities from verified open sources."
  },
  twitter: {
    card: "summary_large_image",
    title: "TenderScope — Public Opportunity Intelligence",
    description: "Public procurement intelligence built from verified open data."
  },
  robots: { index: true, follow: true },
  category: "business"
};

export const viewport: Viewport = { width: "device-width", initialScale: 1, colorScheme: "dark", themeColor: "#0a0b0b" };

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en"><body><PortalShell>{children}</PortalShell></body></html>;
}
