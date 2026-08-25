"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { format, isBefore, isToday, startOfDay } from "date-fns";
import { CalendarDays, PartyPopper, CreditCard, CheckCircle2, Clock, MapPin, X } from "lucide-react";
import { PageHeader, Card, CardContent } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { cn, formatCurrency } from "@alumni/ui";
import {
  getEvents, getMyRsvps, getMyCampaigns, getMyContributions, getMyMembershipStatus,
} from "@/lib/member-api";
import { ActivityCalendarGrid, type CalendarItem } from "@/components/member/activity-calendar-grid";

function useCalendarItems() {
  const eventsQ = useQuery({ queryKey: ["cal-events"], queryFn: () => getEvents(1, 250, "All") });
  const rsvpsQ = useQuery({ queryKey: ["cal-rsvps"], queryFn: () => getMyRsvps("Confirmed") });
  const campaignsQ = useQuery({ queryKey: ["cal-campaigns"], queryFn: () => getMyCampaigns(1, 250) });
  const contributionsQ = useQuery({ queryKey: ["cal-contributions"], queryFn: () => getMyContributions({ pageSize: 200 }) });
  const membershipQ = useQuery({ queryKey: ["cal-membership"], queryFn: getMyMembershipStatus });

  const isLoading = eventsQ.isLoading || rsvpsQ.isLoading || campaignsQ.isLoading || contributionsQ.isLoading || membershipQ.isLoading;

  const items = useMemo<CalendarItem[]>(() => {
    const rsvpEventIds = new Set((rsvpsQ.data ?? []).map((r) => r.eventId));
    const paidCampaignIds = new Set(
      (contributionsQ.data?.results ?? []).filter((c) => c.status === "Confirmed").map((c) => c.campaignId)
    );

    const events: CalendarItem[] = (eventsQ.data?.results ?? [])
      .filter((e) => e.status !== "Cancelled")
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
  const isPast = isBefore(date, startOfDay(new Date())) && !item.done;
  const badgeLabel = isPast ? (item.type === "event" ? "Past event" : "Deadline passed") : item.badge;
  return (
    <Link
      href={item.href}
      className={cn(
        "flex items-center gap-3 sm:gap-4 p-3 sm:p-4 rounded-xl border transition-colors hover:bg-muted/40",
        isPast && "opacity-70"
      )}
      style={{ borderColor: "var(--border)" }}
    >
      <div className="w-11 h-11 sm:w-12 sm:h-12 rounded-xl flex flex-col items-center justify-center shrink-0" style={{ background: "var(--muted)" }}>
        <p className="text-[9px] sm:text-[10px] font-bold uppercase leading-none" style={{ color: "var(--muted-foreground)" }}>
          {format(date, "MMM")}
        </p>
        <p className="text-[14px] sm:text-[16px] font-bold leading-none mt-1" style={{ color: "var(--foreground)" }}>
          {format(date, "d")}
        </p>
      </div>

      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2 min-w-0">
          <Icon size={13} className="text-muted-foreground shrink-0" />
          <p className="text-[13.5px] sm:text-[14px] font-semibold truncate" style={{ color: "var(--foreground)" }}>{item.title}</p>
        </div>
        <p className="text-[12px] sm:text-[12.5px] text-muted-foreground mt-0.5 flex items-center gap-1.5 truncate">
          {item.type === "event" ? <MapPin size={11} className="shrink-0" /> : null}
          <span className="truncate">{item.meta}</span>
        </p>
      </div>

      <Badge
        variant={item.done ? "success" : isPast ? "neutral" : "secondary"}
        className="shrink-0 gap-1 text-[10.5px] sm:text-[11px] whitespace-nowrap"
      >
        {item.done ? <CheckCircle2 size={11} /> : <Clock size={11} />}
        {badgeLabel}
      </Badge>
    </Link>
  );
}

export default function CalendarPage() {
  const [tab, setTab] = useState<"mine" | "all">("mine");
  const [selectedDate, setSelectedDate] = useState<Date>(() => new Date());
  const { items, isLoading, membershipStatus } = useCalendarItems();

  const visible = tab === "mine" ? items.filter((i) => i.mine) : items;

  const itemsByDate = useMemo(() => {
    const map = new Map<string, CalendarItem[]>();
    for (const item of visible) {
      const key = format(new Date(item.date), "yyyy-MM-dd");
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(item);
    }
    return map;
  }, [visible]);

  const selectedKey = format(selectedDate, "yyyy-MM-dd");
  const selectedItems = itemsByDate.get(selectedKey) ?? [];
  const selectedIsPast = isBefore(startOfDay(selectedDate), startOfDay(new Date())) && !isToday(selectedDate);

  const pendingCount = items.filter((i) => i.mine && !i.done).length;

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[900px] mx-auto space-y-6 sm:space-y-8">
      <PageHeader
        title="Calendar"
        description="Every event and payment deadline in one place, so nothing catches you by surprise."
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
              "px-3 sm:px-4 py-2 rounded-lg text-[12.5px] sm:text-[13px] font-semibold transition-colors whitespace-nowrap",
              tab === t.key ? "bg-background shadow-sm" : "text-muted-foreground"
            )}
          >
            {t.label}
          </button>
        ))}
      </div>

      {isLoading ? (
        <div className="grid grid-cols-7 gap-1">
          {Array.from({ length: 35 }).map((_, i) => (
            <div key={i} className="min-h-[52px] sm:min-h-[68px] lg:min-h-[92px] rounded-lg bg-muted animate-pulse" />
          ))}
        </div>
      ) : items.length === 0 ? (
        <EmptyState
          icon={<CalendarDays size={26} />}
          title="No activity yet"
          description="New events and campaigns will appear here as they're published."
        />
      ) : (
        <Card>
          <CardContent className="p-4 sm:p-6">
            <ActivityCalendarGrid
              itemsByDate={itemsByDate}
              selectedDate={selectedDate}
              onSelectDate={setSelectedDate}
            />

            <div
              key={selectedKey}
              className="mt-5 pt-5 border-t animate-in fade-in slide-in-from-top-1 duration-200"
              style={{ borderColor: "var(--border)" }}
            >
              <div className="flex items-center justify-between gap-3 mb-3">
                <p className="text-[13.5px] sm:text-[14.5px] font-bold truncate" style={{ color: "var(--foreground)" }}>
                  {format(selectedDate, "EEEE, MMMM d, yyyy")}
                  {isToday(selectedDate) && (
                    <span className="ml-2 align-middle">
                      <Badge variant="default" size="sm">Today</Badge>
                    </span>
                  )}
                </p>
                {!isToday(selectedDate) && (
                  <button
                    type="button"
                    aria-label="Back to today"
                    onClick={() => setSelectedDate(new Date())}
                    className="shrink-0 w-8 h-8 rounded-lg flex items-center justify-center transition-colors hover:bg-muted"
                    style={{ color: "var(--muted-foreground)" }}
                  >
                    <X size={15} />
                  </button>
                )}
              </div>

              {selectedItems.length === 0 ? (
                <p
                  className={cn("text-[13px] py-6 text-center rounded-xl", selectedIsPast && "opacity-70")}
                  style={{ color: "var(--muted-foreground)", background: "var(--muted)" }}
                >
                  Nothing scheduled on this day.
                </p>
              ) : (
                <div className="space-y-2.5">
                  {selectedItems.map((item) => (
                    <ItemRow key={`${item.type}-${item.id}`} item={item} />
                  ))}
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
