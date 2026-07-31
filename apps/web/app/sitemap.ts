import type { MetadataRoute } from "next";

export default function sitemap(): MetadataRoute.Sitemap {
  const base = process.env.NEXT_PUBLIC_SITE_URL ?? "https://tenderscope.vercel.app";
  return ["", "/opportunities", "/analytics", "/methodology", "/workspace"].map((path, index) => ({
    url: `${base}${path}`,
    lastModified: new Date(),
    changeFrequency: index === 1 ? "hourly" : "daily",
    priority: index === 0 ? 1 : .8
  }));
}
