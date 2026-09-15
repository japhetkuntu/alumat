import type { Metadata } from "next";

// OAuth callback bounce page — never a real destination, must not be indexed.
export const metadata: Metadata = { robots: { index: false, follow: false } };

export default function GoogleAuthLayout({ children }: { children: React.ReactNode }) {
  return children;
}
