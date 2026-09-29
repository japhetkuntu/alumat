import type { MetadataRoute } from "next";
import { getInstitutionTheme } from "@/lib/theme";

// A per-request manifest (not the static public/manifest.json this replaces) — the
// same reasoning as opengraph-image.tsx and layout.tsx's <html> theming: this is a
// multi-tenant app resolved from the request's Host header (see getInstitutionTheme),
// so "install as an app" needs to show that institution's own name and icon, not a
// generic "Member Portal" for everyone. Reading headers() inside a manifest route
// opts Next out of static generation for it, same as sitemap.ts already does.
export default async function manifest(): Promise<MetadataRoute.Manifest> {
  const theme = await getInstitutionTheme();
  const name = theme?.displayName || "Member Portal";
  // PWA short_names are shown under the home-screen icon with very little width —
  // long institution names get clipped by the OS anyway, so pre-truncate to
  // something that reads as a label rather than trusting the OS's own ellipsis.
  const shortName = name.length > 12 ? name.slice(0, 12) : name;

  return {
    name,
    short_name: shortName,
    description: theme?.tagline || "Stay connected with your community. Access jobs, events, campaigns and more.",
    start_url: "/",
    display: "standalone",
    background_color: "#ffffff",
    theme_color: theme?.primaryColorHex || "#2563EB",
    orientation: "portrait-primary",
    icons: theme?.iconUrl
      ? [
          // The institution's own uploaded icon, actual dimensions unknown — "any"
          // tells the OS to scale it rather than reject it for not matching a
          // declared size. The bundled maskable/apple icons still cover Android's
          // adaptive-icon safe zone and iOS's home-screen icon, which a single
          // arbitrary upload can't guarantee on its own.
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
    categories: ["education", "social"],
    lang: "en",
  };
}
