"use client";

import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { institutionClient } from "@/lib/api-client";

/**
 * Same query (and queryKey, so the cache is shared) as the sidebar's own nav
 * feature-filtering in institution-layout.tsx — reuse this instead of
 * duplicating the fetch wherever a page needs to check one specific feature.
 */
function useFeatureTheme() {
  return useQuery({
    queryKey: ["institution-nav-theme"],
    queryFn: async () => {
      const res = await institutionClient.get<{ data: { disabledFeatures: string[] } }>("/public/institution/theme");
      return res.data.data;
    },
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
}

export function useDisabledFeatures(): Set<string> {
  const { data: theme } = useFeatureTheme();
  return useMemo(() => new Set(theme?.disabledFeatures ?? []), [theme]);
}

export function useFeatureEnabled(key: string): boolean {
  const disabled = useDisabledFeatures();
  return !disabled.has(key);
}

/**
 * For pages that fetch feature data. `enabled(key)` is false until the feature list has
 * loaded — useDisabledFeatures() alone is an empty set while loading, which reads as
 * "everything is on" and makes a page request features that are off (the API answers 403).
 * If the list itself fails to load, features are treated as on so each section falls back to
 * its own error handling.
 */
export function useFeatures() {
  const { isLoading } = useFeatureTheme();
  const disabled = useDisabledFeatures();
  const ready = !isLoading;
  return useMemo(
    () => ({ ready, disabled, enabled: (key: string) => ready && !disabled.has(key) }),
    [ready, disabled],
  );
}
