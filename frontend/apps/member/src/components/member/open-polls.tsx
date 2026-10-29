"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Vote } from "@alumni/ui";
import { useFeatures } from "@/components/member/member-layout";
import { getMemberPolls } from "@/lib/member-api";
import { timeLeft } from "@/lib/poll-format";

/**
 * Votes the member can still cast. Only polls that are open and not yet voted in are listed, so the card is a call to act and goes away
 * as soon as there is nothing left to do. The closing time is the first thing said: it is the reason to do it now.
 */
export function OpenPolls() {
  const features = useFeatures();
  const on = features.enabled("Polls");
  const { data } = useQuery({ queryKey: ["m-polls"], queryFn: getMemberPolls, enabled: on, staleTime: 60_000, retry: false });
  const todo = (data ?? []).filter((p) => p.state === "Open" && !p.hasVoted).sort((a, b) => new Date(a.closesAt).getTime() - new Date(b.closesAt).getTime());
  if (!on || todo.length === 0) return null;

  return (
    <section aria-labelledby="open-polls-h" className="border border-primary/40 bg-card p-5 sm:p-6">
      <div className="flex items-center gap-2.5">
        <Vote size={16} className="text-primary" />
        <h2 id="open-polls-h" className="text-[13px] font-semibold text-muted-foreground">{todo.length === 1 ? "Your vote is needed" : `Your vote is needed in ${todo.length} polls`}</h2>
      </div>
      <ul className="mt-3 divide-y divide-border">
        {todo.slice(0, 3).map((p) => (
          <li key={p.id} className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 py-3 first:pt-0 last:pb-0">
            <div className="min-w-0 flex-1">
              <p className="text-[15.5px] font-semibold leading-snug">{p.title}</p>
              <p className="mt-0.5 text-[13.5px] text-muted-foreground">{timeLeft(p.closesAt)}. {p.positionCount} {p.positionCount === 1 ? "position" : "positions"}, and your vote is secret.</p>
            </div>
            <Link href={`/polls/${p.id}`} className="inline-flex h-10 shrink-0 items-center bg-primary px-5 text-[13.5px] font-semibold text-primary-foreground hover:opacity-90">Vote now</Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
