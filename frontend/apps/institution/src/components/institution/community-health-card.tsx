"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { cn } from "@alumni/ui";
import { getEngagementDashboard } from "@/lib/institution-api";
import { useAuth } from "@/hooks/use-auth";

const LABEL: Record<string, { text: string; tone: string }> = {
  Healthy: { text: "Healthy", tone: "text-success" },
  NeedsAttention: { text: "Needs attention", tone: "text-foreground" },
  AtRisk: { text: "At risk", tone: "text-destructive" },
  Inactive: { text: "Inactive", tone: "text-destructive" },
  InsufficientData: { text: "Too few members to score yet", tone: "text-muted-foreground" },
};

/**
 * The community's health at a glance, at the top of the administrator's dashboard, with the first thing worth doing about it.
 * Full administrators only (the workspace behind it is theirs). Quiet while loading or if it fails: the dashboard must never
 * be blocked by a card.
 */
export function CommunityHealthCard() {
  const { user } = useAuth();
  const enabled = user?.role === "SuperAdmin";
  const { data } = useQuery({ queryKey: ["engagement", 30], queryFn: () => getEngagementDashboard(30), staleTime: 60_000, enabled, retry: false });
  if (!enabled) return null;
  if (!data) return <div className="mb-3.5 skeleton h-[92px] rounded-none" aria-busy="true" />;

  const h = data.health;
  const klass = LABEL[h.classification] ?? LABEL.NeedsAttention;
  const next = data.recommendations[0];
  const more = data.recommendations.length - 1;
  return (
    <section aria-labelledby="community-health-heading" className="mb-3.5 border border-border bg-card p-4 sm:p-5">
      <div className="flex flex-wrap items-center justify-between gap-x-6 gap-y-3">
        <div>
          <h2 id="community-health-heading" className="text-[12.5px] font-semibold text-muted-foreground">Community health</h2>
          <p className="mt-1 flex flex-wrap items-baseline gap-x-3 gap-y-0.5">
            {h.score !== undefined && h.score !== null
              ? <span className="text-[34px] font-bold leading-none tabular-nums">{h.score}<span className="text-[14px] font-normal text-muted-foreground"> /100</span></span>
              : <span className="text-[18px] font-bold">No score yet</span>}
            <span className={cn("text-[14px] font-semibold", klass.tone)}>{klass.text}</span>
          </p>
        </div>
        <Link href="/engagement" className="inline-block py-2 text-[13.5px] font-semibold text-primary underline underline-offset-4">See details and what to do</Link>
      </div>
      {next && (
        <p className="mt-2 text-[13.5px] text-muted-foreground">
          Next: <Link href={next.actionUrl} className="font-semibold text-foreground underline underline-offset-4">{next.title}</Link>
          {more > 0 && <> and {more} more</>}
        </p>
      )}
    </section>
  );
}
