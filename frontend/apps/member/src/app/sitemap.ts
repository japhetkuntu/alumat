import type { MetadataRoute } from "next";
import { getRequestOrigin } from "@/lib/seo";
import { getInstitutionThemeStatus } from "@/lib/theme";

// See layout.tsx's `dynamic` export comment — same reasoning applies here:
// this route reads headers() (via getRequestOrigin/getInstitutionThemeStatus)
// on every request, so it can never be static; declaring that upfront skips
// Next's futile static-generation attempt and its noisy build-log error.
export const dynamic = "force-dynamic";

// Served per-host, same as robots.ts. On the bare marketing domain this
// lists the marketing site's own public pages; on a tenant subdomain, the
// institution's public landing page is the only page worth listing —
// everything else in (portal) is behind login and excluded from crawling
// via robots.ts + noindex.
export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const [origin, { status }] = await Promise.all([getRequestOrigin(), getInstitutionThemeStatus()]);

  if (status !== "ok") {
    return [
      { url: `${origin}/`, changeFrequency: "weekly", priority: 1 },
      { url: `${origin}/why-not-whatsapp`, changeFrequency: "monthly", priority: 0.8 },
      { url: `${origin}/privacy`, changeFrequency: "yearly", priority: 0.3 },
      { url: `${origin}/terms`, changeFrequency: "yearly", priority: 0.3 },
      { url: `${origin}/institution-agreement`, changeFrequency: "yearly", priority: 0.3 },
    ];
  }

  return [{ url: `${origin}/`, changeFrequency: "weekly", priority: 1 }];
}
