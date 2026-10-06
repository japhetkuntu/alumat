import { headers, cookies } from "next/headers";

const API_URL = process.env.MEMBER_API_INTERNAL_URL || "http://localhost:5200/api/v1";

/** The API leaves out any figure the institution chose not to show, so treat `undefined` and `null` alike (use `!= null`). What an anonymous visitor may see. Every figure is null unless the institution chose to show it; never any one person's amount. */
export interface PublicFundraiser {
  id: string;
  title: string;
  description: string | null;
  bannerImageUrl: string | null;
  youtubeVideoUrl: string | null;
  message: string | null;
  isOpenForGiving: boolean;
  totalRaised?: number | null;
  targetAmount?: number | null;
  progressPercent?: number | null;
  contributorCount?: number | null;
  deadline?: string | null;
  contributors: string[];
}

/** Server-side only, same tenant forwarding as lib/theme.ts. Returns null for anything not published (the API answers 404). */
export async function getPublicFundraiser(id: string): Promise<PublicFundraiser | null> {
  try {
    const requestHeaders = await headers();
    const host = requestHeaders.get("host") ?? "";
    const workspaceSlug = (await cookies()).get("institution_slug")?.value;
    const fetchHeaders: Record<string, string> = {};
    if (host) fetchHeaders["X-Internal-Tenant-Host"] = host;
    if (workspaceSlug) fetchHeaders["X-Institution-Slug"] = workspaceSlug;

    const res = await fetch(`${API_URL}/campaigns/${encodeURIComponent(id)}/public-page`, { headers: fetchHeaders, cache: "no-store" });
    if (!res.ok) return null;
    return (await res.json())?.data ?? null;
  } catch (err) {
    console.error(`[public-fundraiser] fetch ${id} failed:`, err);
    return null;
  }
}
