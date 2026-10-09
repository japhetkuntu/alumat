"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { getPendingWork } from "@/lib/institution-api";
import { useAuth } from "@/hooks/use-auth";

/**
 * What members have submitted and are waiting on an administrator for, in one list with a link to where each is reviewed.
 * Full administrators only. Absent when nothing is waiting or while loading: the dashboard must never be blocked by a card.
 */
export function WaitingForYou() {
  const { user } = useAuth();
  const enabled = user?.role === "SuperAdmin";
  const { data } = useQuery({ queryKey: ["pending-work"], queryFn: getPendingWork, staleTime: 30_000, enabled, retry: false });
  if (!enabled || !data || data.length === 0) return null;
  return (
    <section aria-labelledby="waiting-heading" className="mb-3.5 border border-border bg-card p-4 sm:p-5">
      <h2 id="waiting-heading" className="text-[12.5px] font-semibold text-muted-foreground">Members are waiting on you</h2>
      <ul className="mt-2 divide-y divide-border">
        {data.map((i) => (
          <li key={i.key}>
            <Link href={i.actionUrl} className="flex min-h-11 items-center justify-between gap-4 py-2 text-[14.5px] font-semibold hover:underline">
              <span>{i.label}</span>
              <span className="shrink-0 text-[13px] text-primary underline underline-offset-4">Review</span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
