import { cache } from "react";
import { cookies, headers } from "next/headers";
import { siteDataFromRaw, type RawSiteData, type SiteBrand, type SiteData, type SiteDocument } from "@alumni/site";
import type { InstitutionTheme } from "@/lib/theme";

// Same server-side address and tenant forwarding as lib/theme.ts: the visitor's own Host decides whose website this is.
const API_URL = process.env.MEMBER_API_INTERNAL_URL || "http://localhost:5200/api/v1";

async function tenantHeaders(): Promise<Record<string, string>> {
  const h: Record<string, string> = {};
  const host = (await headers()).get("host") ?? "";
  const slug = (await cookies()).get("institution_slug")?.value;
  if (host) h["X-Internal-Tenant-Host"] = host;
  if (slug) h["X-Institution-Slug"] = slug;
  return h;
}

async function getJson<T>(path: string, timeoutMs = 3000): Promise<{ status: number; data: T | null }> {
  try {
    const res = await fetch(`${API_URL}${path}`, { headers: await tenantHeaders(), cache: "no-store", signal: AbortSignal.timeout(timeoutMs) });
    if (!res.ok) return { status: res.status, data: null };
    const body = await res.json();
    return { status: res.status, data: (body?.data ?? null) as T | null };
  } catch (err) {
    console.error(`[site] ${path} failed:`, err);
    return { status: 0, data: null };
  }
}

export interface PublishedSite { document: SiteDocument; revision: number; publishedAt: string | null }

/** The institution's published website page, or null when it has none (the standard landing page is shown then). */
export const getPublishedSite = cache(async (): Promise<PublishedSite | null> => {
  const { data } = await getJson<{ document: SiteDocument; revision: number; publishedAt: string | null }>("/public/site");
  return data && data.document ? { document: data.document, revision: data.revision, publishedAt: data.publishedAt } : null;
});

/** Everything the live sections show, from the institution's own public items. A slow or failing call leaves those sections out rather than breaking the page. */
export const getSiteData = cache(async (): Promise<SiteData> => {
  const { data } = await getJson<RawSiteData>("/public/site-data");
  return data ? siteDataFromRaw(data) : {};
});

export const brandFromTheme = (t: InstitutionTheme): SiteBrand => ({
  name: t.displayName || t.portalName, tagline: t.tagline, primary: t.primaryColorHex, secondary: t.secondaryColorHex, logoUrl: t.logoUrl, slug: t.slug,
});
