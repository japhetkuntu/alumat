"use client";

import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/hooks/use-auth";
import { getManageGroups } from "@/lib/member-api";

/** The year groups and communities this member may act on as a leader. Empty for almost everyone, which is what hides the Manage menu. */
export function useManageGroups() {
  const { isMember } = useAuth();
  return useQuery({ queryKey: ["manage-groups"], queryFn: getManageGroups, enabled: isMember, staleTime: 2 * 60_000, retry: false });
}
