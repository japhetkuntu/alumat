"use client";

import { WebsiteEditor } from "@/components/website/website-editor";
import { useAuth } from "@/hooks/use-auth";
import { EmptyState } from "@alumni/ui";

// A published page speaks for the whole institution, so only a SuperAdmin can edit it.
export default function WebsitePage() {
  const { user } = useAuth();
  if (user && user.role !== "SuperAdmin") {
    return <div className="mx-auto max-w-[640px] p-6"><EmptyState title="Only a super admin can edit the website" description="Ask one of your institution's super admins to make changes to it." /></div>;
  }
  return <WebsiteEditor />;
}
