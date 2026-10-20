"use client";

import Link from "next/link";
import type { CSSProperties, ReactNode } from "react";
import { MapPin, Users, Clock, CheckCircle2, Loader2, ArrowRight, Badge, Button, formatDate, formatCurrency, cn } from "@alumni/ui";
import { SourceBadge } from "@/components/member/source-badge";
import type { AlumniEvent, EventStatus } from "@/types";

const statusVariant: Record<EventStatus, "info" | "success" | "secondary" | "destructive"> = {
  Upcoming: "info", Ongoing: "success", Completed: "secondary", Cancelled: "destructive",
};

function DetailLink({ children, className, style, href }: { children: ReactNode; className?: string; style?: CSSProperties; href?: string }) {
  return href ? <Link href={href} className={className} style={style}>{children}</Link>
    : <div className={className} style={style}>{children}</div>;
}

export function MemberEventCard({ event: e, hasRsvp, isPast, canRsvp, isFull, isPending, onRsvp, detailHref }: {
  event: AlumniEvent; hasRsvp: boolean; isPast: boolean; canRsvp: boolean; isFull: boolean;
  isPending: boolean; onRsvp: () => void; detailHref?: string;
}) {
  return (
              <div
                className={cn(
                  "rounded-2xl border overflow-hidden flex flex-col transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md",
                  (e.status === "Cancelled" || isPast) && "opacity-60",
                )}
                style={{ borderColor: "var(--border)", background: "var(--background)" }}
              >
                {/* A photo is only a photo: text laid over a picture goes faint whenever the picture is bright or busy, so the status, date and price always sit in their own row. */}
                {e.bannerImageUrl && (
                  <DetailLink href={detailHref} className="block relative shrink-0 overflow-hidden" style={{ height: 180 }}>
                    <img
                      src={e.bannerImageUrl}
                      alt={e.title}
                      className="absolute inset-0 w-full h-full object-cover transition-transform duration-500 group-hover:scale-105"
                    />
                  </DetailLink>
                )}
                {/* One row across the card: what it is on the left, when and how much on the right, so no side is left empty. */}
                <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-2 px-4 pt-4">
                  <div className="flex flex-wrap items-center gap-1.5">
                    <Badge variant={statusVariant[e.status]} className="text-[12px] font-semibold uppercase tracking-wide">
                      {e.status}
                    </Badge>
                    {isPast && e.status !== "Completed" && e.status !== "Cancelled" && (
                      <Badge variant="secondary" className="text-[12px] font-semibold uppercase tracking-wide">
                        Past
                      </Badge>
                    )}
                    <SourceBadge communityId={e.communityId} communityName={e.communityName} yearGroups={e.yearGroups} />
                  </div>
                  <div className="flex items-center gap-2 text-[12px] font-semibold" style={{ color: "var(--muted-foreground)" }}>
                    <Clock size={12} />
                    <span>{formatDate(e.startDate)}</span>
                    <span aria-hidden>·</span>
                    <span>{e.isTicketed && e.ticketPrice ? formatCurrency(e.ticketPrice) : "Free"}</span>
                    {hasRsvp && (
                      <span className="ml-1 w-6 h-6 rounded-full flex items-center justify-center" style={{ background: "rgba(34,197,94,0.85)" }} aria-label="You're going">
                        <CheckCircle2 size={13} color="white" />
                      </span>
                    )}
                  </div>
                </div>

                {/* Body */}
                <div className="flex flex-col flex-1 p-4 pt-3 gap-3">
                  <DetailLink href={detailHref}>
                    <h3
                      className="text-[15px] font-semibold leading-snug line-clamp-2 transition-colors duration-200 hover:text-primary"
                      style={{ color: "var(--foreground)" }}
                    >
                      {e.title}
                    </h3>
                  </DetailLink>

                  {/* Place and turnout share a line (they wrap on a narrow card), so the card reads across instead of in a short stack down the left. */}
                  <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5">
                    <div className="flex min-w-0 items-center gap-1.5 text-[13px]" style={{ color: "var(--muted-foreground)" }}>
                      <MapPin size={12} className="shrink-0" style={{ color: "var(--primary)" }} />
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
                    {canRsvp && e.isTicketed && hasRsvp ? (
                      <span
                        className="flex-1 inline-flex items-center justify-center gap-1.5 py-2 rounded-lg text-[12px] font-semibold"
                        style={{ background: "rgba(34,197,94,0.1)", color: "var(--success, #16a34a)" }}
                      >
                        <CheckCircle2 size={13} /> RSVP confirmed
                      </span>
                    ) : canRsvp && e.registrationClosed && !hasRsvp ? (
                      <span
                        className="flex-1 text-center py-2 rounded-lg text-[12px] font-semibold"
                        style={{ background: "var(--secondary)", color: "var(--muted-foreground)" }}
                      >
                        RSVPs closed
                      </span>
                    ) : canRsvp ? (
                      <Button
                        size="sm"
                        variant={hasRsvp ? "outline" : "default"}
                        className={cn(
                          "flex-1 font-semibold text-[13px]",
                          hasRsvp && "border-destructive/40 text-destructive hover:bg-destructive hover:text-white hover:border-destructive",
                        )}
                        style={{ height: 38 }}
                        disabled={isPending || (isFull && !hasRsvp)}
                        onClick={onRsvp}
                        aria-label={`${hasRsvp ? "Cancel RSVP for" : "RSVP for"} ${e.title}`}
                      >
                        {isPending ? (
                          <Loader2 size={14} className="animate-spin" />
                        ) : hasRsvp ? (
                          "Cancel RSVP"
                        ) : isFull ? (
                          "Event full"
                        ) : (
                          e.isTicketed && e.ticketPrice ? `RSVP · ${formatCurrency(e.ticketPrice)}` : "RSVP"
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
                    {detailHref && <Link href={detailHref} aria-label={`View ${e.title}`}
                      className="shrink-0 rounded-xl flex items-center justify-center hover:bg-primary/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
                      style={{ width: 38, height: 38 }}><ArrowRight size={16} className="text-muted-foreground" /></Link>}
                  </div>
                </div>
              </div>
  );
}
