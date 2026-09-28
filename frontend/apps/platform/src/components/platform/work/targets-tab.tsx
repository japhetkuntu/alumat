"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Button, EmptyState, LoadError, Skeleton, formatDate } from "@alumni/ui";
import { getWorkTargets, type WorkTarget } from "@/lib/platform-api";
import { useAuth } from "@/hooks/use-auth";
import { HealthBadge, TargetProgressBar, daysLeft } from "./work-common";
import { TargetDialog } from "./target-dialog";

function TargetCard({ t }: { t: WorkTarget }) {
  const active = t.status === "Active";
  const left = daysLeft(t.dueDate);
  return (
    <Link href={`/activation/targets/${t.id}`} className="block border border-border bg-card p-5 hover:border-foreground/30 transition-colors">
      <div className="flex items-start justify-between gap-3 mb-3">
        <div className="min-w-0">
          <p className="text-[15px] font-semibold leading-snug">{t.title}</p>
          <p className="text-[12px] text-muted-foreground mt-0.5">
            {t.metricLabel} · owner {t.ownerName}
          </p>
        </div>
        <HealthBadge health={t.progress.health} />
      </div>
      <TargetProgressBar target={t} />
      <p className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-[12px] text-muted-foreground">
        <span>
          {active ? (left >= 0 ? `Ends ${formatDate(t.dueDate)} · ${left} day${left === 1 ? "" : "s"} left` : `Ended ${formatDate(t.dueDate)}`) : `Ended ${formatDate(t.closedAt ?? t.dueDate)}`}
        </span>
        <span>{t.openTasks} open task{t.openTasks === 1 ? "" : "s"}{t.doneTasks > 0 ? ` · ${t.doneTasks} done` : ""}</span>
        {t.overdueTasks > 0 && <span className="text-destructive font-semibold">{t.overdueTasks} overdue</span>}
      </p>
    </Link>
  );
}

/** Every target: the ones still running first, then the ones that have ended. Super Admins create new ones. */
export function TargetsTab() {
  const router = useRouter();
  const { isSuperAdmin } = useAuth();
  const [creating, setCreating] = useState(false);
  const { data, isLoading, isError, refetch } = useQuery({ queryKey: ["work-targets"], queryFn: () => getWorkTargets() });

  const targets = data ?? [];
  const running = targets.filter((t) => t.status === "Active");
  const ended = targets.filter((t) => t.status !== "Active");

  return (
    <div className="p-4 sm:p-7 max-w-[1100px]">
      <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-3 mb-5">
        <div>
          <h2 className="text-[20px] font-bold">Targets</h2>
          <p className="text-muted-foreground text-[13px] mt-1">
            What the team is working towards. Each has one owner and an end date, and tasks sit under it.
          </p>
        </div>
        {isSuperAdmin && <Button onClick={() => setCreating(true)}>New target</Button>}
      </div>

      {isLoading ? (
        <div className="grid gap-4 md:grid-cols-2">{[0, 1].map((i) => <Skeleton key={i} className="h-40" />)}</div>
      ) : isError ? (
        <LoadError title="Couldn't load targets" onRetry={() => void refetch()} />
      ) : targets.length === 0 ? (
        <EmptyState
          title="No targets yet"
          description={isSuperAdmin ? "Set a goal, such as 20 live institutions by year end, give it an owner, then break it into tasks." : "A Super Admin will set the team's targets here."}
          action={isSuperAdmin ? <Button onClick={() => setCreating(true)}>Create the first target</Button> : undefined}
        />
      ) : (
        <div className="space-y-8">
          {running.length > 0 && (
            <section>
              <p className="text-[12px] font-semibold tracking-wide uppercase text-muted-foreground mb-3">Running</p>
              <div className="grid gap-4 md:grid-cols-2">{running.map((t) => <TargetCard key={t.id} t={t} />)}</div>
            </section>
          )}
          {ended.length > 0 && (
            <section>
              <p className="text-[12px] font-semibold tracking-wide uppercase text-muted-foreground mb-3">Ended</p>
              <div className="grid gap-4 md:grid-cols-2">{ended.map((t) => <TargetCard key={t.id} t={t} />)}</div>
            </section>
          )}
        </div>
      )}

      {creating && <TargetDialog onClose={() => setCreating(false)} onSaved={(t) => { setCreating(false); router.push(`/activation/targets/${t.id}`); }} />}
    </div>
  );
}
