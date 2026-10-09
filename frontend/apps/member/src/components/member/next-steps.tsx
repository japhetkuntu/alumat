"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { getNextSteps } from "@/lib/member-api";

/**
 * Up to three things worth doing next, chosen by fixed rules on the server from what actually exists for this member.
 * Renders nothing while loading, on error, or when there is nothing to suggest: an empty "for you" box is worse than none.
 */
export function NextSteps() {
  const { data } = useQuery({ queryKey: ["m-next-steps"], queryFn: getNextSteps, staleTime: 2 * 60_000, retry: false });
  if (!data || data.length === 0) return null;
  return (
    <section aria-labelledby="next-steps-heading" className="border border-border bg-card p-5 sm:p-6">
      <h2 id="next-steps-heading" className="text-[13px] font-semibold text-muted-foreground">Next for you</h2>
      <ul className="mt-3 divide-y divide-border">
        {data.map((s) => (
          <li key={s.key} className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 py-3 first:pt-0 last:pb-0">
            <div className="min-w-0 flex-1">
              <p className="text-[15px] font-semibold leading-snug">{s.title}</p>
              <p className="mt-0.5 text-[13.5px] text-muted-foreground">{s.detail}</p>
            </div>
            <Link href={s.actionUrl} className="shrink-0 text-[13.5px] font-semibold text-primary underline underline-offset-4">{s.actionLabel}</Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
