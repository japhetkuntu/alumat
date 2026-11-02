import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { getInstitutionTheme } from "@/lib/theme";
import { brandFromTheme, getPublishedSite, getSiteData } from "@/lib/site";
import { WebsitePage } from "../_site/website-page";

// Local development only: the root page shows the platform's own marketing site on a bare local host, so this shows the published
// website of the institution picked with the dev workspace cookie. It does not exist in production.
export const metadata: Metadata = { robots: { index: false, follow: false } };

export default async function SitePreview() {
  if (process.env.NODE_ENV !== "development") notFound();
  const [theme, site] = await Promise.all([getInstitutionTheme(), getPublishedSite()]);
  if (!theme || !site) notFound();
  return <WebsitePage doc={site.document} brand={brandFromTheme(theme)} data={await getSiteData()} platformDisabled={theme.disabledFeatures ?? []} />;
}
