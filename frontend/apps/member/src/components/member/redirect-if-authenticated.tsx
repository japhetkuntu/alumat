"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import { isSafeRedirectPath } from "@/lib/redirect";

export function RedirectIfAuthenticated({ children }: { children: React.ReactNode }) {
  const { isLoading, isMember } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && isMember) {
      // Someone who signs in on this page (or just registered) goes where they were headed, not always the dashboard.
      const wanted = new URLSearchParams(window.location.search).get("redirect");
      router.replace(isSafeRedirectPath(wanted) ? wanted : "/dashboard");
    }
  }, [isLoading, isMember, router]);

  if (isLoading || isMember) return null;
  return <>{children}</>;
}
