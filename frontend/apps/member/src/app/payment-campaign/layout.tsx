import type { Metadata } from "next";

// Campaign-specific payment links, only ever reached via a shared/invite URL
// — nothing here is a landing page a search query should surface.
export const metadata: Metadata = { robots: { index: false, follow: false } };

export default function PaymentCampaignLayout({ children }: { children: React.ReactNode }) {
  return children;
}
