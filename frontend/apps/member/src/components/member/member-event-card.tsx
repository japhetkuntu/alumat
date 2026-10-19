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
                {/* With a photo, the status and date sit over it. With none there is no picture area at all: the same facts sit in a plain row. */}
                {e.bannerImageUrl ? (
                <DetailLink href={detailHref} className="block relative shrink-0 overflow-hidden" style={{ height: 180 }}>
                  <img
                    src={e.bannerImageUrl}
                    alt={e.title}
                    className="absolute inset-0 w-full h-full object-cover transition-transform duration-500 group-hover:scale-105"
                  />
                  {/* Scrim */}
                  <div className="absolute inset-0" style={{ background: "linear-gradient(to top, rgba(0,0,0,0.45) 0%, transparent 55%)" }} />

                  <div className="absolute top-3 left-3 flex items-center gap-1.5">
                    <Badge variant={statusVariant[e.status]} className="text-[12px] font-semibold uppercase tracking-wide">
                      {e.status}
                    </Badge>
                    {isPast && e.status !== "Completed" && e.status !== "Cancelled" && (
                      <Badge variant="secondary" className="text-[12px] font-semibold uppercase tracking-wide">
                        Past
                      </Badge>
                    )}
                  </div>

                  {hasRsvp && (
                    <div className="absolute top-3 right-3 w-7 h-7 rounded-full flex items-center justify-center"
                      style={{ background: "rgba(34,197,94,0.85)", backdropFilter: "blur(4px)" }}>
                      <CheckCircle2 size={14} color="white" />
                    </div>
                  )}

                  <div className="absolute bottom-3 left-3 flex items-center gap-1.5 text-white text-[12px] font-semibold">
                    <Clock size={12} />
                    {formatDate(e.startDate)}
                    {e.isTicketed && e.ticketPrice ? (
                      <span className="ml-2 text-white/80">· {formatCurrency(e.ticketPrice)}</span>
                    ) : (
                      <span className="ml-2 text-white/70">· Free</span>
                    )}
                  </div>
                </DetailLink>
                ) : (
                <div className="flex flex-col gap-2 px-4 pt-4">
                  <div className="flex items-center justify-between gap-2">
                    <div className="flex items-center gap-1.5">
                      <Badge variant={statusVariant[e.status]} className="text-[12px] font-semibold uppercase tracking-wide">
                        {e.status}
                      </Badge>
                      {isPast && e.status !== "Completed" && e.status !== "Cancelled" && (
                        <Badge variant="secondary" className="text-[12px] font-semibold uppercase tracking-wide">
                          Past
                        </Badge>
                      )}
                    </div>
                    {hasRsvp && (
                      <div className="w-7 h-7 rounded-full flex items-center justify-center" style={{ background: "rgba(34,197,94,0.85)" }} aria-label="You're going">
                        <CheckCircle2 size={14} color="white" />
                      </div>
                    )}
                  </div>
                  <div className="flex items-center gap-1.5 text-[12px] font-semibold" style={{ color: "var(--muted-foreground)" }}>
                    <Clock size={12} />
                    {formatDate(e.startDate)}
                    <span className="ml-2">· {e.isTicketed && e.ticketPrice ? formatCurrency(e.ticketPrice) : "Free"}</span>
                  </div>
                </div>
                )}

                {/* Body */}
                <div className={cn("flex flex-col flex-1 p-4 gap-3", !e.bannerImageUrl && "pt-3")}>
                  <SourceBadge communityId={e.communityId} communityName={e.communityName} yearGroups={e.yearGroups} className="self-start" />
                  <DetailLink href={detailHref}>
                    <h3
                      className="text-[15px] font-semibold leading-snug line-clamp-2 transition-colors duration-200 hover:text-primary"
                      style={{ color: "var(--foreground)" }}
                    >
                      {e.title}
                    </h3>
                  </DetailLink>

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
                    {canRsvp && e.isTicketed && hasRsvp ? (
                      <span
                        className="flex-1 inline-flex items-center justify-center gap-1.5 py-2 rounded-lg text-[12px] font-semibold"
                        style={{ background: "rgba(34,197,94,0.1)", color: "var(--success, #16a34a)" }}
                      >
                        <CheckCircle2 size={13} /> Ticket confirmed
                      </span>
                    ) : canRsvp && e.registrationClosed && !hasRsvp ? (
                      <span
                        className="flex-1 text-center py-2 rounded-lg text-[12px] font-semibold"
                        style={{ background: "var(--secondary)", color: "var(--muted-foreground)" }}
                      >
                        {e.isTicketed ? "Ticket sales closed" : "RSVPs closed"}
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
                        aria-label={`${hasRsvp ? "Cancel RSVP for" : e.isTicketed ? "Get a ticket for" : "RSVP for"} ${e.title}`}
                      >
                        {isPending ? (
                          <Loader2 size={14} className="animate-spin" />
                        ) : hasRsvp ? (
                          "Cancel RSVP"
                        ) : isFull ? (
                          "Event full"
                        ) : e.isTicketed ? (
                          "Get ticket"
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
                    {detailHref && <Link href={detailHref} aria-label={`View ${e.title}`}
                      className="shrink-0 rounded-xl flex items-center justify-center hover:bg-primary/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
                      style={{ width: 38, height: 38 }}><ArrowRight size={16} className="text-muted-foreground" /></Link>}
                  </div>
                </div>
              </div>
  );
}
