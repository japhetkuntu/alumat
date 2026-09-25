import type { Metadata } from "next";
import { getRequestOrigin, SITE_NAME } from "@/lib/seo";

const TITLE = "Why Not Run Your Community on WhatsApp? | " + SITE_NAME;
const DESCRIPTION =
  "WhatsApp groups cap at 1,024 members, have no real search, no directory, and are a documented fraud target. See the sourced comparison and what a real community portal gives you instead.";

// Client component (page.tsx) can't export its own metadata, so this
// sibling server-component layout carries it instead — same pattern as the
// noindex wrappers, but this page is genuine public content and should rank.
export async function generateMetadata(): Promise<Metadata> {
  const origin = await getRequestOrigin();
  const url = `${origin}/why-not-whatsapp`;
  return {
    title: { absolute: TITLE },
    description: DESCRIPTION,
    alternates: { canonical: url },
    openGraph: { title: TITLE, description: DESCRIPTION, url, type: "article" },
    twitter: { card: "summary_large_image", title: TITLE, description: DESCRIPTION },
  };
}

export default function WhyNotWhatsappLayout({ children }: { children: React.ReactNode }) {
  return children;
}
