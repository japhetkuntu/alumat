"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Bell, Lock, Pin } from "@alumni/ui";
import { Badge, Button, ChipRow, EventUpdateBlockView, cn, formatDateTime } from "@alumni/ui";
import { getEventUpdates } from "@/lib/member-api";
import type { EventUpdate } from "@/types";

const SEEN_KEY = (eventId: string) => `eventUpdatesSeen:${eventId}`;

function readSeen(eventId: string): number {
  try { return Number(localStorage.getItem(SEEN_KEY(eventId))) || 0; } catch { return 0; }
}

function UpdateCard({ update, isNew }: { update: EventUpdate; isNew: boolean }) {
  return (
    <article className={cn("border bg-card", isNew ? "border-primary/50" : "border-border")}>
      <header className="space-y-1.5 border-b border-border p-4 sm:p-5">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="info" className="text-[11.5px] font-semibold uppercase tracking-wide">{update.category}</Badge>
          {update.isPinned && <Badge variant="secondary" className="gap-1 text-[11.5px]"><Pin size={10} />Pinned</Badge>}
          {isNew && <Badge variant="success" className="text-[11.5px] font-bold uppercase tracking-wide">New</Badge>}
        </div>
        <h3 className="text-[17px] font-semibold leading-snug text-foreground">{update.title}</h3>
        <p className="text-[12.5px] text-muted-foreground">{formatDateTime(update.createdAt)}{update.updatedAt ? " · edited" : ""}</p>
      </header>
      <div className="space-y-5 p-4 sm:p-5">
        {update.blocks.map((b) => <EventUpdateBlockView key={b.id} block={b} />)}
      </div>
    </article>
  );
}

/**
 * What the organiser has posted to an event. People who are going read the updates; everyone else sees that updates exist
 * (how many, and of what kind) and what it takes to read them. `onJoin` is the page's own RSVP action.
 */
export function EventUpdates({ eventId, joinLabel, onJoin }: { eventId: string; joinLabel?: string; onJoin?: () => void }) {
  const { data } = useQuery({ queryKey: ["event-updates", eventId], queryFn: () => getEventUpdates(eventId) });
  const [filter, setFilter] = useState<string | null>(null);
  // What was already seen on the last visit, so only what came since is marked New. Read once, when the updates first show.
  // First time here: only what was posted in the last three days counts as new, rather than everything ever posted.
  const [seen] = useState(() => readSeen(eventId) || Date.now() - 3 * 24 * 60 * 60 * 1000);

  const newest = data && !data.locked ? Math.max(0, ...data.updates.map((u) => Date.parse(u.createdAt))) : 0;
  useEffect(() => {
    if (!newest) return;
    // Remember the newest one now. This visit keeps showing what was new on arrival (`seen` is read once), and the next visit starts from here.
    try { localStorage.setItem(SEEN_KEY(eventId), String(newest)); } catch { /* storage unavailable */ }
  }, [eventId, newest]);

  // Arriving from a notification (…#updates) lands on this section.
  const ready = !!data && (data.locked ? data.count > 0 : true);
  useEffect(() => {
    if (ready && window.location.hash === "#updates") document.getElementById("updates")?.scrollIntoView({ block: "start" });
  }, [ready]);

  if (!data) return null;

  if (data.locked) {
    if (data.count === 0) return null;   // nothing to tease
    return (
      <section id="updates" className="scroll-mt-20 space-y-3">
        <h2 className="font-[family-name:var(--font-display)] mb-4 pb-3 border-b"
        style={{ fontSize: "1.1rem", fontWeight: 700, color: "var(--foreground)", borderColor: "var(--border)" }}>Updates</h2>
        <div className="space-y-4 border border-border bg-card p-5">
          <div className="flex items-start gap-3">
            <span className="flex size-10 shrink-0 items-center justify-center bg-primary/10 text-primary"><Lock size={17} /></span>
            <div>
              <p className="text-[15px] font-semibold">{data.count} {data.count === 1 ? "update" : "updates"} for people who are going</p>
              <p className="text-[13.5px] text-muted-foreground">The organiser is sharing things with attendees. {onJoin ? "Reserve your seat to read them." : "They are only visible to people with a seat."}</p>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            {data.categories.map((c) => (
              <span key={c.category} className="border border-border bg-muted/40 px-2.5 py-1 text-[12.5px] font-medium text-muted-foreground">{c.category} · {c.count}</span>
            ))}
          </div>
          {onJoin && <Button onClick={onJoin} className="w-full font-semibold sm:w-auto">{joinLabel ?? "Reserve my seat"}</Button>}
        </div>
      </section>
    );
  }

  const isNew = (u: EventUpdate) => Date.parse(u.createdAt) > seen;
  const categories = data.categories;
  const visible = filter ? data.updates.filter((u) => u.category === filter) : data.updates;

  return (
    <section id="updates" className="scroll-mt-20 space-y-4">
      <div className="flex items-center justify-between gap-3">
        <h2
          className="flex flex-1 items-center gap-2 font-[family-name:var(--font-display)] pb-3 border-b"
          style={{ fontSize: "1.1rem", fontWeight: 700, color: "var(--foreground)", borderColor: "var(--border)" }}
        >
          Updates
          {data.count > 0 && <span className="text-[13px] font-medium text-muted-foreground">{data.count}</span>}
        </h2>
      </div>

      {data.count === 0 ? (
        <div className="flex items-center gap-3 border border-dashed border-border bg-muted/30 p-5">
          <Bell size={18} className="shrink-0 text-muted-foreground" />
          <p className="text-[14px] text-muted-foreground">Nothing yet. When the organiser posts the programme, photos or files for this event, they will appear here and you will be notified.</p>
        </div>
      ) : (
        <>
          {categories.length > 1 && (
            <ChipRow label="Filter updates by category" activeKey={filter ?? "all"}>
              {[{ category: "All", count: data.count, value: null as string | null }, ...categories.map((c) => ({ ...c, value: c.category as string | null }))].map((c) => (
                <button key={c.category} type="button" aria-pressed={filter === c.value} onClick={() => setFilter(c.value)}
                  className={cn("shrink-0 snap-start border px-3 py-1.5 text-[13px] font-semibold transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary",
                    filter === c.value ? "border-primary bg-primary text-primary-foreground" : "border-border bg-card hover:border-primary/50")}>
                  {c.category} <span className="opacity-70">{c.count}</span>
                </button>
              ))}
            </ChipRow>
          )}
          <div className="space-y-4">
            {visible.map((u) => <UpdateCard key={u.id} update={u} isNew={isNew(u)} />)}
          </div>
        </>
      )}
    </section>
  );
}
