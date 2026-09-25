import type { Metadata } from "next";
import { getInstitutionThemeStatus } from "@/lib/theme";
import LandingPage from "./landing-page";
import PlatformMarketingPage from "./platform-marketing-page";
import { FAQS } from "./_marketing/faqs";
import { getRequestOrigin, MARKETING_DESCRIPTION, MARKETING_KEYWORDS, MARKETING_TITLE, SITE_NAME } from "@/lib/seo";

// Same Host-resolved branch as the page component below, but metadata is
// generated up front by Next before render — duplicated here rather than
// derived from the render because generateMetadata and the page component
// run as separate calls and can't share local state.
export async function generateMetadata(): Promise<Metadata> {
  const [{ status, theme }, origin] = await Promise.all([getInstitutionThemeStatus(), getRequestOrigin()]);
  const url = `${origin}/`;

  // Mirrors RootPage's own branch below exactly: only "not-found" (the bare
  // marketing domain) renders PlatformMarketingPage. A transient backend
  // "error" still renders LandingPage (degrading to generic placeholder
  // branding), so its metadata must match that — not claim marketing copy
  // for a page that isn't actually showing it.
  if (status === "not-found") {
    return {
      title: { absolute: MARKETING_TITLE },
      description: MARKETING_DESCRIPTION,
      keywords: MARKETING_KEYWORDS,
      alternates: { canonical: url },
      openGraph: { title: MARKETING_TITLE, description: MARKETING_DESCRIPTION, url, type: "website" },
      twitter: { card: "summary_large_image", title: MARKETING_TITLE, description: MARKETING_DESCRIPTION },
    };
  }

  const name = theme?.displayName || theme?.portalName;
  const title = name ? `${name} Member Portal | ${SITE_NAME}` : "Member Portal";
  const description = theme?.tagline
    ? `${theme.tagline} — the official ${name} community portal. Directory, events, jobs, dues, and mentorship, all in one place.`
    : `A searchable directory, events, jobs, dues collection, and community updates for members — all in one place, powered by ${SITE_NAME}.`;

  return {
    title: { absolute: title },
    description,
    alternates: { canonical: url },
    openGraph: { title, description, url, type: "website", images: theme?.logoUrl ? [theme.logoUrl] : undefined },
    twitter: { card: "summary_large_image", title, description },
  };
}

/** Structured data (schema.org, as JSON-LD) so search engines can render a
 * FAQ rich result on the marketing homepage and attribute the site to a real
 * Organization — separate from React state, so it's built directly from
 * status/theme rather than passed through client components. */
function StructuredData({ isMarketing, theme, origin }: { isMarketing: boolean; theme: Awaited<ReturnType<typeof getInstitutionThemeStatus>>["theme"]; origin: string }) {
  const graphs: object[] = [];

  if (isMarketing || !theme) {
    graphs.push({
      "@context": "https://schema.org",
      "@type": "SoftwareApplication",
      "@id": `${origin}/#organization`,
      name: SITE_NAME,
      url: origin,
      description: MARKETING_DESCRIPTION,
      applicationCategory: "BusinessApplication",
      operatingSystem: "Web",
    });
    graphs.push({
      "@context": "https://schema.org",
      "@type": "FAQPage",
      mainEntity: FAQS.map(({ q, a }) => ({
        "@type": "Question",
        name: q,
        acceptedAnswer: { "@type": "Answer", text: a },
      })),
    });
  } else {
    // A Community-type institution (see the platform's configurable
    // OrganizationType) is not an educational body — EducationalOrganization
    // would misdescribe it to search engines and any AI system reading this
    // markup, so only Alumni institutions get the more specific type.
    const sameAs = theme.socialLinks ? Object.values(theme.socialLinks).filter(Boolean) : undefined;
    graphs.push({
      "@context": "https://schema.org",
      "@type": theme.organizationType === "Community" ? "Organization" : "EducationalOrganization",
      "@id": `${origin}/#organization`,
      name: theme.displayName || theme.portalName,
      url: origin,
      ...(theme.logoUrl ? { logo: theme.logoUrl } : {}),
      ...(theme.tagline ? { description: theme.tagline } : {}),
      ...(sameAs && sameAs.length > 0 ? { sameAs } : {}),
    });
  }

  return (
    <>
      {graphs.map((g, i) => (
        // "<" is escaped so institution-supplied strings (tagline, name) can
        // never prematurely close this </script> tag.
        <script key={i} type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(g).replace(/</g, "\\u003c") }} />
      ))}
    </>
  );
}

// Root route only — decides, per request, whether the Host header resolved to
// a real institution (render its branded landing page, same as always) or to
// nothing at all (the bare/main domain — render the platform's own marketing
// site instead). A transient backend error is deliberately treated the same
// as "resolved" here: it falls through to LandingPage, which already
// degrades gracefully to generic placeholder branding on its own client-side
// fetch — an outage must never misclassify a real institution visitor as a
// marketing-site visitor.
export default async function RootPage() {
  const [{ status, theme }, origin] = await Promise.all([getInstitutionThemeStatus(), getRequestOrigin()]);
  console.log(`RootPage: status=${status}, theme=${theme?.portalName}, origin=${origin}`);
  if (status === "not-found" || status === "error" ) {
    return (
      <>
        <StructuredData isMarketing theme={theme} origin={origin} />
        <PlatformMarketingPage />
      </>
    );
  }
  // Passed through as SSR-resolved initial data (see useLandingContent in
  // landing-page.tsx) so the first paint already has the real institution's
  // branding — no flash of generic placeholder content while a client-side
  // fetch is in flight.
  return (
    <>
      <StructuredData isMarketing={false} theme={theme} origin={origin} />
      <LandingPage initialContent={theme} />
    </>
  );
}
