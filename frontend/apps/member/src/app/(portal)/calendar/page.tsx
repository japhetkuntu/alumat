"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { CalendarDays, PartyPopper, CreditCard, CheckCircle2, Clock, MapPin } from "lucide-react";
import { PageHeader } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { cn, formatCurrency } from "@alumni/ui";
import {
  getEvents, getMyRsvps, getMyCampaigns, getMyContributions, getMyMembershipStatus,
} from "@/lib/member-api";

type CalendarItem = {
  id: string;
  type: "event" | "campaign";
  title: string;
  date: string;
  href: string;
  meta: string;
  mine: boolean;      // relevant to the signed-in member specifically
  done: boolean;       // RSVP'd (events) or paid (campaigns) — no longer actionable
  badge: string;
};

function useCalendarItems() {
  const eventsQ = useQuery({ queryKey: ["cal-events"], queryFn: () => getEvents(1, 100, "Upcoming") });
  const rsvpsQ = useQuery({ queryKey: ["cal-rsvps"], queryFn: () => getMyRsvps("Confirmed") });
  const campaignsQ = useQuery({ queryKey: ["cal-campaigns"], queryFn: () => getMyCampaigns(1, 100) });
  const contributionsQ = useQuery({ queryKey: ["cal-contributions"], queryFn: () => getMyContributions({ pageSize: 200 }) });
  const membershipQ = useQuery({ queryKey: ["cal-membership"], queryFn: getMyMembershipStatus });

  const isLoading = eventsQ.isLoading || rsvpsQ.isLoading || campaignsQ.isLoading || contributionsQ.isLoading || membershipQ.isLoading;

  const items = useMemo<CalendarItem[]>(() => {
    const rsvpEventIds = new Set((rsvpsQ.data ?? []).map((r) => r.eventId));
    const paidCampaignIds = new Set(
      (contributionsQ.data?.results ?? []).filter((c) => c.status === "Confirmed").map((c) => c.campaignId)
    );

    const events: CalendarItem[] = (eventsQ.data?.results ?? [])
      .filter((e) => e.status === "Upcoming")
      .map((e) => ({
        id: e.id,
        type: "event",
        title: e.title,
        date: e.startDate,
        href: `/events/${e.id}`,
        meta: e.venue,
        mine: rsvpEventIds.has(e.id),
        done: rsvpEventIds.has(e.id),
        badge: rsvpEventIds.has(e.id) ? "You're going" : "Open to RSVP",
      }));

    const campaigns: CalendarItem[] = (campaignsQ.data?.results ?? [])
      .filter((c) => c.status === "Active")
      .map((c) => ({
        id: c.id,
        type: "campaign",
        title: c.title,
        date: c.deadline,
        href: "/contributions",
        meta: `${formatCurrency(c.amountPerMember)} per member`,
        mine: !paidCampaignIds.has(c.id),
        done: paidCampaignIds.has(c.id),
        badge: paidCampaignIds.has(c.id) ? "Paid" : "Payment due",
      }));

    return [...events, ...campaigns].sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime());
  }, [eventsQ.data, rsvpsQ.data, campaignsQ.data, contributionsQ.data]);

  return { items, isLoading, membershipStatus: membershipQ.data };
}

function ItemRow({ item }: { item: CalendarItem }) {
  const date = new Date(item.date);
  const Icon = item.type === "event" ? PartyPopper : CreditCard;
  return (
    <Link
      href={item.href}
      className="flex items-center gap-4 p-4 rounded-xl border transition-colors hover:bg-muted/40"
      style={{ borderColor: "var(--border)" }}
    >
      <div className="w-12 h-12 rounded-xl flex flex-col items-center justify-center shrink-0" style={{ background: "var(--muted)" }}>
        <p className="text-[10px] font-bold uppercase leading-none" style={{ color: "var(--muted-foreground)" }}>
          {date.toLocaleDateString(undefined, { month: "short" })}
        </p>
        <p className="text-[16px] font-bold leading-none mt-1" style={{ color: "var(--foreground)" }}>
          {date.getDate()}
        </p>
      </div>

      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <Icon size={13} className="text-muted-foreground shrink-0" />
          <p className="text-[14px] font-semibold truncate" style={{ color: "var(--foreground)" }}>{item.title}</p>
        </div>
        <p className="text-[12.5px] text-muted-foreground mt-0.5 flex items-center gap-1.5 truncate">
          {item.type === "event" ? <MapPin size={11} className="shrink-0" /> : null}
          {item.meta}
        </p>
      </div>

      <Badge
        variant={item.done ? "success" : "secondary"}
        className="shrink-0 gap-1 text-[11px]"
      >
        {item.done ? <CheckCircle2 size={11} /> : <Clock size={11} />}
        {item.badge}
      </Badge>
    </Link>
  );
}

export default function CalendarPage() {
  const [tab, setTab] = useState<"mine" | "all">("mine");
  const { items, isLoading, membershipStatus } = useCalendarItems();

  const visible = tab === "mine" ? items.filter((i) => i.mine) : items;

  const grouped = useMemo(() => {
    const groups = new Map<string, CalendarItem[]>();
    for (const item of visible) {
      const key = new Date(item.date).toLocaleDateString(undefined, { month: "long", year: "numeric" });
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key)!.push(item);
    }
    return Array.from(groups.entries());
  }, [visible]);

  const pendingCount = items.filter((i) => i.mine && !i.done).length;

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[900px] mx-auto space-y-6 sm:space-y-8">
      <PageHeader
        title="Calendar"
        description="Every upcoming event and payment deadline in one place, so nothing catches you by surprise."
      />

      {membershipStatus && !membershipStatus.isCurrentYearPaid && (
        <Link
          href="/contributions"
          className="flex items-center gap-3 p-4 rounded-xl border"
          style={{ background: "var(--color-background-info)", borderColor: "var(--color-border-info)" }}
        >
          <Clock size={16} className="shrink-0" style={{ color: "var(--primary)" }} />
          <p className="text-[13px]" style={{ color: "var(--foreground)" }}>
            <span className="font-semibold">Your membership renewal is due.</span> Renew to keep your active status.
          </p>
        </Link>
      )}

      <div className="flex items-center gap-1 p-1 rounded-xl w-fit" style={{ background: "var(--muted)" }}>
        {([
          { key: "mine", label: `My calendar${pendingCount ? ` (${pendingCount})` : ""}` },
          { key: "all", label: "All activities" },
        ] as const).map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={cn(
              "px-4 py-2 rounded-lg text-[13px] font-semibold transition-colors",
              tab === t.key ? "bg-background shadow-sm" : "text-muted-foreground"
            )}
          >
            {t.label}
          </button>
        ))}
      </div>

      {isLoading ? (
        <div className="space-y-3">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-[76px] rounded-xl bg-muted animate-pulse" />
          ))}
        </div>
      ) : visible.length === 0 ? (
        <EmptyState
          icon={<CalendarDays size={26} />}
          title={tab === "mine" ? "Nothing on your calendar right now" : "No upcoming activity"}
          description={tab === "mine"
            ? "RSVP to an event or check open campaigns — they'll show up here."
            : "New events and campaigns will appear here as they're published."}
        />
      ) : (
        <div className="space-y-6">
          {grouped.map(([month, monthItems]) => (
            <div key={month}>
              <p className="text-[12px] font-bold uppercase tracking-wide mb-3" style={{ color: "var(--muted-foreground)" }}>
                {month}
              </p>
              <div className="space-y-2.5">
                {monthItems.map((item) => (
                  <ItemRow key={`${item.type}-${item.id}`} item={item} />
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
