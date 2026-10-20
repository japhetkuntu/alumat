"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { RichText } from "@alumni/ui";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import {
  Calendar, MapPin, Users, Ticket, ArrowLeft,
  Loader2, CheckCircle2, Navigation, ExternalLink,
} from "@alumni/ui";
import { toast } from "sonner";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { ShareLinkButton, shareMessages } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { MediaGallery, ZoomableImage } from "@alumni/ui";
import { formatCurrency, formatDate } from "@alumni/ui";
import { getEventById, rsvpEvent, cancelRsvp, getMyRsvps, buyEventTicket } from "@/lib/member-api";
import { SourceBadge } from "@/components/member/source-badge";
import { EventUpdates } from "@/components/member/event-updates";
import { handleApiError } from "@/lib/api-client";
import { EmptyState } from "@alumni/ui";
import type { AlumniEvent, EventRegistration } from "@/types";

type EventPlaceLink = { href: string; kind: "map" | "online" | "link"; label: string };

const MAP_HOSTS = /(^|\.)(google\.[a-z.]+\/maps|maps\.google\.|maps\.app\.goo\.gl|goo\.gl\/maps|maps\.apple\.com|openstreetmap\.org|waze\.com|bing\.com\/maps)/i;
const ONLINE_HOSTS = /(^|\.)(zoom\.us|zoom\.com|meet\.google\.com|teams\.microsoft\.com|teams\.live\.com|webex\.com|whereby\.com|skype\.com|gotomeeting\.com|bluejeans\.com|discord\.(gg|com)|meet\.jit\.si|jitsi\.org|youtube\.com|youtu\.be|facebook\.com|fb\.watch|linkedin\.com|twitch\.tv|crowdcast\.io|streamyard\.com|chime\.aws|whatsapp\.com|t\.me)/i;

