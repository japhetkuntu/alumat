"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Check, Vote, Clock } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { LoadError } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { getMemberPolls, type MemberPollListItem } from "@/lib/member-api";
import { fmtWhen, timeLeft } from "@/lib/poll-format";

function PollCard({ p }: { p: MemberPollListItem }) {
  const open = p.state === "Open";
  const needsVote = open && !p.hasVoted;
  return (
    <Link href={`/polls/${p.id}`} className={`card group block p-5 transition-all hover:-translate-y-0.5 hover:shadow-[var(--card-shadow-hover)] ${needsVote ? "border-primary/40" : ""}`}>
      <div className="flex flex-wrap items-center gap-2">
        {needsVote && <Badge variant="success">Your vote is needed</Badge>}
        {open && p.hasVoted && <Badge variant="info" className="gap-1"><Check size={10} /> You voted</Badge>}
        {p.state === "Scheduled" && <Badge variant="info">Opens soon</Badge>}
        {p.state === "Closed" && <Badge variant="neutral">{p.resultsVisible ? "Results are in" : "Closed"}</Badge>}
        <span className="text-[12.5px] text-muted-foreground">{p.audience}</span>
      </div>
      <p className="mt-2 font-[family-name:var(--font-display)] text-[19px] font-semibold leading-snug group-hover:text-primary">{p.title}</p>
      {p.description && <p className="mt-1 line-clamp-2 text-[13.5px] text-muted-foreground">{p.description}</p>}
      <p className="mt-3 flex items-center gap-1.5 text-[12.5px] text-muted-foreground">
        <Clock size={12} />
        {open ? timeLeft(p.closesAt) : p.state === "Scheduled" ? `Opens ${fmtWhen(p.opensAt)}` : `Closed ${fmtWhen(p.closesAt)}`}
        <span aria-hidden>·</span> {p.positionCount} {p.positionCount === 1 ? "position" : "positions"}
      </p>
    </Link>
  );
}

export default function MemberPollsPage() {
  const { data = [], isLoading, isError, refetch } = useQuery({ queryKey: ["m-polls"], queryFn: getMemberPolls, refetchInterval: 60_000 });
  const open = data.filter((p) => p.state === "Open");
  const soon = data.filter((p) => p.state === "Scheduled");
  const closed = data.filter((p) => p.state === "Closed");

  return (
    <div className="mx-auto max-w-[900px] space-y-8 p-4 sm:p-6 lg:p-8">
      <PageHeader eyebrow="Have your say" title="Polls and elections" description="Your vote is secret. Nobody, including the administrators, can see who voted for whom." />

      {isLoading ? (
        <div className="space-y-3">{Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}</div>
      ) : isError ? (
        <LoadError onRetry={() => refetch()} />
      ) : data.length === 0 ? (
        <EmptyState icon={<Vote size={40} />} title="Nothing to vote on right now" description="When your institution opens an election or a poll for you, it shows up here and you will be told." />
      ) : (
        <>
          {open.length > 0 && <Section title="Open for voting">{open.map((p) => <PollCard key={p.id} p={p} />)}</Section>}
          {soon.length > 0 && <Section title="Coming up">{soon.map((p) => <PollCard key={p.id} p={p} />)}</Section>}
          {closed.length > 0 && <Section title="Closed">{closed.map((p) => <PollCard key={p.id} p={p} />)}</Section>}
        </>
      )}
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return <section className="space-y-3"><h2 className="text-[13px] font-semibold uppercase tracking-wide text-muted-foreground">{title}</h2><div className="space-y-3">{children}</div></section>;
}
