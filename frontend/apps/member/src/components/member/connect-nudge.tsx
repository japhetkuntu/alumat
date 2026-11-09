"use client";

import Link from "next/link";
import { useState, useSyncExternalStore } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, MessageCircle, X } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { useFeatures } from "@/components/member/member-layout";
import { getIntroSummary, hasAnyIntroduction } from "@/lib/member-api";

const key = (userId: string) => `member-connect-nudge-dismissed:${userId}`;
const subscribe = (cb: () => void) => { window.addEventListener("storage", cb); return () => window.removeEventListener("storage", cb); };

/**
 * Two things, never both. When people are waiting to connect with this member, that is said first and plainly (the number is the
 * strongest reason to open the app). Otherwise, for a member who has never asked anyone and never been asked, one short card says
 * that they can message other members and how; it can be dismissed for good. Silent for everyone else.
 */
export function ConnectNudge() {
  const { user } = useAuth();
  const features = useFeatures();
  const on = features.enabled("Introductions");
  const directoryOn = features.enabled("Directory");

  const summary = useQuery({ queryKey: ["intro-summary"], queryFn: getIntroSummary, enabled: on, staleTime: 30_000, retry: false });
  const waiting = summary.data?.incomingRequests ?? 0;

  const dismissed = useSyncExternalStore(subscribe, () => {
    if (!user?.id) return true;
    try { return localStorage.getItem(key(user.id)) === "1"; } catch { return false; }
  }, () => true);
  const [hidden, setHidden] = useState(false);

  // Only asked when nothing is waiting and the card has not been dismissed: has this member ever connected with anyone?
  const needsIntro = on && directoryOn && summary.isSuccess && waiting === 0 && !dismissed;
  const mine = useQuery({
    queryKey: ["intro-any"],
    queryFn: hasAnyIntroduction,
    enabled: needsIntro, staleTime: 5 * 60_000, retry: false,
  });

  if (!on) return null;

  if (waiting > 0) {
    return (
      <section aria-labelledby="connect-waiting" className="flex flex-col gap-4 border border-primary/30 bg-primary/[0.05] p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6">
        <div className="flex items-start gap-3.5">
          <span className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-primary text-primary-foreground"><MessageCircle size={18} /></span>
          <div>
            <h2 id="connect-waiting" className="text-[16px] font-semibold leading-snug">
              {waiting === 1 ? "Someone wants to connect with you" : `${waiting} people want to connect with you`}
            </h2>
            <p className="mt-1 text-[13.5px] leading-relaxed text-muted-foreground">They&apos;re waiting for your answer. If you accept, you can chat privately.</p>
          </div>
        </div>
        <Link href="/connections?tab=requests" className="shrink-0"><Button className="w-full rounded-full px-6 font-semibold sm:w-auto">See who</Button></Link>
      </section>
    );
  }

  if (!needsIntro || hidden || mine.data !== false) return null;

  return (
    <section aria-labelledby="connect-intro" className="relative border border-border bg-card p-5 sm:p-6">
      <button type="button" aria-label="Dismiss" onClick={() => { setHidden(true); try { localStorage.setItem(key(user!.id), "1"); window.dispatchEvent(new Event("storage")); } catch { /* ignore */ } }}
        className="absolute right-3 top-3 grid h-8 w-8 place-items-center rounded-full text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"><X size={14} /></button>
      <div className="flex items-start gap-3.5 pr-8">
        <span className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-primary/10 text-primary"><MessageCircle size={18} /></span>
        <div className="min-w-0">
          <h2 id="connect-intro" className="text-[16px] font-semibold leading-snug">You can message other members</h2>
          <p className="mt-1 text-[13.5px] leading-relaxed text-muted-foreground">Find someone in the directory and ask for an introduction with a short note. If they accept, you chat privately.</p>
          <Link href="/directory" className="mt-3 inline-block"><Button variant="outline" size="sm" className="rounded-full px-5 font-semibold text-foreground">Find someone</Button></Link>
        </div>
      </div>
    </section>
  );
}
