import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = { title: "TenderScope", description: "Open-data public procurement intelligence" };

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en"><body>{children}</body></html>;
}
