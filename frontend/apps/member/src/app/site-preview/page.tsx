import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { buildTemplate } from "@alumni/site";
import { getInstitutionTheme } from "@/lib/theme";
import { brandFromTheme, getPublishedSite, getSiteData } from "@/lib/site";
import { WebsitePage } from "../_site/website-page";

// Local development only: the root page shows the platform's own marketing site on a bare local host, so this shows the published
// website of the institution picked with the dev workspace cookie. It does not exist in production.
// `?template=journal` shows a fresh starter page for that look instead, built from the institution's own branding and items.
export const metadata: Metadata = { robots: { index: false, follow: false } };

export default async function SitePreview({ searchParams }: { searchParams: Promise<{ template?: string }> }) {
  if (process.env.NODE_ENV !== "development") notFound();
  const { template } = await searchParams;
  const [theme, site] = await Promise.all([getInstitutionTheme(), getPublishedSite()]);
  if (!theme) notFound();
  const brand = brandFromTheme(theme);
  const doc = template ? buildTemplate(template, { brand }) : site?.document;
  if (!doc) notFound();
  return <WebsitePage doc={doc} brand={brand} data={await getSiteData()} platformDisabled={theme.disabledFeatures ?? []} />;
}
