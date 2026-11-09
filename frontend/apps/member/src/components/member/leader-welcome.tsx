"use client";

import { useEffect, useState } from "react";
import { Button } from "@alumni/ui";
import type { ManageGroup } from "@/lib/member-api";

/** What each thing a role allows means, as a short phrase that reads in a sentence. Anything the server adds later is left out rather than shown as a raw key. */
const PHRASE: Record<string, string> = {
  ManageMembers: "welcome and approve members",
  MessageGroup: "message the whole group",
  ManageEvents: "put on events",
  PostNews: "share news",
  PostJobs: "post jobs",
  ShareResources: "share links",
  ManageFundraisers: "run fundraisers",
  ViewGiving: "see what has been given",
  ViewReport: "see how the group is doing",
};

const storageKey = (g: Pick<ManageGroup, "scopeType" | "scopeId">) => `leader-welcome:${g.scopeType}:${g.scopeId}`;

/**
 * Shown once, the first time a leader opens a group they have just been given: what they can do here and where to find it.
 * Dismissed for good with one tap. Quiet if storage is unavailable (it just shows again), never blocks anything.
 */
export function LeaderWelcome({ group }: { group: ManageGroup }) {
  const [show, setShow] = useState(false);
  useEffect(() => {
    try { setShow(localStorage.getItem(storageKey(group)) !== "done"); } catch { setShow(true); }
  }, [group]);
  if (!show || group.privileges.length === 0) return null;

  const done = () => {
    setShow(false);
    try { localStorage.setItem(storageKey(group), "done"); } catch { /* shown again next time, which is harmless */ }
  };
  const phrases = group.privileges.map((p) => PHRASE[p]).filter(Boolean);
  if (phrases.length === 0) return null;   // nothing the card knows how to describe
  const sentence = phrases.length > 1 ? `${phrases.slice(0, -1).join(", ")} and ${phrases[phrases.length - 1]}` : phrases[0];

  return (
    <section aria-labelledby="leader-welcome" className="rounded-[18px] border border-primary/30 bg-primary/5 p-4">
      <h2 id="leader-welcome" className="text-[15px] font-semibold">You look after {group.name}</h2>
      <p className="mt-1 text-[13.5px] leading-relaxed text-muted-foreground">
        As {group.roleNames.join(" and ")}, you can {sentence}. Pick a tab below to start. Your roles are on your profile, where you can also step down from one.
      </p>
      <Button size="sm" className="mt-3" onClick={done}>Got it</Button>
    </section>
  );
}
