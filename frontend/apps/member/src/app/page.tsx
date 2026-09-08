import { getInstitutionThemeStatus } from "@/lib/theme";
import LandingPage from "./landing-page";
import PlatformMarketingPage from "./platform-marketing-page";

// Root route only — decides, per request, whether the Host header resolved to
// a real institution (render its branded landing page, same as always) or to
// nothing at all (the bare/main domain — render the platform's own marketing
// site instead). A transient backend error is deliberately treated the same
// as "resolved" here: it falls through to LandingPage, which already
// degrades gracefully to generic placeholder branding on its own client-side
// fetch — an outage must never misclassify a real institution visitor as a
// marketing-site visitor.
export default async function RootPage() {
  const { status, theme } = await getInstitutionThemeStatus();
  if (status === "not-found") {
    return <PlatformMarketingPage />;
  }
  // Passed through as SSR-resolved initial data (see useLandingContent in
  // landing-page.tsx) so the first paint already has the real institution's
  // branding — no flash of generic placeholder content while a client-side
  // fetch is in flight.
  return <LandingPage initialContent={theme} />;
}
