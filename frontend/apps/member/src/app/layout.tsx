import type { Metadata, Viewport } from "next";
import { Inter, Lora } from "next/font/google";
import "./globals.css";
import { Providers } from "@/components/shared/providers";
import { getInstitutionTheme, themeStyleVars } from "@/lib/theme";

const inter = Inter({
  subsets: ["latin"],
  variable: "--font-sans",
  weight: ["300", "400", "500", "600", "700"],
  display: "swap",
});

const lora = Lora({
  subsets: ["latin"],
  variable: "--font-display",
  weight: ["400", "500", "600"],
  display: "swap",
});

// Dynamic per-institution: browser tab title and favicon are configured by
// platform staff (MemberPortalTitle / IconUrl), not hardcoded — falls back
// to generic copy when an institution hasn't set one.
export async function generateMetadata(): Promise<Metadata> {
  const theme = await getInstitutionTheme();
  const title = theme?.portalTitle || theme?.portalName || "Alumni Portal";
  return {
    title,
    description: "Alumni Member Portal",
    manifest: "/manifest.json",
    icons: theme?.iconUrl ? { icon: theme.iconUrl } : undefined,
    appleWebApp: {
      capable: true,
      statusBarStyle: "default",
      title,
    },
  };
}

export async function generateViewport(): Promise<Viewport> {
  const theme = await getInstitutionTheme();
  return { themeColor: theme?.primaryColorHex || "#4f46e5" };
}

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  const theme = await getInstitutionTheme();

  return (
    <html
      lang="en"
      suppressHydrationWarning
      className={`${inter.variable} ${lora.variable}`}
      style={themeStyleVars(theme)}
    >
      <body className="min-h-screen bg-background font-sans antialiased">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
