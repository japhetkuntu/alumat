import type { Metadata } from "next";
import { getRequestOrigin, SITE_NAME } from "@/lib/seo";

const TITLE = "AlumUnion Founding 20 | " + SITE_NAME;
const DESCRIPTION =
  "We're partnering with 20 institutions and organized communities in Ghana to build stronger, more connected digital communities. No setup cost. Applications close October 30, 2026.";

export async function generateMetadata(): Promise<Metadata> {
  const origin = await getRequestOrigin();
  const url = `${origin}/founding20`;
  return {
    title: { absolute: TITLE },
    description: DESCRIPTION,
    alternates: { canonical: url },
    openGraph: { title: TITLE, description: DESCRIPTION, url, type: "website" },
    twitter: { card: "summary_large_image", title: TITLE, description: DESCRIPTION },
  };
}

export default function Founding20Layout({ children }: { children: React.ReactNode }) {
  return children;
}
