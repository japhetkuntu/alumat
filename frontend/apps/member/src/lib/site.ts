import { cache } from "react";
import { cookies, headers } from "next/headers";
import type { SiteBrand, SiteData, SiteDocument } from "@alumni/site";
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

interface RawSiteData {
  news: { id: string; title: string; excerpt?: string; imageUrl?: string | null; publishedAt?: string | null; category?: string }[];
  events: { id: string; title: string; startDate: string; venue?: string; bannerImageUrl?: string | null }[];
  communities: { id: string; name: string; memberCount: number; description?: string; coverImageUrl?: string | null }[];
  businesses: { id: string; businessName: string; description?: string; logoUrl?: string | null; bannerUrl?: string | null; location?: string }[];
  services: { id: string; name: string; description?: string | null; price: number }[];
  store: { id: string; name: string; price: number; imageUrl?: string | null }[];
  fundraisers: { id: string; title: string; summary?: string | null; raised: number; goal: number; imageUrl?: string | null }[];
  jobs: { id: string; title: string; company?: string | null; location?: string | null }[];
  spotlights: { id: string; title: string; story?: string | null; imageUrl?: string | null; memberName?: string | null }[];
  stats: { members: number; joinedLast30Days: number; eventsNext30Days?: number | null; openJobs?: number | null; businesses?: number | null; communities: number };
}

/** Everything the live sections show, from the institution's own public items. A slow or failing call leaves those sections out rather than breaking the page. */
export const getSiteData = cache(async (): Promise<SiteData> => {
  const { data: d } = await getJson<RawSiteData>("/public/site-data");
  if (!d) return {};
  return {
    news: d.news.map((n) => ({ id: n.id, title: n.title, excerpt: n.excerpt, imageUrl: n.imageUrl, publishedAt: n.publishedAt, category: n.category })),
    events: d.events.map((e) => ({ id: e.id, title: e.title, startDate: e.startDate, venue: e.venue, bannerImageUrl: e.bannerImageUrl })),
    communities: d.communities.map((c) => ({ id: c.id, name: c.name, memberCount: c.memberCount, description: c.description, imageUrl: c.coverImageUrl })),
    businesses: d.businesses.map((b) => ({ id: b.id, name: b.businessName, category: b.location, imageUrl: b.logoUrl ?? b.bannerUrl })),
    services: d.services,
    store: d.store.map((p) => ({ id: p.id, name: p.name, price: p.price, imageUrl: p.imageUrl })),
    fundraisers: d.fundraisers.map((f) => ({ id: f.id, title: f.title, summary: f.summary, raised: f.raised, goal: f.goal, imageUrl: f.imageUrl })),
    jobs: d.jobs,
    spotlights: (d.spotlights ?? []).map((s) => ({ id: s.id, title: s.title, story: s.story ? s.story.replace(/<[^>]*>/g, " ").replace(/\s+/g, " ").trim() : s.story, imageUrl: s.imageUrl, memberName: s.memberName })),
    stats: { members: d.stats.members, joinedLast30Days: d.stats.joinedLast30Days, eventsNext30Days: d.stats.eventsNext30Days, openJobs: d.stats.openJobs, businesses: d.stats.businesses, communities: d.stats.communities },
  };
});

export const brandFromTheme = (t: InstitutionTheme): SiteBrand => ({
  name: t.displayName || t.portalName, tagline: t.tagline, primary: t.primaryColorHex, secondary: t.secondaryColorHex, logoUrl: t.logoUrl, slug: t.slug,
});
