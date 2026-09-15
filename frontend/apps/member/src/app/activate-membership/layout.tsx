import type { Metadata } from "next";

// Campaign-specific activation links, only ever reached via a personal
// invite — nothing here is a landing page a search query should surface.
export const metadata: Metadata = { robots: { index: false, follow: false } };

export default function ActivateMembershipLayout({ children }: { children: React.ReactNode }) {
  return children;
}
