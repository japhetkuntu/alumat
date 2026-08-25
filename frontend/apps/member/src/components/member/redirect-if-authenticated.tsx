"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";

export function RedirectIfAuthenticated({ children }: { children: React.ReactNode }) {
  const { isLoading, isMember } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && isMember) router.replace("/dashboard");
  }, [isLoading, isMember, router]);

  if (isLoading || isMember) return null;
  return <>{children}</>;
}