/** The link an organiser put on an event, and what to call it. Null when there is none, or when it isn't a web address. */
function eventPlaceLink(raw?: string | null): EventPlaceLink | null {
  const value = raw?.trim();
  if (!value) return null;
  let url: URL;
  try { url = new URL(value); } catch { return null; }
  if (url.protocol !== "http:" && url.protocol !== "https:") return null;
  const hostAndPath = url.hostname + url.pathname;
  if (MAP_HOSTS.test(hostAndPath)) return { href: url.toString(), kind: "map", label: "Open in maps" };
  if (ONLINE_HOSTS.test(url.hostname)) return { href: url.toString(), kind: "online", label: "Join online" };
  return { href: url.toString(), kind: "link", label: "Open event link" };
}

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
  const [cancelConfirmOpen, setCancelConfirmOpen] = useState(false);
  const [buyConfirmOpen, setBuyConfirmOpen] = useState(false);

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
      qc.invalidateQueries({ queryKey: ["event-updates", id] });
      qc.invalidateQueries({ queryKey: ["event", id] });
      // rsvpCount is also shown on the events list cards.
      qc.invalidateQueries({ queryKey: ["m-events-list"] });
      // The Calendar page reads its own separate RSVP/events queries.
      qc.invalidateQueries({ queryKey: ["cal-rsvps"] });
      qc.invalidateQueries({ queryKey: ["cal-events"] });
      setRsvpConfirmOpen(false);
      toast.success("You're in! We'll see you there.", { description: "Find it under \"Going\" in Events and in your calendar." });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  // Paying happens on Paystack's page; we come back to the ticket-status page, which confirms the seat.
  const buyMut = useMutation({
    mutationFn: () => buyEventTicket(id, `${window.location.origin}/events/ticket-status`),
    onSuccess: (checkout) => {
      if (!checkout.authorizationUrl) { toast.error("We couldn't open the payment page. Please try again."); return; }
      window.location.href = checkout.authorizationUrl;
    },
    onError: (e) => { setBuyConfirmOpen(false); toast.error(handleApiError(e)); },
  });

  const cancelMut = useMutation({
    mutationFn: () => cancelRsvp(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-rsvps"] });
      qc.invalidateQueries({ queryKey: ["event-updates", id] });
      qc.invalidateQueries({ queryKey: ["event", id] });
      qc.invalidateQueries({ queryKey: ["m-events-list"] });
      qc.invalidateQueries({ queryKey: ["cal-rsvps"] });
      qc.invalidateQueries({ queryKey: ["cal-events"] });
      setCancelConfirmOpen(false);
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

  const myRsvp   = (myRsvps ?? []).find(r => r.eventId === id && r.status === "Confirmed");
  const hasRsvp  = !!myRsvp;
  const canRsvp  = event.status === "Upcoming" || event.status === "Ongoing";
  const isFull   = event.capacity ? event.rsvpCount >= event.capacity : false;
  // Only what the organiser added: nothing is invented from the venue name. It may be a map or, for an online event, a meeting link.
  const place    = eventPlaceLink(event.googleLocationUrl);

  const hasMedia = (event.imageUrls?.length ?? 0) > 0 || (event.youtubeVideoUrls?.length ?? 0) > 0;
  const shareUrl = typeof window !== "undefined" ? window.location.href : undefined;

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto pb-20">

      {/* ── Nav row ── */}
      <div className="flex flex-wrap items-center justify-between mb-6 sm:mb-8 gap-x-3 gap-y-2">
        <button
          onClick={() => router.push("/events")}
          className="flex shrink-0 items-center gap-1.5 whitespace-nowrap text-[13.5px] font-semibold transition-colors hover:underline"
          style={{ color: "var(--muted-foreground)" }}
        >
          <ArrowLeft size={15} /> Back to events
        </button>
        <div className="flex flex-wrap items-center gap-2">
          <ShareLinkButton
            url={shareUrl}
            title={event.title}
            variant="outline"
            message={shareMessages.event({ title: event.title, startDate: event.startDate, venue: event.venue, description: event.description })}
            size="sm"
            onSuccess={(result) => toast.success(result === "shared" ? "Share sheet opened" : "Event link copied")}
            onError={(message) => toast.error(message)}
          />
          <Badge
            variant={statusVariant[event.status] ?? "secondary"}
            className="text-[12px] font-semibold uppercase tracking-wide"
          >
            {event.status}
          </Badge>
        </div>
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
            <SourceBadge communityId={event.communityId} communityName={event.communityName} yearGroups={event.yearGroups} className="mb-3" />
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

          {/* Banner: only when the organiser uploaded one. No photo means no photo. */}
          {event.bannerImageUrl && (
            <div className="rounded-2xl overflow-hidden border" style={{ borderColor: "var(--border)" }}>
              <ZoomableImage
                src={event.bannerImageUrl}
                alt={event.title}
                className="w-full object-cover"
                style={{ maxHeight: 420 }}
              />
            </div>
          )}

          {/* RSVP block — mobile only */}
          <div className="lg:hidden">
            <RsvpBlock
              event={event}
              hasRsvp={hasRsvp}
              canRsvp={canRsvp}
              isFull={isFull}
              place={place}
              ticket={myRsvp}
              onRsvp={() => (event.isTicketed ? setBuyConfirmOpen(true) : setRsvpConfirmOpen(true))}
              onCancel={() => setCancelConfirmOpen(true)}
              cancelPending={cancelMut.isPending}
              rsvpPending={rsvpMut.isPending || buyMut.isPending}
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
            {event.description ? (
              <RichText className="text-[15.4px]" content={event.description} />
            ) : (
              <p className="leading-[1.85]" style={{ fontSize: "0.9625rem", color: "var(--muted-foreground)" }}>More details will be shared closer to the date. RSVP to stay informed.</p>
            )}
          </div>

          {/* What the organiser has posted for the people going */}
          <EventUpdates
            eventId={id}
            joinLabel="RSVP to read them"
            onJoin={canRsvp && !isFull && !event.registrationClosed ? () => (event.isTicketed ? setBuyConfirmOpen(true) : setRsvpConfirmOpen(true)) : undefined}
          />

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
                place={place}
                ticket={myRsvp}
                onRsvp={() => (event.isTicketed ? setBuyConfirmOpen(true) : setRsvpConfirmOpen(true))}
                onCancel={() => setCancelConfirmOpen(true)}
                cancelPending={cancelMut.isPending}
                rsvpPending={rsvpMut.isPending || buyMut.isPending}
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

      <ConfirmModal
        open={buyConfirmOpen}
        title="Confirm your RSVP"
        message={`RSVP for "${event.title}": ${event.ticketPrice ? formatCurrency(event.ticketPrice) : ""}. Once you pay, your RSVP can't be cancelled and isn't refunded.`}
        confirmLabel="Continue to payment"
        cancelLabel="Not now"
        variant="default"
        isLoading={buyMut.isPending}
        onConfirm={() => buyMut.mutate()}
        onCancel={() => setBuyConfirmOpen(false)}
      />

      <ConfirmModal
        open={cancelConfirmOpen}
        title="Cancel your RSVP?"
        message={`You will give up your place at "${event.title}". You can RSVP again later if there is still room.`}
        confirmLabel="Yes, cancel RSVP"
        cancelLabel="Keep my spot"
        emphasis="cancel"
        isLoading={cancelMut.isPending}
        onConfirm={() => cancelMut.mutate()}
        onCancel={() => setCancelConfirmOpen(false)}
      />
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   RSVP BLOCK — shared between mobile (inline) and desktop (sidebar)
   ───────────────────────────────────────────────────────────────────────── */
function RsvpBlock({
  event, hasRsvp, canRsvp, isFull, place, ticket,
  onRsvp, onCancel, cancelPending, rsvpPending,
}: {
  event: AlumniEvent;
  /** The member's own confirmed RSVP or ticket for this event, if any. */
  ticket?: EventRegistration;
  hasRsvp: boolean;
  canRsvp: boolean;
  isFull: boolean;
  place: EventPlaceLink | null;
  onRsvp: () => void;
  onCancel: () => void;
  cancelPending: boolean;
  rsvpPending: boolean;
}) {
  const paid = event.isTicketed;
  return (
    <div className="space-y-5">

      {/* RSVP / price */}
      <div className="flex items-center justify-between">
        <div>
          <p className="text-[12px] font-semibold uppercase tracking-wide mb-0.5" style={{ color: "var(--muted-foreground)" }}>
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

      {/* The terms, shown before paying so nobody is surprised afterwards */}
      {paid && !hasRsvp && canRsvp && !event.registrationClosed && (
        <div className="p-3.5 rounded-xl space-y-1" style={{ background: "var(--secondary)", border: "1px solid var(--border)" }}>
          <p className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>Paid RSVPs are final</p>
          <p className="text-[12.5px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
            A paid RSVP can&apos;t be cancelled and isn&apos;t refunded.
          </p>
        </div>
      )}

      {/* RSVP status — if already going */}
      {hasRsvp && paid && (
        <div
          className="p-3.5 rounded-xl space-y-1"
          style={{ background: "rgba(34,197,94,0.08)", border: "1px solid rgba(34,197,94,0.2)" }}
        >
          <div className="flex items-center gap-3">
            <CheckCircle2 size={18} className="text-success shrink-0" />
            <p className="text-[13.5px] font-semibold text-success">Your RSVP is confirmed.</p>
          </div>
          <p className="text-[12.5px] leading-relaxed pl-[30px]" style={{ color: "var(--muted-foreground)" }}>
            {ticket?.amountPaid ? `Paid ${formatCurrency(ticket.amountPaid)}${ticket.paidAt ? ` on ${formatDate(ticket.paidAt)}` : ""}. ` : ""}
            Paid RSVPs can&apos;t be cancelled or refunded.
          </p>
        </div>
      )}
      {hasRsvp && !paid && (
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
        hasRsvp && paid ? null : hasRsvp ? (
          <Button
            variant="outline"
            className="w-full font-semibold text-[14px] gap-2"
            style={{ height: 44, borderColor: "var(--destructive)", color: "var(--destructive)" }}
            disabled={cancelPending}
            onClick={onCancel}
          >
            {cancelPending ? <Loader2 size={15} className="animate-spin" /> : "Cancel my RSVP"}
          </Button>
        ) : event.registrationClosed ? (
          <div className="p-4 rounded-xl text-center" style={{ background: "var(--secondary)", border: "1px solid var(--border)" }}>
            <p className="text-[14px] font-semibold mb-0.5" style={{ color: "var(--foreground)" }}>
              RSVPs are closed
            </p>
            <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>
              The organiser isn&apos;t taking new RSVPs for this event. Anyone already going keeps their place.
            </p>
          </div>
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
            ) : paid ? (
              <><Ticket size={16} /> RSVP{event.ticketPrice ? ` · ${formatCurrency(event.ticketPrice)}` : ""}</>
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
            style={{ background: "var(--card)", border: "1px solid var(--border-emphasis, var(--border))" }}
          >
            <MapPin size={14} style={{ color: "var(--primary)" }} />
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>Venue</p>
            <p className="text-[13.5px] font-semibold truncate" style={{ color: "var(--foreground)" }}>
              {event.venue}
            </p>
          </div>
          {place && (
            <a
              href={place.href}
              target="_blank"
              rel="noopener noreferrer"
              className="shrink-0 w-8 h-8 rounded-lg flex items-center justify-center transition-colors hover:bg-primary hover:text-white focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary"
              style={{ background: "var(--secondary)", color: "var(--muted-foreground)" }}
              title={place.label}
              aria-label={place.label}
            >
              {place.kind === "map" ? <Navigation size={13} /> : <ExternalLink size={13} />}
            </a>
          )}
        </div>

        {/* Date */}
        <div className="flex items-center gap-3 px-4 py-3.5">
          <div
            className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0"
            style={{ background: "var(--card)", border: "1px solid var(--border-emphasis, var(--border))" }}
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
            style={{ background: "var(--card)", border: "1px solid var(--border-emphasis, var(--border))" }}
          >
            <Users size={14} style={{ color: "var(--primary)" }} />
          </div>
          <div>
            <p className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>Attendance</p>
            <p className="text-[13.5px] font-semibold" style={{ color: "var(--foreground)" }}>
              {event.rsvpCount} going
              {event.capacity ? ` · ${event.capacity - event.rsvpCount} spots remaining` : ""}
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
