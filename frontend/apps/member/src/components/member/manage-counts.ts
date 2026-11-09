"use client";

import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import type { GroupCampaign, GroupEvent, GroupJob, GroupMember, GroupNews, GroupResource, ManageCounts, ManageGroup } from "@/lib/member-api";
import { manageQueries } from "@/components/member/manage-queries";

const LISTS = new Set(["manage-members", "manage-events", "manage-news", "manage-jobs", "manage-links", "manage-campaigns"]);
/** The members list shows only the newest this many, so it can only correct the total when it is shorter than that. */
const MEMBERS_LISTED = 200;

/**
 * The numbers on a group's tabs. They come from one light call, then follow what the leader does: the moment a section's list changes
 * (an event added, someone approved, a job closed) its number changes with it, instead of waiting for the next reload.
 */
export function useManageCounts(group: Pick<ManageGroup, "scopeType" | "scopeId"> | undefined): ManageCounts | undefined {
  const qc = useQueryClient();
  const base = useQuery({ ...manageQueries.counts(group ?? { scopeType: "Batch", scopeId: "" }), enabled: !!group });
  const [, changed] = useState(0);

  useEffect(() => {
    if (!group) return;
    return qc.getQueryCache().subscribe((event) => {
      // Only a change to the data itself matters. The cache also announces every time a section's own query re-reads its options on a
      // render, and reacting to those would re-render the page, which re-renders the section, which announces again, for ever.
      if (event.type !== "updated" || (event.action.type !== "success" && event.action.type !== "setState")) return;
      const [name, type, id] = event.query.queryKey as [string, string, string];
      if (LISTS.has(name) && type === group.scopeType && id === group.scopeId) changed((n) => n + 1);
    });
  }, [qc, group]);

  const server = base.data;
  if (!group || !server) return server;

  const list = <T,>(key: readonly unknown[]) => qc.getQueryData<T[]>(key);
  const now = Date.now();
  const events = list<GroupEvent>(manageQueries.events(group).queryKey);
  const news = list<GroupNews>(manageQueries.news(group).queryKey);
  const jobs = list<GroupJob>(manageQueries.jobs(group).queryKey);
  const links = list<GroupResource>(manageQueries.links(group).queryKey);
  const funds = list<GroupCampaign>(manageQueries.campaigns(group).queryKey);
  const people = list<GroupMember>(manageQueries.members(group).queryKey);

  return {
    members: people && people.length < MEMBERS_LISTED ? people.filter((m) => m.status === "Active").length : server.members,
    waiting: people ? people.filter((m) => m.status === "Pending").length : server.waiting,
    events: server.events === null ? null : events ? events.filter((e) => e.status !== "Cancelled" && new Date(e.endDate ?? e.startDate).getTime() >= now).length : server.events,
    news: server.news === null ? null : news ? news.length : server.news,
    jobs: server.jobs === null ? null : jobs ? jobs.filter((j) => j.status !== "Closed").length : server.jobs,
    links: server.links === null ? null : links ? links.length : server.links,
    fundraisers: server.fundraisers === null ? null : funds ? funds.filter((c) => c.status === "Active").length : server.fundraisers,
  };
}
