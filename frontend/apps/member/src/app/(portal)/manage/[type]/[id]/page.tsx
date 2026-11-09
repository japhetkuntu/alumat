"use client";

import Link from "next/link";
import dynamic from "next/dynamic";
import { use, useCallback, useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { ChevronLeft, EmptyState } from "@alumni/ui";
import { useManageGroups } from "@/hooks/use-manage-groups";
import { scopeTypeFromSlug } from "@/components/member/manage-parts";
import { SectionNav, SectionSkeleton, type SectionTab } from "@/components/member/manage-ui";
import { LeaderWelcome } from "@/components/member/leader-welcome";
import { useManageCounts } from "@/components/member/manage-counts";
import { manageQueries } from "@/components/member/manage-queries";
import type { ManageGroup } from "@/lib/member-api";

// Each section's code is fetched only when someone opens it (or points at its tab), so the page itself stays light.
const people = () => import("@/components/member/manage-people");
const events = () => import("@/components/member/manage-events");
const content = () => import("@/components/member/manage-content");
const fundraising = () => import("@/components/member/manage-fundraising");
const Report = dynamic(() => people().then((m) => m.Report), { loading: () => <SectionSkeleton /> });
const Members = dynamic(() => people().then((m) => m.Members), { loading: () => <SectionSkeleton /> });
const Message = dynamic(() => people().then((m) => m.Message), { loading: () => <SectionSkeleton /> });
const Events = dynamic(() => events().then((m) => m.Events), { loading: () => <SectionSkeleton /> });
const NewsSection = dynamic(() => content().then((m) => m.NewsSection), { loading: () => <SectionSkeleton /> });
const JobsSection = dynamic(() => content().then((m) => m.JobsSection), { loading: () => <SectionSkeleton /> });
const LinksSection = dynamic(() => content().then((m) => m.LinksSection), { loading: () => <SectionSkeleton /> });
const FundraisersSection = dynamic(() => fundraising().then((m) => m.FundraisersSection), { loading: () => <SectionSkeleton /> });

type TabId = "overview" | "members" | "events" | "news" | "jobs" | "links" | "funds" | "message";
const TAB_IDS: TabId[] = ["overview", "members", "events", "news", "jobs", "links", "funds", "message"];

/** The tab named in the address, so a refresh, a shared link and the back button all land where the leader was. */
const tabFromAddress = (): TabId | null => {
  if (typeof window === "undefined") return null;
  const t = new URLSearchParams(window.location.search).get("tab");
  return TAB_IDS.includes(t as TabId) ? (t as TabId) : null;
};

export default function ManageGroupPage({ params }: { params: Promise<{ type: string; id: string }> }) {
  const { type, id: rawId } = use(params);
  const id = decodeURIComponent(rawId);
  const scopeType = scopeTypeFromSlug(type);
  const qc = useQueryClient();
  const groups = useManageGroups();
  const group = groups.data?.find((g) => g.scopeType === scopeType && g.scopeId === id);
  const [tab, setTab] = useState<TabId | null>(tabFromAddress);

  const counts = useManageCounts(group);

  // Only the sections this member's roles allow, in a steady order, each with how much it holds.
  const tabs = useMemo(() => {
    const has = (p: string) => !!group?.privileges.includes(p);
    const waiting = counts?.waiting ?? 0;
    const many = (n: number | null | undefined, one: string, other: string) => (n == null ? undefined : `${n} ${n === 1 ? one : other}`);
    return ([
      has("ViewReport") && { id: "overview" as const, label: "Overview" },
      // Someone waiting to be let in is what a leader most needs to see; otherwise the tab just says how many are in.
      has("ManageMembers") && {
        id: "members" as const, label: "Members", count: waiting > 0 ? waiting : counts?.members, attention: waiting > 0,
        hint: waiting > 0 ? `${waiting} waiting for you` : many(counts?.members, "member", "members"),
      },
      has("ManageEvents") && { id: "events" as const, label: "Events", count: counts?.events, hint: many(counts?.events, "upcoming event", "upcoming events") },
      has("PostNews") && { id: "news" as const, label: "News", count: counts?.news, hint: many(counts?.news, "post", "posts") },
      has("PostJobs") && { id: "jobs" as const, label: "Jobs", count: counts?.jobs, hint: many(counts?.jobs, "open job", "open jobs") },
      has("ShareResources") && { id: "links" as const, label: "Links", count: counts?.links, hint: many(counts?.links, "link", "links") },
      (has("ManageFundraisers") || has("ViewGiving")) && { id: "funds" as const, label: "Fundraisers", count: counts?.fundraisers, hint: many(counts?.fundraisers, "active fundraiser", "active fundraisers") },
      has("MessageGroup") && { id: "message" as const, label: "Message" },
    ].filter(Boolean)) as SectionTab<TabId>[];
  }, [group, counts]);
  const active = tab && tabs.some((t) => t.id === tab) ? tab : tabs[0]?.id;

  const choose = useCallback((next: TabId) => {
    setTab(next);
    const url = new URL(window.location.href);
    url.searchParams.set("tab", next);
    window.history.replaceState(null, "", url);
  }, []);

  // Pointing at a tab starts fetching its code and its data, so by the time it is tapped it is usually there already.
  const intent = useCallback((next: TabId, g: ManageGroup) => {
    switch (next) {
      case "overview": void people(); void qc.prefetchQuery(manageQueries.summary(g)); break;
      case "members": void people(); void qc.prefetchQuery(manageQueries.members(g)); break;
      case "message": void people(); void qc.prefetchQuery(manageQueries.channels(g)); void qc.prefetchQuery(manageQueries.messages(g)); break;
      case "events": void events(); void qc.prefetchQuery(manageQueries.events(g)); break;
      case "news": void content(); void qc.prefetchQuery(manageQueries.news(g)); break;
      case "jobs": void content(); void qc.prefetchQuery(manageQueries.jobs(g)); break;
      case "links": void content(); void qc.prefetchQuery(manageQueries.links(g)); break;
      case "funds": void fundraising(); void qc.prefetchQuery(manageQueries.campaigns(g)); break;
    }
  }, [qc]);

  const several = (groups.data?.length ?? 0) > 1;
  return (
    <div className="mx-auto max-w-5xl space-y-4 px-4 pb-8 pt-4 sm:px-6 sm:pt-6 lg:px-8 lg:pt-8">
      {groups.isLoading ? (
        <div className="h-20 animate-pulse rounded-[18px] bg-muted/60" aria-busy="true" />
      ) : !group ? (
        <EmptyState title="You can't manage this group" description="Either it doesn't exist, or your role doesn't include access to it." />
      ) : (
        <>
          <header className="flex items-center gap-1">
            {/* A way back only when there is somewhere to go: with a single group the list would just send you straight back here. */}
            {several && (
              <Link href="/manage" aria-label="All my groups" className="-ml-2.5 grid size-11 shrink-0 place-items-center rounded-full text-muted-foreground transition hover:bg-muted hover:text-foreground"><ChevronLeft size={18} /></Link>
            )}
            <div className="min-w-0">
              <h1 className="truncate text-[21px] font-bold leading-tight sm:text-[26px]" style={{ marginBottom: 0 }}>{group.name}</h1>
              <p className="truncate text-[13px] text-muted-foreground">{group.roleNames.join(" · ")}</p>
            </div>
          </header>
          <LeaderWelcome group={group} />
          <div className={tabs.length > 1 ? "lg:grid lg:grid-cols-[200px_minmax(0,1fr)] lg:items-start lg:gap-8" : ""}>
            {tabs.length > 1 && <div className="mb-3 lg:mb-0"><SectionNav tabs={tabs} value={active!} onChange={choose} onIntent={(t) => intent(t, group)} /></div>}
            <div className="min-w-0 space-y-4">
              {active === "overview" && <Report group={group} />}
              {active === "members" && <Members group={group} />}
              {active === "events" && <Events group={group} />}
              {active === "news" && <NewsSection group={group} />}
              {active === "jobs" && <JobsSection group={group} />}
              {active === "links" && <LinksSection group={group} />}
              {active === "funds" && <FundraisersSection group={group} />}
              {active === "message" && <Message group={group} />}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
