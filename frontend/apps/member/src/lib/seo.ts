import { headers } from "next/headers";

/** Product-wide constants — the marketing site's own copy, independent of
 *  any institution's branding. */
export const SITE_NAME = "AlumUnion";
export const MARKETING_TITLE = "AlumUnion — Community Platform for Institutions";
export const MARKETING_DESCRIPTION =
  "AlumUnion gives institutions a home for their community — from alumni and former students to members, supporters, and stakeholders. Build, organize, and grow a structured community with a directory, events, fundraising, memberships, and more.";
export const MARKETING_KEYWORDS = [
  "institution community platform",
  "community platform for institutions",
  "alumni association software",
  "school alumni community platform",
  "university community management platform",
  "member management platform",
  "institutional community portal",
  "alumni network software",
  "association membership platform",
  "community engagement platform",
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
