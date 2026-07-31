import type { MetadataRoute } from "next";

export default function robots(): MetadataRoute.Robots {
  const base = process.env.NEXT_PUBLIC_SITE_URL ?? "https://tenderscope.vercel.app";
  return {
    rules: [{ userAgent: "*", allow: ["/", "/opportunities", "/analytics"], disallow: ["/workspace", "/admin"] }],
    sitemap: `${base}/sitemap.xml`
  };
}
