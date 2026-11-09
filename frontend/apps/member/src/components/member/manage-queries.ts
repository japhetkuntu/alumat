import {
  getGroupCampaigns, getGroupEvents, getGroupJobs, getGroupMembers, getGroupNews, getGroupResources, getGroupSummary, getManageCounts, getMessageChannels, getMessageHistory,
  type ManageGroup,
} from "@/lib/member-api";

type G = Pick<ManageGroup, "scopeType" | "scopeId">;

/**
 * Every query the Manage page makes, defined once. The sections read and update these by key, and the page can start any of them early
 * (on hover or focus of a tab) without loading the section's code, because this file is small and the sections are not.
 */
export const manageQueries = {
  summary: (g: G) => ({ queryKey: ["manage-summary", g.scopeType, g.scopeId] as const, queryFn: () => getGroupSummary(g), staleTime: 60_000 }),
  members: (g: G) => ({ queryKey: ["manage-members", g.scopeType, g.scopeId] as const, queryFn: () => getGroupMembers(g), staleTime: 30_000 }),
  events: (g: G) => ({ queryKey: ["manage-events", g.scopeType, g.scopeId] as const, queryFn: () => getGroupEvents(g), staleTime: 30_000 }),
  news: (g: G) => ({ queryKey: ["manage-news", g.scopeType, g.scopeId] as const, queryFn: () => getGroupNews(g), staleTime: 30_000 }),
  jobs: (g: G) => ({ queryKey: ["manage-jobs", g.scopeType, g.scopeId] as const, queryFn: () => getGroupJobs(g), staleTime: 30_000 }),
  links: (g: G) => ({ queryKey: ["manage-links", g.scopeType, g.scopeId] as const, queryFn: () => getGroupResources(g), staleTime: 30_000 }),
  campaigns: (g: G) => ({ queryKey: ["manage-campaigns", g.scopeType, g.scopeId] as const, queryFn: () => getGroupCampaigns(g), staleTime: 30_000 }),
  counts: (g: G) => ({ queryKey: ["manage-counts", g.scopeType, g.scopeId] as const, queryFn: () => getManageCounts(g), staleTime: 60_000 }),
  messages: (g: G) => ({ queryKey: ["manage-messages", g.scopeType, g.scopeId] as const, queryFn: () => getMessageHistory(g), staleTime: 30_000 }),
  channels: (g: G) => ({ queryKey: ["manage-channels", g.scopeType, g.scopeId] as const, queryFn: () => getMessageChannels(g), staleTime: 5 * 60_000 }),
};

/** Puts a saved item into a cached list: replaces it if it is there, otherwise adds it at the top. So a save shows at once, with no reload. */
export function upsertById<T extends { id: string }>(list: T[] | undefined, item: T): T[] {
  if (!list) return [item];
  return list.some((x) => x.id === item.id) ? list.map((x) => (x.id === item.id ? item : x)) : [item, ...list];
}

export const removeById = <T extends { id: string }>(list: T[] | undefined, id: string): T[] => (list ?? []).filter((x) => x.id !== id);
