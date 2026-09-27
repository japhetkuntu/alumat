import type { Metadata } from "next";
import localFont from "next/font/local";
import "./globals.css";
import { Providers } from "@/components/shared/providers";

// IBM Plex Sans is the platform's single, permanent typeface — every portal,
// every tenant. Loaded from local files (next/font/local), not
// next/font/google, so the build never depends on reaching Google's font CDN
// (that fetch failed outright on the production host). See
// apps/member/src/app/layout.tsx for the full rationale.
const ibmPlexSans = localFont({
  src: [
    { path: "../fonts/ibm-plex-sans-300.woff2", weight: "300", style: "normal" },
    { path: "../fonts/ibm-plex-sans-400.woff2", weight: "400", style: "normal" },
    { path: "../fonts/ibm-plex-sans-500.woff2", weight: "500", style: "normal" },
    { path: "../fonts/ibm-plex-sans-600.woff2", weight: "600", style: "normal" },
    { path: "../fonts/ibm-plex-sans-700.woff2", weight: "700", style: "normal" },
  ],
  variable: "--font-sans",
  display: "swap",
});

export const metadata: Metadata = {
  title: "Platform Portal",
  description: "Onboard and manage every institution on the platform.",
  manifest: "/manifest.json",
  themeColor: "#2563EB",
  appleWebApp: {
    capable: true,
    statusBarStyle: "default",
    title: "Platform Portal",
  },
  // Internal staff backoffice — never meant to be discoverable or indexed.
  robots: { index: false, follow: false, nocache: true },
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning className={ibmPlexSans.variable}>
      <body className="min-h-screen bg-background font-sans antialiased">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
