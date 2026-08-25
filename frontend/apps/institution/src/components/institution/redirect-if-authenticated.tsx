"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";

export function RedirectIfAuthenticated({ children }: { children: React.ReactNode }) {
  const { isLoading, isAdmin } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && isAdmin) router.replace("/dashboard");
  }, [isLoading, isAdmin, router]);

  if (isLoading || isAdmin) return null;
  return <>{children}</>;
}
