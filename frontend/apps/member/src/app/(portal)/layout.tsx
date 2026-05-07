import { MemberLayout } from "@/components/member/member-layout";
import { ReactNode } from "react";

export default function PortalGroupLayout({ children }: { children: ReactNode }) {
  return <MemberLayout>{children}</MemberLayout>;
}
