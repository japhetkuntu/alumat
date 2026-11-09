"use client";

import { useQuery } from "@tanstack/react-query";
import type { GroupCampaign, GroupEvent, GroupJob, GroupMember, GroupNews, GroupResource, ManageCounts, ManageGroup } from "@/lib/member-api";
import { manageQueries } from "@/components/member/manage-queries";

/** The members list shows only the newest this many, so it can only correct the total when it is shorter than that. */
const MEMBERS_LISTED = 200;

/**
 * Watches a section's list without ever fetching it: the list is read when a section opens it, and this only follows what is already
 * in the cache. It re-renders only when the number it derives changes, so adding an event or approving someone updates the tab at once,
 * and nothing else (a section re-reading its options, a refetch with the same result) can set it off.
 */
function useWatched<T, R>(options: { queryKey: readonly unknown[] }, select: (rows: T[]) => R): R | undefined {
  return useQuery({
    queryKey: options.queryKey,
    queryFn: () => Promise.resolve<T[]>([]),   // never called: the query is disabled
    enabled: false,
    select,
  }).data;
}

/**
 * The numbers on a group's tabs. They come from one light call, then follow what the leader does with each section's list.
 * Before a section has been opened there is nothing to follow, and the server's number stands.
 */
export function useManageCounts(group: Pick<ManageGroup, "scopeType" | "scopeId"> | undefined): ManageCounts | undefined {
  const g = group ?? { scopeType: "Batch", scopeId: "" };
  const base = useQuery({ ...manageQueries.counts(g), enabled: !!group }).data;

  const people = useWatched<GroupMember, { members: number; waiting: number }>(manageQueries.members(g), (rows) => ({
    members: rows.length < MEMBERS_LISTED ? rows.filter((m) => m.status === "Active").length : -1,
    waiting: rows.filter((m) => m.status === "Pending").length,
  }));
  const events = useWatched<GroupEvent, number>(manageQueries.events(g), (rows) => {
    const now = Date.now();
    return rows.filter((e) => e.status !== "Cancelled" && new Date(e.endDate ?? e.startDate).getTime() >= now).length;
  });
  const news = useWatched<GroupNews, number>(manageQueries.news(g), (rows) => rows.length);
  const jobs = useWatched<GroupJob, number>(manageQueries.jobs(g), (rows) => rows.filter((j) => j.status !== "Closed").length);
  const links = useWatched<GroupResource, number>(manageQueries.links(g), (rows) => rows.length);
  const funds = useWatched<GroupCampaign, number>(manageQueries.campaigns(g), (rows) => rows.filter((c) => c.status === "Active").length);

  if (!group || !base) return base;
  return {
    members: people && people.members >= 0 ? people.members : base.members,
    waiting: people ? people.waiting : base.waiting,
    // A section the leader may not open has no number, however its list looks.
    events: base.events === null ? null : events ?? base.events,
    news: base.news === null ? null : news ?? base.news,
    jobs: base.jobs === null ? null : jobs ?? base.jobs,
    links: base.links === null ? null : links ?? base.links,
    fundraisers: base.fundraisers === null ? null : funds ?? base.fundraisers,
  };
}
