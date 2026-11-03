import type { Metadata } from "next";
import { cookies, headers } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { buildEntityMetadata, type PreviewEntityType } from "@/lib/entity-og";
import { getInstitutionTheme } from "@/lib/theme";
import { brandFromTheme, getPublishedSite, getSiteData } from "@/lib/site";
import { WebsitePage } from "../../../_site/website-page";
import { ReaderArticle, type ReaderPreview } from "../../../_site/reader-article";

const API_URL = process.env.MEMBER_API_INTERNAL_URL || "http://localhost:5200/api/v1";

/** The kinds of item the website links to, and where each lives in the member portal (for signing in and coming back to it). */
const KINDS: Record<string, (id: string) => string> = {
  news: (id) => `/news/${id}`,
  event: (id) => `/events/${id}`,
  job: (id) => `/jobs/${id}`,
  community: (id) => `/communities/${id}`,
  spotlight: () => "/spotlights",
};

async function getPreview(type: string, id: string): Promise<ReaderPreview | null> {
  try {
    const h: Record<string, string> = {};
    const host = (await headers()).get("host") ?? "";
    const slug = (await cookies()).get("institution_slug")?.value;
    if (host) h["X-Internal-Tenant-Host"] = host;
    if (slug) h["X-Institution-Slug"] = slug;
    const res = await fetch(`${API_URL}/public/preview/${type}/${encodeURIComponent(id)}`, { headers: h, cache: "no-store" });
    if (!res.ok) return null;
    return ((await res.json())?.data ?? null) as ReaderPreview | null;
  } catch { return null; }
}

export async function generateMetadata({ params }: { params: Promise<{ type: string; id: string }> }): Promise<Metadata> {
  const { type, id } = await params;
  if (!(type in KINDS)) return {};
  return buildEntityMetadata(type as PreviewEntityType, id);
}

export default async function ReadPage({ params }: { params: Promise<{ type: string; id: string }> }) {
  const { type, id } = await params;
  const portal = KINDS[type];
  if (!portal) notFound();
  const [theme, site, preview] = await Promise.all([getInstitutionTheme(), getPublishedSite(), getPreview(type, id)]);
  // Without a published website there is no site to draw this in: the portal's own page handles the link instead.
  if (!theme || !site) redirect(portal(id));
  if (!preview) notFound();
  const orgName = theme.displayName || theme.portalName || "us";
  return (
    <WebsitePage doc={site.document} brand={brandFromTheme(theme)} data={await getSiteData()} platformDisabled={theme.disabledFeatures ?? []}
      reader={<ReaderArticle type={type} preview={preview} portalPath={portal(id)} orgName={orgName} />} />
  );
}
