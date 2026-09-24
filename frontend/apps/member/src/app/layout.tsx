import type { Metadata, Viewport } from "next";
import { IBM_Plex_Sans } from "next/font/google";
import "./globals.css";
import { Providers } from "@/components/shared/providers";
import { GoogleAnalytics } from "@/components/shared/google-analytics";
import { getInstitutionTheme, themeStyleVars } from "@/lib/theme";
import { getRequestOrigin, SITE_NAME } from "@/lib/seo";

// IBM Plex Sans is the platform's single, permanent typeface — every portal,
// every tenant. Assigned to both --font-sans and (via globals.css's @theme
// block) --font-display, so the existing font-[family-name:var(--font-display)]
// call sites across the app keep working unchanged, just rendering in a
// heavier weight of this same family instead of a separate serif.
const ibmPlexSans = IBM_Plex_Sans({
  subsets: ["latin"],
  variable: "--font-sans",
  weight: ["300", "400", "500", "600", "700"],
  display: "swap",
});

// Every route needs per-request rendering (tenant/theme is resolved from the
// request's Host header — see getInstitutionTheme). Without this, `next
// build` still attempts static generation for each route first, hits
// headers() inside that fetch, and bails with a DYNAMIC_SERVER_USAGE error —
// harmless (every route ends up correctly marked dynamic either way) but it
// floods the build log with noise that looks like real fetch failures.
// Declaring it upfront skips that futile attempt entirely.
export const dynamic = "force-dynamic";

// Dynamic per-institution: browser tab title and favicon are configured by
// platform staff (MemberPortalTitle / IconUrl), not hardcoded — falls back
// to generic copy when an institution hasn't set one.
export async function generateMetadata(): Promise<Metadata> {
  const [theme, origin] = await Promise.all([getInstitutionTheme(), getRequestOrigin()]);
  const title = theme?.portalTitle || theme?.portalName || "Member Portal";
  const description = theme?.tagline
    ? `${theme.tagline} — the official ${theme.displayName ?? "community"} portal.`
    : "A searchable directory, events, dues, jobs, and community updates for members, in one place.";

  // Root-level defaults only — every route inherits these unless it sets its
  // own (page.tsx overrides title/description/OG for "/", legal pages set
  // their own title, (portal)/(auth) route groups override robots to
  // noindex). metadataBase is set here once so every other generateMetadata
  // in this app can hand back relative OG/icon paths.
  return {
    metadataBase: new URL(origin),
    title: { default: title, template: `%s · ${title}` },
    description,
    manifest: "/manifest.json",
    icons: theme?.iconUrl ? { icon: theme.iconUrl } : undefined,
    appleWebApp: {
      capable: true,
      statusBarStyle: "default",
      title,
    },
    openGraph: {
      siteName: theme?.displayName ? `${theme.displayName} — ${SITE_NAME}` : SITE_NAME,
      title,
      description,
      type: "website",
      locale: "en_US",
    },
    twitter: {
      card: "summary_large_image",
      title,
      description,
    },
    robots: { index: true, follow: true },
  };
}

export async function generateViewport(): Promise<Viewport> {
  const theme = await getInstitutionTheme();
  // A custom `generateViewport` REPLACES Next.js's default viewport meta tag
  // rather than merging with it — omitting width/initialScale here meant no
  // `width=device-width, initial-scale=1` was ever rendered at all, so mobile
  // browsers fell back to a ~980px desktop layout viewport and scaled the
  // whole page down, which is exactly what "have to manually zoom in/out to
  // align it" looks like.
  return { width: "device-width", initialScale: 1, themeColor: theme?.primaryColorHex || "#2563EB" };
}

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  const theme = await getInstitutionTheme();

  return (
    <html
      lang="en"
      suppressHydrationWarning
      className={ibmPlexSans.variable}
      style={themeStyleVars(theme)}
    >
      <body className="min-h-screen bg-background font-sans antialiased">
        {process.env.NEXT_PUBLIC_GA_MEASUREMENT_ID && (
          <GoogleAnalytics measurementId={process.env.NEXT_PUBLIC_GA_MEASUREMENT_ID} />
        )}
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
