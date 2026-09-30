"use client";

import { useState } from "react";
import { SegmentedControl, ConfirmModal } from "@alumni/ui";
import { NotifyMeButton } from "@/components/member/notify-me-button";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { Calendar } from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { toast } from "sonner";
import { PageHeader } from "@alumni/ui";
import { getEvents, rsvpEvent, cancelRsvp, getMyRsvps } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { MemberEventCard } from "@/components/member/member-event-card";
import { SourceFilterChips } from "@/components/member/source-filter-chips";

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
      // The Calendar page reads its own separate RSVP/events queries.
      qc.invalidateQueries({ queryKey: ["cal-rsvps"] });
      qc.invalidateQueries({ queryKey: ["cal-events"] });
      toast.success("You're in! We'll see you there.");
    },
    onError: (e) => { setPendingId(null); toast.error(handleApiError(e)); },
  });

  const [cancelTarget, setCancelTarget] = useState<{ id: string; title: string } | null>(null);

  const cancelMut = useMutation({
    mutationFn: (eventId: string) => cancelRsvp(eventId),
    onSuccess: () => {
      setPendingId(null);
      setCancelTarget(null);
      qc.invalidateQueries({ queryKey: ["m-rsvps"] });
      qc.invalidateQueries({ queryKey: ["m-events-list"] });
      qc.invalidateQueries({ queryKey: ["cal-rsvps"] });
      qc.invalidateQueries({ queryKey: ["cal-events"] });
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

      <PageHeader eyebrow="Connect in person" title="Events" description="Never miss a Speech Day or AGM. RSVP for annual dinners, speech and prize-giving days, chapter meetings, and reunions." />

      {/* ── Filters ── */}
      <div className="flex flex-wrap items-center gap-2">
        <SegmentedControl label="Filter events" options={EVENT_FILTERS} value={filter} onChange={setFilter} className="w-full sm:w-auto" />
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
          title="Events bring your community together"
          description="Reunions, annual dinners, chapter meetings and career evenings are posted here. RSVP with one tap and see who else is going. Nothing is scheduled yet."
          action={<NotifyMeButton />}
        />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {events.map((e) => {
            const hasRsvp = rsvpSet.has(e.id);
            const isPast = new Date(e.endDate ?? e.startDate).getTime() < Date.now();
            const canRsvp = (e.status === "Upcoming" || e.status === "Ongoing") && !isPast;
            const isFull  = e.capacity ? e.rsvpCount >= e.capacity : false;
            const isPending = pendingId === e.id;

            return (
              <MemberEventCard
                key={e.id} event={e} hasRsvp={hasRsvp} isPast={isPast} canRsvp={canRsvp}
                isFull={!!isFull} isPending={isPending} detailHref={`/events/${e.id}`}
                onRsvp={() => {
                  if (hasRsvp) { setCancelTarget({ id: e.id, title: e.title }); return; }
                  setPendingId(e.id);
                  rsvpMut.mutate(e.id);
                }}
              />
            );
          })}
        </div>
      )}

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />

      <ConfirmModal
        open={!!cancelTarget}
        title="Cancel your RSVP?"
        message={`You will give up your place at "${cancelTarget?.title ?? ""}". You can RSVP again later if there is still room.`}
        confirmLabel="Yes, cancel RSVP"
        cancelLabel="Keep my spot"
        emphasis="cancel"
        isLoading={cancelMut.isPending}
        onConfirm={() => { if (cancelTarget) cancelMut.mutate(cancelTarget.id); }}
        onCancel={() => setCancelTarget(null)}
      />
    </div>
  );
}
