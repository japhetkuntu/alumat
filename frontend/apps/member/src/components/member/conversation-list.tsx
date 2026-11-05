"use client";

import Link from "next/link";
import { useState } from "react";
import { useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { formatDistanceToNowStrict, isToday, isYesterday, format } from "date-fns";
import { Button, ChevronDown, MessageCircle, UserAvatar, cn } from "@alumni/ui";
import { getIntroductions, getIntroSummary, type IntroCondition, type IntroItem } from "@/lib/member-api";
import { pollWhenOffline, useStreamConnected } from "@/lib/introduction-stream";

type Tab = "chats" | "requests";

/** "now", "5m", "3h", "Yesterday", or a short date: the way a chat list tells time. */
function stamp(iso: string) {
  const d = new Date(iso);
  if (isToday(d)) return Date.now() - d.getTime() < 60_000 ? "now" : format(d, "HH:mm");
  if (isYesterday(d)) return "Yesterday";
  return format(d, "d MMM");
}

const CONDITION_LABEL: Partial<Record<IntroCondition, string>> = { ended: "Ended", blocked: "Blocked", unavailable: "Unavailable" };

function Row({ item, active, children }: { item: IntroItem; active: boolean; children: React.ReactNode }) {
  return (
    <li>
      <Link href={`/connections/${item.id}`} aria-current={active ? "page" : undefined}
        className={cn("flex items-center gap-3 border-l-[3px] px-4 py-3 transition-colors hover:bg-muted/60", active ? "border-primary bg-primary/[0.06]" : "border-transparent")}>
        <UserAvatar src={item.other.photoUrl} name={item.other.name} size="lg" />
        {children}
      </Link>
    </li>
  );
}

export function ConversationList({ activeId }: { activeId?: string }) {
  const params = useSearchParams();
  const [tab, setTab] = useState<Tab>(params.get("tab") === "requests" ? "requests" : "chats");
  const [showArchived, setShowArchived] = useState(false);

  // While the live connection is up nothing here polls; if it drops, these fall back to slow polling until it returns.
  const live = useStreamConnected();
  const summary = useQuery({ queryKey: ["intro-summary"], queryFn: getIntroSummary, refetchInterval: pollWhenOffline(live, 20_000), staleTime: 10_000 });
  const chats = useQuery({ queryKey: ["intros", "chats"], queryFn: () => getIntroductions("chats"), refetchInterval: pollWhenOffline(live, 15_000) });
  // Requests are only listed while that tab is open; the badge count comes from the summary.
  const incoming = useQuery({ queryKey: ["intros", "incoming"], queryFn: () => getIntroductions("incoming"), refetchInterval: pollWhenOffline(live, 30_000), enabled: tab === "requests" });
  const sent = useQuery({ queryKey: ["intros", "sent"], queryFn: () => getIntroductions("sent"), refetchInterval: pollWhenOffline(live, 30_000), enabled: tab === "requests" });

  const unread = summary.data?.unreadChats ?? 0;
  const waiting = summary.data?.incomingRequests ?? 0;

  const tabBtn = (t: Tab, label: string, count: number) => (
    <button type="button" role="tab" aria-selected={tab === t} onClick={() => setTab(t)}
      className={cn("relative flex flex-1 items-center justify-center gap-2 py-3 text-[13.5px] font-semibold transition-colors", tab === t ? "text-foreground" : "text-muted-foreground hover:text-foreground")}>
      {label}
      {count > 0 && <span className="inline-flex h-[18px] min-w-[18px] items-center justify-center rounded-full px-1 text-[12px] font-bold leading-none text-white" style={{ background: "var(--accent)" }}>{count}</span>}
      {tab === t && <span className="absolute inset-x-4 bottom-0 h-[2.5px] rounded-full" style={{ background: "var(--primary)" }} />}
    </button>
  );

  const empty = (title: string, text: string) => (
    <div className="flex flex-col items-center gap-3 px-8 py-14 text-center">
      <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-muted"><MessageCircle size={24} className="text-muted-foreground" /></span>
      <p className="text-[15px] font-semibold">{title}</p>
      <p className="max-w-[26ch] text-[13px] leading-relaxed text-muted-foreground">{text}</p>
      <Link href="/directory"><Button size="sm" variant="outline">Open the directory</Button></Link>
    </div>
  );

  return (
    <div className="flex h-full min-h-0 flex-col bg-background">
      <div className="px-4 pb-1 pt-4 lg:pt-5">
        <h1 className="font-[family-name:var(--font-display)] text-[20px] font-semibold tracking-tight">Connections</h1>
        <p className="mt-0.5 text-[12.5px] text-muted-foreground">Ask for an introduction. If they accept, chat privately.</p>
      </div>
      <div role="tablist" className="flex border-b" style={{ borderColor: "var(--border)" }}>{tabBtn("chats", "Chats", unread)}{tabBtn("requests", "Requests", waiting)}</div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        {tab === "chats" && (() => {
          const all = chats.data ?? [];
          const live = all.filter((c) => (c.condition ?? "open") === "open");
          const archived = all.filter((c) => (c.condition ?? "open") !== "open");
          // Keep the open one in view: if the conversation being read is in the archive, that group starts open.
          const archiveOpen = showArchived || archived.some((c) => c.id === activeId);
          if (chats.isLoading) return <div className="space-y-3 p-4">{[0, 1, 2].map((i) => <div key={i} className="h-14 animate-pulse rounded-xl bg-muted" />)}</div>;
          if (all.length === 0) return empty("No conversations yet", "Find someone in the directory and ask for an introduction. Your chat opens here once they accept.");
          return (
            <div>
              {live.length === 0 && <p className="px-6 py-8 text-center text-[13.5px] text-muted-foreground">No open conversations right now.</p>}
              <ul className="divide-y" style={{ borderColor: "var(--border)" }}>
                {live.map((c) => (
                  <Row key={c.id} item={c} active={c.id === activeId}>
                    <div className="min-w-0 flex-1">
                      <div className="flex items-baseline justify-between gap-2">
                        <p className={cn("truncate text-[14.5px]", c.unread ? "font-bold" : "font-semibold")}>{c.other.name}</p>
                        {c.lastMessageAt && <span className={cn("shrink-0 text-[12px]", c.unread ? "font-semibold text-foreground" : "text-muted-foreground")}>{stamp(c.lastMessageAt)}</span>}
                      </div>
                      <div className="flex items-center justify-between gap-2">
                        <p className={cn("truncate text-[13px]", c.unread ? "font-medium text-foreground" : "text-muted-foreground")}>
                          {c.lastMessagePreview ? `${c.lastMessageMine ? "You: " : ""}${c.lastMessagePreview}` : c.other.headline ?? "Say hello"}
                        </p>
                        {c.unread && <span className="h-2.5 w-2.5 shrink-0 rounded-full" style={{ background: "var(--accent)" }} aria-label="Unread" />}
                      </div>
                    </div>
                  </Row>
                ))}
              </ul>
              {archived.length > 0 && (
                <div className="border-t" style={{ borderColor: "var(--border)" }}>
                  <button type="button" aria-expanded={archiveOpen} onClick={() => setShowArchived((v) => !v)}
                    className="flex w-full items-center justify-between px-4 py-3 text-[12.5px] font-semibold text-muted-foreground transition-colors hover:bg-muted/50 hover:text-foreground">
                    <span>Ended and blocked · {archived.length}</span>
                    <ChevronDown size={14} className={cn("transition-transform", archiveOpen && "rotate-180")} />
                  </button>
                  {archiveOpen && (
                    <ul className="divide-y animate-in fade-in slide-in-from-top-1 duration-200" style={{ borderColor: "var(--border)" }}>
                      {archived.map((c) => (
                        <Row key={c.id} item={c} active={c.id === activeId}>
                          <div className="min-w-0 flex-1 opacity-80">
                            <div className="flex items-baseline justify-between gap-2">
                              <p className="truncate text-[14.5px] font-semibold">{c.other.name}</p>
                              <span className="shrink-0 rounded-full bg-muted px-2 py-0.5 text-[11px] font-semibold text-muted-foreground">{CONDITION_LABEL[c.condition ?? "open"]}</span>
                            </div>
                            <p className="truncate text-[13px] text-muted-foreground">
                              {c.condition === "ended" && c.endedByMe ? "You ended this. Tap to resume." : c.lastMessagePreview ? `${c.lastMessageMine ? "You: " : ""}${c.lastMessagePreview}` : c.other.headline ?? ""}
                            </p>
                          </div>
                        </Row>
                      ))}
                    </ul>
                  )}
                </div>
              )}
            </div>
          );
        })()}

        {tab === "requests" && (
          <div className="pb-6">
            <h2 className="px-4 pb-1 pt-4 text-[12px] font-semibold uppercase tracking-wide text-muted-foreground">Waiting for you</h2>
            {incoming.isLoading ? <div className="p-4"><div className="h-14 animate-pulse rounded-xl bg-muted" /></div>
              : (incoming.data ?? []).length === 0 ? <p className="px-4 py-2 text-[13.5px] text-muted-foreground">Nobody is waiting on you.</p>
              : <ul>{incoming.data!.map((r) => (
                <Row key={r.id} item={r} active={r.id === activeId}>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-baseline justify-between gap-2"><p className="truncate text-[14.5px] font-bold">{r.other.name}</p><span className="shrink-0 text-[12px] text-muted-foreground">{stamp(r.createdAt)}</span></div>
                    {r.reopensIntroductionId && <p className="text-[11.5px] font-semibold text-primary">Wants to pick your chat back up</p>}
                    <p className="line-clamp-2 text-[13px] leading-snug text-muted-foreground">{r.reason}</p>
                  </div>
                </Row>
              ))}</ul>}

            <h2 className="px-4 pb-1 pt-6 text-[12px] font-semibold uppercase tracking-wide text-muted-foreground">You asked</h2>
            {sent.isLoading ? <div className="p-4"><div className="h-14 animate-pulse rounded-xl bg-muted" /></div>
              : (sent.data ?? []).length === 0 ? <p className="px-4 py-2 text-[13.5px] text-muted-foreground">You haven&apos;t asked anyone yet. Open the <Link href="/directory" className="font-semibold underline underline-offset-4">directory</Link> to find someone.</p>
              : <ul>{sent.data!.map((r) => (
                <Row key={r.id} item={r} active={r.id === activeId}>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-baseline justify-between gap-2"><p className="truncate text-[14.5px] font-semibold">{r.other.name}</p><span className="shrink-0 text-[12px] text-muted-foreground">{stamp(r.createdAt)}</span></div>
                    <p className="truncate text-[13px] text-muted-foreground">No reply yet</p>
                  </div>
                </Row>
              ))}</ul>}
          </div>
        )}
      </div>
    </div>
  );
}
