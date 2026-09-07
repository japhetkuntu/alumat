"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import {
  Calendar, MapPin, Users, Ticket, ArrowLeft,
  Loader2, CheckCircle2, Navigation,
} from "@alumni/ui";
import { toast } from "sonner";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { MediaGallery } from "@alumni/ui";
import { formatCurrency, formatDate } from "@alumni/ui";
import { getEventById, rsvpEvent, cancelRsvp, getMyRsvps } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { EmptyState } from "@alumni/ui";
import type { AlumniEvent } from "@/types";

const statusVariant: Record<string, "info" | "success" | "secondary" | "destructive"> = {
  Upcoming:  "info",
  Ongoing:   "success",
  Completed: "secondary",
  Cancelled: "destructive",
};

export default function EventDetailPage() {
  const { id }  = useParams() as { id: string };
  const router  = useRouter();
  const qc      = useQueryClient();
  const [rsvpConfirmOpen, setRsvpConfirmOpen] = useState(false);

  const { data: event, isLoading } = useQuery({
    queryKey: ["event", id],
    queryFn:  () => getEventById(id),
  });

  const { data: myRsvps } = useQuery({
    queryKey: ["m-rsvps"],
    queryFn:  () => getMyRsvps(),
  });

  const rsvpMut = useMutation({
    mutationFn: () => rsvpEvent(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-rsvps"] });
      qc.invalidateQueries({ queryKey: ["event", id] });
      setRsvpConfirmOpen(false);
      toast.success("You're in! We'll see you there.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const cancelMut = useMutation({
    mutationFn: () => cancelRsvp(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-rsvps"] });
      qc.invalidateQueries({ queryKey: ["event", id] });
      toast.success("RSVP cancelled.");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  /* ── Loading ── */
  if (isLoading) {
    return (
      <div className="p-6 lg:p-10 max-w-5xl mx-auto space-y-6 animate-pulse">
        <div className="h-6 w-24 rounded-lg" style={{ background: "var(--secondary)" }} />
        <div className="h-64 w-full rounded-2xl" style={{ background: "var(--secondary)" }} />
        <div className="space-y-3">
          <div className="h-8 w-2/3 rounded-lg" style={{ background: "var(--secondary)" }} />
          <div className="h-4 w-full rounded-lg"  style={{ background: "var(--secondary)" }} />
          <div className="h-4 w-4/5 rounded-lg"  style={{ background: "var(--secondary)" }} />
        </div>
      </div>
    );
  }

  if (!event) return (
    <div className="p-6 lg:p-10 max-w-5xl mx-auto">
      <Button variant="ghost" size="sm" className="mb-6 font-semibold gap-2" onClick={() => router.push("/events")}>
        <ArrowLeft size={15} /> Back to events
      </Button>
      <EmptyState
        icon={<Calendar size={40} />}
        title="Event not found"
        description="This event may have been removed or the link is incorrect."
      />
    </div>
  );

  const hasRsvp  = (myRsvps ?? []).some(r => r.eventId === id && r.status === "Confirmed");
  const canRsvp  = event.status === "Upcoming" || event.status === "Ongoing";
  const isFull   = event.capacity ? event.rsvpCount >= event.capacity : false;
  const mapUrl   = event.googleLocationUrl?.trim()
    ? event.googleLocationUrl.trim()
    : `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(event.venue)}`;

  const hasMedia = (event.imageUrls?.length ?? 0) > 0 || (event.youtubeVideoUrls?.length ?? 0) > 0;

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto pb-20">

      {/* ── Nav row ── */}
      <div className="flex items-center justify-between mb-6 sm:mb-8">
        <button
          onClick={() => router.push("/events")}
          className="flex items-center gap-1.5 text-[13.5px] font-semibold transition-colors hover:underline"
          style={{ color: "var(--muted-foreground)" }}
        >
          <ArrowLeft size={15} /> Back to events
        </button>
        <Badge
          variant={statusVariant[event.status] ?? "secondary"}
          className="text-[11px] font-semibold uppercase tracking-wide"
        >
          {event.status}
        </Badge>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 lg:gap-12">

        {/* ════════════════════════════════════════════
            LEFT — main content
        ════════════════════════════════════════════ */}
        <div className="lg:col-span-7 space-y-8">

          {/* Title + meta */}
          <div>
            <h1
              className="font-[family-name:var(--font-display)] leading-tight mb-4"
              style={{ fontSize: "clamp(1.5rem, 3.5vw, 2.25rem)", fontWeight: 700, letterSpacing: "-0.02em", color: "var(--foreground)" }}
            >
              {event.title}
            </h1>
            <div className="flex flex-wrap gap-x-5 gap-y-2">
              {[
                { icon: Calendar, text: formatDate(event.startDate)             },
                { icon: MapPin,   text: event.venue                              },
                { icon: Users,    text: `${event.rsvpCount} attending`           },
              ].map(({ icon: Icon, text }) => (
                <div key={text} className="flex items-center gap-1.5 text-[13.5px]" style={{ color: "var(--muted-foreground)" }}>
                  <Icon size={14} style={{ color: "var(--primary)" }} />
                  {text}
                </div>
              ))}
            </div>
          </div>

          {/* Banner */}
          <div className="rounded-2xl overflow-hidden border" style={{ borderColor: "var(--border)" }}>
            {event.bannerImageUrl ? (
              <img
                src={event.bannerImageUrl}
                alt={event.title}
                className="w-full object-cover"
                style={{ maxHeight: 420 }}
              />
            ) : (
              <div
                className="flex items-center justify-center py-16"
                style={{ background: "var(--secondary)" }}
              >
                <Calendar size={48} style={{ color: "var(--muted-foreground)", opacity: 0.2 }} />
              </div>
            )}
          </div>

          {/* RSVP block — mobile only */}
          <div className="lg:hidden">
            <RsvpBlock
              event={event}
              hasRsvp={hasRsvp}
              canRsvp={canRsvp}
              isFull={isFull}
              mapUrl={mapUrl}
              onRsvp={() => setRsvpConfirmOpen(true)}
              onCancel={() => cancelMut.mutate()}
              cancelPending={cancelMut.isPending}
              rsvpPending={rsvpMut.isPending}
            />
          </div>

          {/* Description */}
          <div>
            <h2
              className="font-[family-name:var(--font-display)] mb-4 pb-3 border-b"
              style={{ fontSize: "1.1rem", fontWeight: 700, color: "var(--foreground)", borderColor: "var(--border)" }}
            >
              About this event
            </h2>
            <p
              className="whitespace-pre-wrap leading-[1.85]"
              style={{ fontSize: "0.9625rem", color: "var(--muted-foreground)" }}
            >
              {event.description || "More details will be shared closer to the date. RSVP to stay informed."}
            </p>
          </div>

          {/* Media gallery */}
          {hasMedia && (
            <div>
              <h2
                className="font-[family-name:var(--font-display)] mb-4 pb-3 border-b"
                style={{ fontSize: "1.1rem", fontWeight: 700, color: "var(--foreground)", borderColor: "var(--border)" }}
              >
                Photos & videos
              </h2>
              <MediaGallery imageUrls={event.imageUrls} youtubeUrls={event.youtubeVideoUrls} />
            </div>
          )}
        </div>

        {/* ════════════════════════════════════════════
            RIGHT — sticky sidebar
        ════════════════════════════════════════════ */}
        <aside className="hidden lg:block lg:col-span-5">
          <div
            className="rounded-2xl border overflow-hidden lg:sticky lg:top-24"
            style={{ borderColor: "var(--border)", background: "var(--background)" }}
          >
            <div className="h-1 w-full" style={{ background: "var(--primary)" }} />
            <div className="p-5 sm:p-6">
              <RsvpBlock
                event={event}
                hasRsvp={hasRsvp}
                canRsvp={canRsvp}
                isFull={isFull}
                mapUrl={mapUrl}
                onRsvp={() => setRsvpConfirmOpen(true)}
                onCancel={() => cancelMut.mutate()}
                cancelPending={cancelMut.isPending}
                rsvpPending={rsvpMut.isPending}
              />
            </div>
          </div>
        </aside>

      </div>

      <ConfirmModal
        open={rsvpConfirmOpen}
        title="Confirm your spot"
        message={`Reserve your place at "${event.title}"?`}
        confirmLabel="Yes, RSVP"
        variant="default"
        isLoading={rsvpMut.isPending}
        onConfirm={() => rsvpMut.mutate()}
        onCancel={() => setRsvpConfirmOpen(false)}
      />
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   RSVP BLOCK — shared between mobile (inline) and desktop (sidebar)
   ───────────────────────────────────────────────────────────────────────── */
function RsvpBlock({
  event, hasRsvp, canRsvp, isFull, mapUrl,
  onRsvp, onCancel, cancelPending, rsvpPending,
}: {
  event: AlumniEvent;
  hasRsvp: boolean;
  canRsvp: boolean;
  isFull: boolean;
  mapUrl: string;
  onRsvp: () => void;
  onCancel: () => void;
  cancelPending: boolean;
  rsvpPending: boolean;
}) {
  return (
    <div className="space-y-5">

      {/* Ticket / price */}
      <div className="flex items-center justify-between">
        <div>
          <p className="text-[11.5px] font-semibold uppercase tracking-wide mb-0.5" style={{ color: "var(--muted-foreground)" }}>
            Registration
          </p>
          <p
            className="font-[family-name:var(--font-display)] font-bold"
            style={{ fontSize: "1.4rem", color: "var(--foreground)", letterSpacing: "-0.02em" }}
          >
            {event.isTicketed && event.ticketPrice ? formatCurrency(event.ticketPrice) : "Free"}
          </p>
        </div>
        {event.isTicketed && <Ticket size={22} style={{ color: "var(--primary)" }} />}
      </div>

      {/* RSVP status — if already going */}
      {hasRsvp && (
        <div
          className="flex items-center gap-3 p-3.5 rounded-xl"
          style={{ background: "rgba(34,197,94,0.08)", border: "1px solid rgba(34,197,94,0.2)" }}
        >
          <CheckCircle2 size={18} className="text-success shrink-0" />
          <p className="text-[13.5px] font-semibold text-success">
            You&apos;re going to this event.
          </p>
        </div>
      )}

      {/* Action button */}
      {canRsvp ? (
        hasRsvp ? (
          <Button
            variant="outline"
            className="w-full font-semibold text-[14px] gap-2"
            style={{ height: 44, borderColor: "var(--destructive)", color: "var(--destructive)" }}
            disabled={cancelPending}
            onClick={onCancel}
          >
            {cancelPending ? <Loader2 size={15} className="animate-spin" /> : "Cancel my RSVP"}
          </Button>
        ) : (
          <Button
            className="w-full font-bold text-[15px] gap-2"
            style={{ height: 48 }}
            disabled={rsvpPending || isFull}
            onClick={onRsvp}
          >
            {rsvpPending ? (
              <Loader2 size={15} className="animate-spin" />
            ) : isFull ? (
              "Event fully booked"
            ) : (
              <><Calendar size={16} /> Confirm RSVP</>
            )}
          </Button>
        )
      ) : (
        <div
          className="p-4 rounded-xl text-center"
          style={{ background: "var(--secondary)", border: "1px solid var(--border)" }}
        >
          <p className="text-[14px] font-semibold mb-0.5" style={{ color: "var(--foreground)" }}>
            Registration closed
          </p>
          <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>
            This event has ended or was cancelled.
          </p>
        </div>
      )}

      {/* Details list */}
      <div
        className="rounded-xl border divide-y overflow-hidden"
        style={{ borderColor: "var(--border)" }}
      >
        {/* Venue with map link */}
        <div className="flex items-center gap-3 px-4 py-3.5">
          <div
            className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0"
            style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
          >
            <MapPin size={14} style={{ color: "var(--primary)" }} />
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>Venue</p>
            <p className="text-[13.5px] font-semibold truncate" style={{ color: "var(--foreground)" }}>
              {event.venue}
            </p>
          </div>
          <a
            href={mapUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="shrink-0 w-7 h-7 rounded-lg flex items-center justify-center transition-colors hover:bg-primary hover:text-white"
            style={{ background: "var(--secondary)", color: "var(--muted-foreground)" }}
            title="Open in Google Maps"
          >
            <Navigation size={13} />
          </a>
        </div>

        {/* Date */}
        <div className="flex items-center gap-3 px-4 py-3.5">
          <div
            className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0"
            style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
          >
            <Calendar size={14} style={{ color: "var(--primary)" }} />
          </div>
          <div>
            <p className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>Date</p>
            <p className="text-[13.5px] font-semibold" style={{ color: "var(--foreground)" }}>
              {formatDate(event.startDate)}
            </p>
          </div>
        </div>

        {/* Attendance */}
        <div className="flex items-center gap-3 px-4 py-3.5">
          <div
            className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0"
            style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
          >
            <Users size={14} style={{ color: "var(--primary)" }} />
          </div>
          <div>
            <p className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>Attendance</p>
            <p className="text-[13.5px] font-semibold" style={{ color: "var(--foreground)" }}>
              {event.rsvpCount} alumni going
              {event.capacity ? ` · ${event.capacity - event.rsvpCount} spots remaining` : ""}
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
