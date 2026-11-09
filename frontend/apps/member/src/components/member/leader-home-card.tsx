"use client";

import Link from "next/link";
import { useMemo } from "react";
import { ChevronRight, ShieldCheck } from "@alumni/ui";
import { useManageGroups } from "@/hooks/use-manage-groups";
import { groupHref } from "@/components/member/manage-parts";

/**
 * For a member who leads a group: the way in from home, with what is waiting for them. Silent for everyone else, so most people never see it.
 * Groups with someone waiting come first, and the link goes straight to the people to approve.
 */
export function LeaderHomeCard() {
  const { data } = useManageGroups();
  const groups = useMemo(() => [...(data ?? [])].sort((a, b) => b.pendingCount - a.pendingCount), [data]);
  if (groups.length === 0) return null;

  const waiting = groups.reduce((n, g) => n + g.pendingCount, 0);
  const shown = groups.slice(0, 3);
  return (
    <section aria-labelledby="leader-home" className="rounded-[18px] border border-border bg-background p-4 sm:p-5">
      <div className="flex items-center gap-3">
        <span className="grid size-10 shrink-0 place-items-center rounded-full bg-primary/10 text-primary"><ShieldCheck size={18} /></span>
        <div className="min-w-0 flex-1">
          <h2 id="leader-home" className="text-[16px] font-semibold leading-tight">{groups.length === 1 ? "You lead a group" : `You lead ${groups.length} groups`}</h2>
          <p className="mt-0.5 text-[13px] text-muted-foreground">{waiting > 0 ? `${waiting} ${waiting === 1 ? "person is" : "people are"} waiting for you` : "Nothing is waiting for you"}</p>
        </div>
      </div>
      <ul className="mt-3 divide-y divide-border border-t border-border">
        {shown.map((g) => (
          <li key={`${g.scopeType}-${g.scopeId}`}>
            <Link href={g.pendingCount > 0 && g.privileges.includes("ManageMembers") ? `${groupHref(g)}?tab=members` : groupHref(g)} className="flex min-h-12 items-center gap-3 py-2.5 transition hover:text-primary">
              <span className="min-w-0 flex-1">
                <span className="block truncate text-[14px] font-semibold leading-tight">{g.name}</span>
                <span className="block truncate text-[12.5px] text-muted-foreground">{g.roleNames.join(" · ")}</span>
              </span>
              {g.pendingCount > 0 && <span className="shrink-0 rounded-full bg-primary px-2.5 py-0.5 text-[12px] font-semibold text-primary-foreground">{g.pendingCount}</span>}
              <ChevronRight size={14} className="shrink-0 text-muted-foreground" />
            </Link>
          </li>
        ))}
      </ul>
      {groups.length > 3 && <Link href="/manage" className="mt-1 inline-flex min-h-10 items-center text-[13px] font-semibold text-primary underline-offset-2 hover:underline">See all {groups.length}</Link>}
    </section>
  );
}
