"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { Calendar, MapPin, Users, ArrowRight, Clock, CheckCircle2, Loader2 } from "@alumni/ui";
import Link from "next/link";
import { Pagination } from "@alumni/ui";
import { toast } from "sonner";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { formatDate, formatCurrency, cn } from "@alumni/ui";
import { getEvents, rsvpEvent, cancelRsvp, getMyRsvps } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import type { EventStatus } from "@/types";
import { SourceBadge } from "@/components/member/source-badge";
import { SourceFilterChips } from "@/components/member/source-filter-chips";

const statusVariant: Record<EventStatus, "info" | "success" | "secondary" | "destructive"> = {
  Upcoming:  "info",
  Ongoing:   "success",
  Completed: "secondary",
  Cancelled: "destructive",
};

type EventFilter = "all" | "upcoming" | "going" | "completed";

const EVENT_FILTERS: { value: EventFilter; label: string }[] = [
  { value: "all",       label: "All events" },
  { value: "upcoming",  label: "Upcoming" },
  { value: "going",     label: "Going" },
  { value: "completed", label: "Completed" },
];

export default function MemberEventsPage() {
  const searchParams = useSearchParams();
  const [page,      setPage]      = useState(1);
  const [pendingId, setPendingId] = useState<string | null>(null);
  const [filter,    setFilter]    = useState<EventFilter>("all");
  const [communityId, setCommunityId] = useState<string | null>(() => searchParams.get("communityId"));
  const pageSize = 12;
  const qc = useQueryClient();

  const { data: eventsData, isLoading } = useQuery({
    queryKey:        ["m-events-list", page, communityId],
    queryFn:         () => getEvents(page, pageSize, undefined, communityId || undefined),
    placeholderData: (prev) => prev,
  });

  const { data: myRsvps } = useQuery({
    queryKey: ["m-rsvps"],
    queryFn:  () => getMyRsvps(),
  });

  const rsvpMut = useMutation({
    mutationFn: (eventId: string) => rsvpEvent(eventId),
    onSuccess: () => {
      setPendingId(null);
      qc.invalidateQueries({ queryKey: ["m-rsvps"] });
      qc.invalidateQueries({ queryKey: ["m-events-list"] });
      toast.success("You're in! We'll see you there.");
    },
    onError: (e) => { setPendingId(null); toast.error(handleApiError(e)); },
  });

  const cancelMut = useMutation({
    mutationFn: (eventId: string) => cancelRsvp(eventId),
    onSuccess: () => {
      setPendingId(null);
      qc.invalidateQueries({ queryKey: ["m-rsvps"] });
      qc.invalidateQueries({ queryKey: ["m-events-list"] });
      toast.success("RSVP cancelled.");
    },
    onError: (e) => { setPendingId(null); toast.error(handleApiError(e)); },
  });

  const allEvents  = eventsData?.results ?? [];
  const totalPages = eventsData?.totalPages ?? 1;
  const rsvpSet    = new Set((myRsvps ?? []).filter(r => r.status === "Confirmed").map(r => r.eventId));

  const events = allEvents.filter(e => {
    if (filter === "all") return true;
    if (filter === "going") return rsvpSet.has(e.id);
    if (filter === "upcoming") return e.status === "Upcoming" || e.status === "Ongoing";
    if (filter === "completed") return e.status === "Completed";
    return true;
  });

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">

      <PageHeader eyebrow="Connect in person" title="Events" description="Never miss a Speech Day or AGM — RSVP for annual dinners, speech and prize-giving days, chapter meetings, and reunions." />

      {/* ── Filter pills ── */}
      <div className="flex flex-wrap items-center gap-2">
        {EVENT_FILTERS.map(f => (
          <button
            key={f.value}
            onClick={() => setFilter(f.value)}
            className={cn(
              "px-3.5 py-1.5 text-[12.5px] font-semibold border transition-colors",
              filter === f.value
                ? "bg-primary text-primary-foreground border-transparent"
                : "border-border text-muted-foreground hover:border-primary/40",
            )}
          >
            {f.label}
          </button>
        ))}
        <SourceFilterChips value={communityId} onChange={(v) => { setCommunityId(v); setPage(1); }} />
      </div>

      {/* ── Grid ── */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : events.length === 0 ? (
        <EmptyState
          icon={<Calendar size={40} />}
          title="No events yet"
          description="Check back soon for the next Speech Day, AGM, or reunion."
        />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {events.map((e) => {
            const hasRsvp = rsvpSet.has(e.id);
            const canRsvp = e.status === "Upcoming" || e.status === "Ongoing";
            const isFull  = e.capacity ? e.rsvpCount >= e.capacity : false;
            const isPending = pendingId === e.id;

            return (
              <div
                key={e.id}
                className={cn(
                  "rounded-2xl border overflow-hidden flex flex-col transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md",
                  e.status === "Cancelled" && "opacity-60",
                )}
                style={{ borderColor: "var(--border)", background: "var(--background)" }}
              >
                {/* Banner */}
                <Link href={`/events/${e.id}`} className="block relative shrink-0 overflow-hidden" style={{ height: 180 }}>
                  {e.bannerImageUrl ? (
                    <img
                      src={e.bannerImageUrl}
                      alt={e.title}
                      className="absolute inset-0 w-full h-full object-cover transition-transform duration-500 group-hover:scale-105"
                    />
                  ) : (
                    <div
                      className="absolute inset-0 flex items-center justify-center"
                      style={{ background: "var(--secondary)" }}
                    >
                      <Calendar size={40} style={{ color: "var(--muted-foreground)", opacity: 0.2 }} />
                    </div>
                  )}
                  {/* Scrim */}
                  <div className="absolute inset-0" style={{ background: "linear-gradient(to top, rgba(0,0,0,0.45) 0%, transparent 55%)" }} />

                  {/* Status badge */}
                  <div className="absolute top-3 left-3">
                    <Badge variant={statusVariant[e.status]} className="text-[10px] font-semibold uppercase tracking-wide">
                      {e.status}
                    </Badge>
                  </div>

                  {/* RSVP tick */}
                  {hasRsvp && (
                    <div className="absolute top-3 right-3 w-7 h-7 rounded-full flex items-center justify-center"
                      style={{ background: "rgba(34,197,94,0.85)", backdropFilter: "blur(4px)" }}>
                      <CheckCircle2 size={14} color="white" />
                    </div>
                  )}

                  {/* Date over image */}
                  <div className="absolute bottom-3 left-3 flex items-center gap-1.5 text-white text-[12px] font-semibold">
                    <Clock size={12} />
                    {formatDate(e.startDate)}
                    {e.isTicketed && e.ticketPrice ? (
                      <span className="ml-2 text-white/80">· {formatCurrency(e.ticketPrice)}</span>
                    ) : (
                      <span className="ml-2 text-white/70">· Free</span>
                    )}
                  </div>
                </Link>

                {/* Body */}
                <div className="flex flex-col flex-1 p-4 gap-3">
                  <SourceBadge communityId={e.communityId} communityName={e.communityName} className="self-start" />
                  <Link href={`/events/${e.id}`}>
                    <h3
                      className="text-[15px] font-semibold leading-snug line-clamp-2 transition-colors duration-200 hover:text-primary"
                      style={{ color: "var(--foreground)" }}
                    >
                      {e.title}
                    </h3>
                  </Link>

                  <div className="flex flex-col gap-1.5">
                    <div className="flex items-center gap-1.5 text-[13px]" style={{ color: "var(--muted-foreground)" }}>
                      <MapPin size={12} style={{ color: "var(--primary)" }} />
                      <span className="line-clamp-1">{e.venue}</span>
                    </div>
                    <div className="flex items-center gap-1.5 text-[13px]" style={{ color: "var(--muted-foreground)" }}>
                      <Users size={12} style={{ color: "var(--primary)" }} />
                      {e.rsvpCount} attending
                      {e.capacity ? ` · ${e.capacity - e.rsvpCount} spots left` : ""}
                    </div>
                  </div>

                  {/* Actions */}
                  <div className="flex items-center gap-2 mt-auto pt-3 border-t" style={{ borderColor: "var(--border)" }}>
                    {canRsvp ? (
                      <Button
                        size="sm"
                        variant={hasRsvp ? "outline" : "default"}
                        className={cn(
                          "flex-1 font-semibold text-[13px]",
                          hasRsvp && "border-destructive/40 text-destructive hover:bg-destructive hover:text-white hover:border-destructive",
                        )}
                        style={{ height: 38 }}
                        disabled={isPending || (isFull && !hasRsvp)}
                        onClick={ev => {
                          ev.preventDefault();
                          setPendingId(e.id);
                          if (hasRsvp) cancelMut.mutate(e.id); else rsvpMut.mutate(e.id);
                        }}
                      >
                        {isPending ? (
                          <Loader2 size={14} className="animate-spin" />
                        ) : hasRsvp ? (
                          "Cancel RSVP"
                        ) : isFull ? (
                          "Event full"
                        ) : (
                          "RSVP"
                        )}
                      </Button>
                    ) : (
                      <span
                        className="flex-1 text-center py-2 rounded-lg text-[12px] font-semibold"
                        style={{ background: "var(--secondary)", color: "var(--muted-foreground)" }}
                      >
                        Registration closed
                      </span>
                    )}
                    <Link href={`/events/${e.id}`}>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="shrink-0 rounded-xl transition-colors hover:bg-primary/10"
                        style={{ width: 38, height: 38 }}
                      >
                        <ArrowRight size={16} style={{ color: "var(--muted-foreground)" }} />
                      </Button>
                    </Link>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
    </div>
  );
}
