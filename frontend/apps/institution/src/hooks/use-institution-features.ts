"use client";

import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { institutionClient } from "@/lib/api-client";

/**
 * Same query (and queryKey, so the cache is shared) as the sidebar's own nav
 * feature-filtering in institution-layout.tsx — reuse this instead of
 * duplicating the fetch wherever a page needs to check one specific feature.
 */
export function useDisabledFeatures(): Set<string> {
  const { data: theme } = useQuery({
    queryKey: ["institution-nav-theme"],
    queryFn: async () => {
      const res = await institutionClient.get<{ data: { disabledFeatures: string[] } }>("/public/institution/theme");
      return res.data.data;
    },
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
  return useMemo(() => new Set(theme?.disabledFeatures ?? []), [theme]);
}

export function useFeatureEnabled(key: string): boolean {
  const disabled = useDisabledFeatures();
  return !disabled.has(key);
}
