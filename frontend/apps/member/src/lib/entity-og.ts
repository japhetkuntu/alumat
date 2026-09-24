import { headers, cookies } from "next/headers";
import type { Metadata } from "next";
import { getInstitutionTheme } from "@/lib/theme";
import { getRequestOrigin, SITE_NAME } from "@/lib/seo";

const API_URL = process.env.MEMBER_API_INTERNAL_URL || "http://localhost:5200/api/v1";

export type PreviewEntityType = "event" | "job" | "news" | "resource" | "business" | "community" | "album" | "service" | "campaign";

interface EntityPreview {
  title: string;
  description: string | null;
  imageUrl: string | null;
}

/** Server-side only — mirrors lib/theme.ts's fetchTheme() tenant-forwarding, so this
 * resolves the same institution the request's Host header maps to. */
async function fetchEntityPreview(type: PreviewEntityType, id: string): Promise<EntityPreview | null> {
  try {
    const requestHeaders = await headers();
    const host = requestHeaders.get("host") ?? "";
    const workspaceSlug = (await cookies()).get("institution_slug")?.value;
    const fetchHeaders: Record<string, string> = {};
    if (host) fetchHeaders["X-Internal-Tenant-Host"] = host;
    if (workspaceSlug) fetchHeaders["X-Institution-Slug"] = workspaceSlug;

    const res = await fetch(`${API_URL}/public/preview/${type}/${id}`, {
      headers: fetchHeaders,
      cache: "no-store",
    });
    if (!res.ok) return null;
    const body = await res.json();
    return body?.data ?? null;
  } catch (err) {
    console.error(`[entity-og] fetch preview ${type}/${id} failed:`, err);
    return null;
  }
}

/** Builds page-level Metadata for a shared detail page: real title/description/OG image
 * for the specific entity when found, falling back to the institution's generic
 * theme-based copy (never a hardcoded/broken title) when the entity is missing or the
 * fetch fails — a shared link should never look worse than the site default. */
export async function buildEntityMetadata(type: PreviewEntityType, id: string, fallbackDescription?: string): Promise<Metadata> {
  const [preview, theme, origin] = await Promise.all([
    fetchEntityPreview(type, id),
    getInstitutionTheme(),
    getRequestOrigin(),
  ]);

  // No entity found (deleted, wrong id, or the fetch itself failed) — return
  // nothing rather than re-deriving the same generic title/description the
  // root layout already provides, which would otherwise double up through
  // its "%s · {title}" template (e.g. "Member Portal · Member Portal").
  if (!preview) return {};

  const title = preview.title;
  const description = preview.description || fallbackDescription || theme?.tagline || undefined;
  const images = preview.imageUrl ? [{ url: preview.imageUrl }] : undefined;

  return {
    title,
    description,
    openGraph: {
      siteName: theme?.displayName ? `${theme.displayName} — ${SITE_NAME}` : SITE_NAME,
      title,
      description,
      images,
      url: `${origin}`,
      type: "website",
    },
    twitter: {
      card: images ? "summary_large_image" : "summary",
      title,
      description,
      images: images?.map((i) => i.url),
    },
  };
}
