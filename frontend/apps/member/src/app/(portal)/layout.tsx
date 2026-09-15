import type { Metadata } from "next";
import { MemberLayout } from "@/components/member/member-layout";
import { ReactNode } from "react";

// Everything under here sits behind login — nothing for a crawler to index,
// and a stray indexed URL would just point searchers at a login wall.
export const metadata: Metadata = { robots: { index: false, follow: false } };

export default function PortalGroupLayout({ children }: { children: ReactNode }) {
  return <MemberLayout>{children}</MemberLayout>;
}
