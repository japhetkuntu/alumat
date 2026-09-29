import type { MetadataRoute } from "next";
import { getInstitutionTheme } from "@/lib/theme";

// A per-request manifest (not the static public/manifest.json this replaces) — see
// member/src/app/manifest.ts for the full reasoning: this is a multi-tenant app
// resolved from the request's Host header, so "install as an app" should show the
// institution's own name and icon, not a generic "Institution Portal" for everyone.
export default async function manifest(): Promise<MetadataRoute.Manifest> {
  const theme = await getInstitutionTheme();
  const name = theme?.displayName || "Institution Portal";
  const shortName = name.length > 12 ? name.slice(0, 12) : name;

  return {
    name,
    short_name: shortName,
    description: theme?.tagline || "Institution portal for managing your community.",
    start_url: "/",
    display: "standalone",
    background_color: "#ffffff",
    theme_color: theme?.primaryColorHex || "#2563EB",
    orientation: "portrait-primary",
    icons: theme?.iconUrl
      ? [
          { src: theme.iconUrl, sizes: "any", type: "image/png", purpose: "any" },
          { src: "/icon-maskable-192.png", sizes: "192x192", type: "image/png", purpose: "maskable" },
          { src: "/icon-maskable-512.png", sizes: "512x512", type: "image/png", purpose: "maskable" },
        ]
      : [
          { src: "/logo.svg", sizes: "any", type: "image/svg+xml", purpose: "any" },
          { src: "/icon-192.png", sizes: "192x192", type: "image/png", purpose: "any" },
          { src: "/icon-512.png", sizes: "512x512", type: "image/png", purpose: "any" },
          { src: "/icon-maskable-192.png", sizes: "192x192", type: "image/png", purpose: "maskable" },
          { src: "/icon-maskable-512.png", sizes: "512x512", type: "image/png", purpose: "maskable" },
        ],
    categories: ["education", "productivity"],
    lang: "en",
  };
}
