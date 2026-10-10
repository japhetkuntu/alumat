"use client";

import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { YearGroupOptionsProvider } from "@alumni/ui";
import { getBatches } from "@/lib/institution-api";
import { useAuth } from "@/hooks/use-auth";

/**
 * Gives every year-group picker in the portal the institution's own batches to choose from, so admins tap a class rather than
 * type a year. Quiet if the list cannot be loaded: the picker then offers recent years and manual entry as before.
 */
export function BatchOptionsProvider({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  const enabled = user?.role === "SuperAdmin" || user?.role === "ScopedAdmin";
  const { data } = useQuery({ queryKey: ["batches"], queryFn: getBatches, enabled, staleTime: 5 * 60_000, retry: false });
  const options = useMemo(() => (data ?? []).filter((b) => b.isActive).map((b) => ({ year: b.year, label: b.name })), [data]);
  return <YearGroupOptionsProvider options={options}>{children}</YearGroupOptionsProvider>;
}
