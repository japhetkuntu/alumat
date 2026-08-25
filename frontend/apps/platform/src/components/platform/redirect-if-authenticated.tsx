"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";

export function RedirectIfAuthenticated({ children }: { children: React.ReactNode }) {
  const { isLoading, isPlatformStaff } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && isPlatformStaff) router.replace("/dashboard");
  }, [isLoading, isPlatformStaff, router]);

  if (isLoading || isPlatformStaff) return null;
  return <>{children}</>;
}
