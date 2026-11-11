const API_URL = process.env.MEMBER_API_INTERNAL_URL || "http://localhost:5200/api/v1";

/** One institution the AlumUnion website features. A figure is absent unless the institution agreed to show it (and is never zero). */
export interface ShowcaseInstitution {
  name: string;
  slug: string;
  logoUrl: string | null;
  /** The institution's own brand colour, as #rrggbb. */
  primaryColor?: string | null;
  blurb: string | null;
  organizationType: string;
  portalUrl: string | null;
  members?: number | null;
  groups?: number | null;
  events?: number | null;
  raised?: number | null;
}

export interface Showcase {
  institutions: ShowcaseInstitution[];
  totals: { members: number; groups: number; events: number; raised: number };
}

/**
 * The institutions platform staff chose to feature, read on the server so the section is in the page when it arrives (no flash, and
 * search engines see it). Any failure means "none": the section is left out rather than shown broken or empty.
 */
export async function getShowcase(): Promise<Showcase | null> {
  try {
    const res = await fetch(`${API_URL}/showcase`, { next: { revalidate: 60 } });
    if (!res.ok) return null;
    const data = (await res.json())?.data as Showcase | undefined;
    return data && data.institutions.length > 0 ? data : null;
  } catch (err) {
    console.error("[showcase] fetch failed:", err);
    return null;
  }
}
