"use client";

import Link from "next/link";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Plus, Vote, SegmentedControl, EmptyState, LoadError, CardSkeleton, Progress } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { getPolls, type PollListItem } from "@/lib/institution-api";
import { PollStateBadge, audienceText, fmtWhen } from "@/components/institution/poll-ui";
import { useCohortPhrase } from "@/components/institution/institution-layout";

function PollRow({ p }: { p: PollListItem }) {
  const { word } = useCohortPhrase();
  const pct = p.eligible > 0 ? Math.round((p.voted / p.eligible) * 100) : 0;
  const when = p.state === "Draft" ? `Voting ${fmtWhen(p.opensAt)} to ${fmtWhen(p.closesAt)}`
    : p.state === "Scheduled" ? `Opens ${fmtWhen(p.opensAt)}, closes ${fmtWhen(p.closesAt)}`
    : p.state === "Open" ? `Closes ${fmtWhen(p.closesAt)}` : `Closed ${fmtWhen(p.closesAt)}`;
  return (
    <Link href={`/polls/${p.id}`} className="card group block p-4 transition-colors hover:border-primary/40 sm:p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 flex-1 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <PollStateBadge state={p.state} />
            <span className="text-[12px] text-muted-foreground">{p.positionCount} {p.positionCount === 1 ? "position" : "positions"}</span>
          </div>
          <p className="text-[16px] font-semibold leading-snug group-hover:text-primary">{p.title}</p>
          <p className="text-[13px] text-muted-foreground">{when}. {audienceText(p, word)}.</p>
        </div>
        {p.status === "Published" && p.eligible > 0 && (
          <div className="w-full shrink-0 sm:w-56">
            <div className="mb-1.5 flex items-baseline justify-between text-[12.5px]">
              <span className="font-semibold tabular-nums">{p.voted} of {p.eligible} voted</span>
              <span className="tabular-nums text-muted-foreground">{pct}%</span>
            </div>
            <Progress value={pct} tone="accent" aria-label={`${pct}% turnout`} className="h-1.5" />
          </div>
        )}
      </div>
    </Link>
  );
}

export default function PollsPage() {
  const [tab, setTab] = useState<"active" | "archived">("active");
  const { data = [], isLoading, isError, refetch } = useQuery({ queryKey: ["polls", tab], queryFn: () => getPolls(tab === "archived") });

  return (
    <div className="mx-auto max-w-[1100px] space-y-5 p-4 sm:p-[26px]">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="m-0 text-[20px] font-bold sm:text-[25px]">Polls and elections</h1>
          <p className="mt-1.5 text-[13px] text-muted-foreground">Put a ballot to your members. They vote once, in secret, and you choose when the results show.</p>
        </div>
        <Link href="/polls/new"><Button><Plus size={14} /> New poll</Button></Link>
      </div>

      <SegmentedControl label="Polls" options={[{ value: "active", label: "Active" }, { value: "archived", label: "Archived" }]} value={tab} onChange={setTab} className="w-full sm:w-auto" />

      {isLoading ? (
        <div className="space-y-3">{Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}</div>
      ) : isError ? (
        <LoadError onRetry={() => refetch()} />
      ) : data.length === 0 ? (
        tab === "active"
          ? <EmptyState icon={<Vote size={40} />} title="Run your first election" description="Set up the positions, add the aspirants with their photos, choose who may vote and when, and publish. Members vote from their portal, and nobody can see who voted for whom." action={<Link href="/polls/new"><Button><Plus size={14} /> New poll</Button></Link>} />
          : <EmptyState icon={<Vote size={40} />} title="Nothing archived" description="A poll that has closed can be archived from its page to keep this list tidy. Its results stay available." />
      ) : (
        <div className="space-y-3">{data.map((p) => <PollRow key={p.id} p={p} />)}</div>
      )}
    </div>
  );
}
