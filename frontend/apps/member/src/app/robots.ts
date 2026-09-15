import type { MetadataRoute } from "next";
import { getRequestOrigin } from "@/lib/seo";

// See layout.tsx's `dynamic` export comment — this route reads headers() on
// every request (via getRequestOrigin) and can never be static.
export const dynamic = "force-dynamic";

// Served on every host this app answers to (the bare marketing domain and
// every {slug}.<domain> tenant alike) — disallow paths are route shapes that
// exist identically across all of them: auth flows, the logged-in portal,
// and one-off campaign/callback links. See (portal)/layout.tsx and
// (auth)/layout.tsx for the matching <meta robots> noindex on those same
// paths — this keeps crawlers from even requesting them, defense in depth
// alongside that per-page tag.
export default async function robots(): Promise<MetadataRoute.Robots> {
  const origin = await getRequestOrigin();
  return {
    rules: {
      userAgent: "*",
      allow: "/",
      disallow: [
        "/login",
        "/register",
        "/forgot-password",
        "/reset-password",
        "/dashboard",
        "/settings",
        "/directory",
        "/events",
        "/jobs",
        "/mentorship",
        "/forum",
        "/communities",
        "/news",
        "/spotlights",
        "/albums",
        "/store",
        "/services",
        "/resources",
        "/contributions",
        "/leaderboard",
        "/business-directory",
        "/alumni-map",
        "/membership-certificate",
        "/notifications",
        "/profile",
        "/calendar",
        "/activate-membership",
        "/payment-campaign",
        "/google-auth",
        "/api/",
      ],
    },
    sitemap: `${origin}/sitemap.xml`,
    host: origin,
  };
}
