"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { formatDistanceToNowStrict } from "date-fns";
import { useCallback, useEffect, useState } from "react";
import { UserAvatar, X } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { useFeatures } from "@/components/member/member-layout";
import { getForumThreads } from "@/lib/member-api";

/** A question that has gone two weeks without an answer is no longer urgent, so it stops being put in front of everyone. */
const MAX_AGE_DAYS = 14;
const SHOWN = 3;
const SEEN_KEY = "waiting-for-answer:seen";
const SEEN_KEPT = 100;

/** The questions this member has already opened or waved away on this device. Quietly empty if storage is unavailable. */
function useSeen() {
  const [seen, setSeen] = useState<string[]>([]);
  useEffect(() => {
    try { const v = JSON.parse(localStorage.getItem(SEEN_KEY) ?? "[]"); if (Array.isArray(v)) setSeen(v.filter((x) => typeof x === "string")); } catch { /* nothing remembered */ }
  }, []);
  const markSeen = useCallback((id: string) => {
    setSeen((cur) => {
      if (cur.includes(id)) return cur;
      const next = [...cur, id].slice(-SEEN_KEPT);
      try { localStorage.setItem(SEEN_KEY, JSON.stringify(next)); } catch { /* shown again next visit, which is harmless */ }
      return next;
    });
  }, []);
  return { seen, markSeen };
}

/**
 * Forum questions nobody has answered yet, put in front of the people who could. Each one goes away for a member once they open it or
 * wave it off, and after two weeks it goes away for everyone, so the box clears instead of sitting on home for ever. A conversation usually dies for lack of one
 * reply, and this is where a member is most likely to give it. Never lists a member's own questions or ones that went quiet long
 * ago, and says nothing at all when there is nothing waiting: an empty "someone needs you" box would be noise.
 */
export function WaitingForAnswer() {
  const { user } = useAuth();
  const features = useFeatures();
  const on = features.enabled("Forum");
  const { seen, markSeen } = useSeen();
  const { data } = useQuery({
    queryKey: ["m-unanswered-threads"],
    queryFn: () => getForumThreads(1, 12, undefined, undefined, "unanswered"),
    enabled: on,
    staleTime: 2 * 60_000,
    retry: false,
  });
  if (!on || !data) return null;

  const cutoff = Date.now() - MAX_AGE_DAYS * 86_400_000;
  const waiting = data.results
    .filter((t) => t.authorId !== user?.id && !t.isClosed && !seen.includes(t.id) && new Date(t.createdAt).getTime() >= cutoff)
    .slice(0, SHOWN);
  if (waiting.length === 0) return null;

  return (
    <section aria-labelledby="waiting-heading" className="border border-border bg-card p-5 sm:p-6">
      <h2 id="waiting-heading" className="text-[13px] font-semibold text-muted-foreground">
        {waiting.length === 1 ? "Someone is waiting for an answer" : "People are waiting for an answer"}
      </h2>
      <ul className="mt-3 divide-y divide-border">
        {waiting.map((t) => (
          <li key={t.id} className="flex items-center gap-3 py-3 first:pt-0 last:pb-0">
            <UserAvatar name={t.authorName ?? "Member"} src={t.authorProfilePictureUrl} size="default" className="shrink-0" />
            <div className="min-w-0 flex-1">
              <p className="truncate text-[15px] font-semibold leading-snug">{t.title}</p>
              <p className="mt-0.5 text-[13px] text-muted-foreground">
                {t.authorName ?? "A member"} posted {formatDistanceToNowStrict(new Date(t.createdAt))} ago{t.categoryName ? ` in ${t.categoryName}` : ""}
              </p>
            </div>
            <Link href={`/forum/${t.id}`} onClick={() => markSeen(t.id)} className="shrink-0 text-[13.5px] font-semibold text-primary underline underline-offset-4">Reply</Link>
            <button type="button" onClick={() => markSeen(t.id)} aria-label={`Not for me: ${t.title}`} title="Not for me"
              className="-mr-2 grid size-10 shrink-0 place-items-center rounded-full text-muted-foreground transition hover:bg-muted hover:text-foreground"><X size={14} /></button>
          </li>
        ))}
      </ul>
    </section>
  );
}
