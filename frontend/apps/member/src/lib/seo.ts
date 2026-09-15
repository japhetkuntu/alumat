import { headers } from "next/headers";

/** Product-wide constants — the marketing site's own copy, independent of
 *  any institution's branding. */
export const SITE_NAME = "AlumUnion";
export const MARKETING_TITLE = "AlumUnion — Free Alumni Portal Software for Schools & Universities";
export const MARKETING_DESCRIPTION =
  "AlumUnion is a free, dedicated alumni platform for schools and universities: a searchable directory, events with RSVPs, dues and fundraising collection, a jobs board, mentorship matching, and more — replacing scattered WhatsApp groups and spreadsheets with one branded portal your institution controls.";
export const MARKETING_KEYWORDS = [
  "alumni portal software",
  "alumni management platform",
  "alumni directory software",
  "school alumni website",
  "university alumni platform",
  "alumni engagement software",
  "alumni association software",
  "free alumni portal",
  "alumni fundraising platform",
  "alumni network app",
];

/** Reconstructs the current request's origin from its Host header — this app
 * is multi-tenant over the Host header (see lib/theme.ts), so there is no
 * single fixed site URL to hardcode; metadataBase/canonical/OG urls all need
 * to resolve to whichever host actually served the request. */
export async function getRequestOrigin(): Promise<string> {
  const h = await headers();
  const host = h.get("host") ?? process.env.NEXT_PUBLIC_BASE_DOMAIN ?? "localhost:3000";
  const protocol = host.startsWith("localhost") || host.startsWith("127.0.0.1") ? "http" : "https";
  return `${protocol}://${host}`;
}
