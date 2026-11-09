"use client";

import Link from "next/link";
import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { ChevronRight, EmptyState, LoadError, PageHeader } from "@alumni/ui";
import { useManageGroups } from "@/hooks/use-manage-groups";
import { groupHref } from "@/components/member/manage-parts";

export default function ManagePage() {
  const router = useRouter();
  const { data, isLoading, isError, refetch } = useManageGroups();
  const groups = data ?? [];

  // One group is the common case, so it opens straight away instead of asking the leader to pick the only choice.
  useEffect(() => { if (groups.length === 1) router.replace(groupHref(groups[0])); }, [groups, router]);

  return (
    <div className="mx-auto max-w-3xl px-4 pb-8 pt-4 sm:px-6 sm:pt-6 lg:px-8 lg:pt-8">
      <PageHeader eyebrow="Leadership" title="Manage" description="The groups you look after." />
      <div className="mt-5 space-y-3">
        {isLoading || groups.length === 1 ? (
          <div className="space-y-3" aria-busy="true">{[0, 1].map((i) => <div key={i} className="h-[88px] animate-pulse rounded-[18px] bg-muted/60" />)}</div>
        ) : isError ? (
          <LoadError onRetry={() => refetch()} />
        ) : groups.length === 0 ? (
          <EmptyState title="You don't manage any groups" description="When an administrator gives you a role that comes with access, the group appears here." />
        ) : groups.map((g) => (
          <Link key={`${g.scopeType}-${g.scopeId}`} href={groupHref(g)}
            className="flex min-h-[88px] items-center gap-3 rounded-[18px] border border-border bg-background p-4 transition active:scale-[0.99] hover:border-primary/40 hover:bg-muted/30 sm:p-5">
            <div className="min-w-0 flex-1">
              <p className="truncate text-[16px] font-semibold leading-tight">{g.name}</p>
              <p className="mt-0.5 truncate text-[13px] text-muted-foreground">{g.roleNames.join(" · ")}</p>
              {g.pendingCount > 0 && (
                <p className="mt-2 inline-flex items-center gap-1.5 rounded-full bg-primary/10 px-2.5 py-1 text-[12.5px] font-semibold text-primary">
                  <span className="size-1.5 rounded-full bg-primary" aria-hidden />{g.pendingCount} waiting for you
                </p>
              )}
            </div>
            <ChevronRight size={16} className="shrink-0 text-muted-foreground" />
          </Link>
        ))}
      </div>
    </div>
  );
}
