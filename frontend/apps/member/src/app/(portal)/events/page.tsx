"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Calendar, MapPin, Users, ArrowRight, Clock, CheckCircle2, Loader2 } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { formatDate, formatCurrency } from "@/lib/utils";
import { getEvents, rsvpEvent, cancelRsvp, getMyRsvps } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import Link from "next/link";
import type { EventStatus } from "@/types";

const statusVariant: Record<EventStatus, "info" | "success" | "secondary" | "destructive"> = {
  Upcoming: "info",
  Ongoing: "success",
  Completed: "secondary",
  Cancelled: "destructive",
};

export default function MemberEventsPage() {
  const [page, setPage] = useState(1);
  const pageSize = 12;
  const qc = useQueryClient();

  const { data: eventsData, isLoading } = useQuery({
    queryKey: ["m-events-list", page],
    queryFn: () => getEvents(page, pageSize),
    placeholderData: (prev) => prev,
  });

  const { data: myRsvps } = useQuery({
    queryKey: ["m-rsvps"],
    queryFn: () => getMyRsvps(),
  });

  const rsvpMut = useMutation({
    mutationFn: (eventId: string) => rsvpEvent(eventId),
    onSuccess: () => { 
      qc.invalidateQueries({ queryKey: ["m-rsvps"] }); 
      qc.invalidateQueries({ queryKey: ["m-events-list"] }); 
      toast.success("RSVP confirmed!"); 
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const cancelMut = useMutation({
    mutationFn: (eventId: string) => cancelRsvp(eventId),
    onSuccess: () => { 
      qc.invalidateQueries({ queryKey: ["m-rsvps"] }); 
      qc.invalidateQueries({ queryKey: ["m-events-list"] }); 
      toast.success("RSVP cancelled"); 
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const events = eventsData?.results ?? [];
  const totalPages = eventsData?.totalPages ?? 1;
  const rsvpSet = new Set((myRsvps ?? []).filter((r) => r.status === "Confirmed").map((r) => r.eventId));

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-6 sm:space-y-8 lg:space-y-10 selection:bg-primary/20">
      <header className="space-y-2 animate-in fade-in slide-in-from-bottom-4 duration-700">
        <h1 className="text-2xl sm:text-3xl lg:text-4xl font-black tracking-tight text-foreground">Events</h1>
        <p className="text-muted-foreground text-sm sm:text-base lg:text-lg font-medium leading-relaxed max-w-2xl">
          Connect, celebrate, and grow with your fellow UMaT alumni. Discover upcoming gatherings and confirm your participation.
        </p>
      </header>

      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 sm:gap-6 lg:gap-8">
          {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : events.length === 0 ? (
        <EmptyState icon={<Calendar size={48} />} title="No events found" description="Check back soon for upcoming alumni events and reunions." />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 sm:gap-6 lg:gap-8">
          {events.map((e, i) => {
            const hasRsvp = rsvpSet.has(e.id);
            const canRsvp = e.status === "Upcoming" || e.status === "Ongoing";
            const isFull = e.capacity ? e.rsvpCount >= e.capacity : false;
            
            return (
              <Card 
                key={e.id} 
                className={`group relative flex flex-col overflow-hidden border-border/40 hover:border-primary/30 transition-all duration-500 hover:shadow-2xl hover:shadow-primary/5 animate-in fade-in slide-in-from-bottom-6 duration-700 ${e.status === "Cancelled" ? "opacity-60" : ""}`}
                style={{ animationDelay: `${i * 100}ms` }}
              >
                {/* Image Section */}
                <Link href={`/events/${e.id}`} className="relative h-56 overflow-hidden block">
                  {e.bannerImageUrl ? (
                    <img src={e.bannerImageUrl} alt={e.title} className="w-full h-full object-cover transition-transform duration-1000 group-hover:scale-110" />
                  ) : (
                    <div className="w-full h-full bg-gradient-to-br from-primary/20 via-primary/5 to-muted/20 flex items-center justify-center">
                      <Calendar size={48} className="text-primary/10" />
                    </div>
                  )}
                  <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-black/20 to-transparent" />
                  <div className="absolute top-4 left-4 flex gap-2">
                    <Badge variant={statusVariant[e.status]} className="font-bold uppercase tracking-widest text-[9px] translate-y-0 opacity-100 transition-all group-hover:translate-x-1">
                      {e.status}
                    </Badge>
                  </div>
                  {hasRsvp && (
                    <div className="absolute top-4 right-4 animate-in zoom-in duration-300">
                      <div className="w-8 h-8 rounded-full bg-white/10 backdrop-blur-md border border-white/20 flex items-center justify-center text-white">
                        <CheckCircle2 size={16} />
                      </div>
                    </div>
                  )}
                </Link>

                <CardContent className="flex-1 p-6 flex flex-col space-y-6">
                  <div className="space-y-2">
                    <div className="flex items-center justify-between text-[10px] font-black uppercase tracking-widest text-primary">
                      <div className="flex items-center gap-1.5">
                        <Clock size={12} />
                        {formatDate(e.startDate)}
                      </div>
                      {e.isTicketed && e.ticketPrice ? (
                        <span className="text-foreground/60">{formatCurrency(e.ticketPrice)}</span>
                      ) : (
                        <span className="text-foreground/40">Free</span>
                      )}
                    </div>
                    <Link href={`/events/${e.id}`}>
                      <h3 className="text-xl font-bold leading-tight group-hover:text-primary transition-colors line-clamp-2">{e.title}</h3>
                    </Link>
                  </div>

                  <div className="space-y-3 text-sm font-medium text-muted-foreground">
                    <div className="flex items-start gap-2.5">
                      <MapPin size={16} className="text-primary/60 shrink-0 mt-0.5" />
                      <span className="line-clamp-1">{e.venue}</span>
                    </div>
                    <div className="flex items-center gap-2.5">
                      <Users size={16} className="text-primary/60 shrink-0" />
                      <span>{e.rsvpCount} Attending{e.capacity ? ` / ${e.capacity}` : ""}</span>
                    </div>
                  </div>

                  <div className="pt-6 mt-auto flex items-center gap-3 border-t border-border/40">
                    {canRsvp ? (
                      <Button 
                        className={`flex-1 h-11 font-black shadow-lg transition-all ${hasRsvp ? "bg-muted text-foreground hover:bg-destructive hover:text-white" : "shadow-primary/10 hover:shadow-primary/20"}`}
                        size="sm"
                        disabled={rsvpMut.isPending || cancelMut.isPending || (isFull && !hasRsvp)}
                        onClick={(clickEvent) => {
                          clickEvent.preventDefault();
                          if (hasRsvp) {
                            cancelMut.mutate(e.id);
                          } else {
                            rsvpMut.mutate(e.id);
                          }
                        }}
                      >
                        {rsvpMut.isPending || cancelMut.isPending ? (
                          <Loader2 size={16} className="animate-spin" />
                        ) : hasRsvp ? (
                          "Participating"
                        ) : isFull ? (
                          "Full"
                        ) : (
                          "Confirm RSVP"
                        )}
                      </Button>
                    ) : (
                      <div className="flex-1 text-center py-2 bg-muted/30 rounded-lg text-[11px] font-bold text-muted-foreground uppercase tracking-widest leading-none">
                        Registration Closed
                      </div>
                    )}
                    <Link href={`/events/${e.id}`}>
                      <Button variant="ghost" size="icon" className="h-11 w-11 rounded-xl bg-muted/20 hover:bg-primary/5 hover:text-primary transition-all">
                        <ArrowRight size={18} />
                      </Button>
                    </Link>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}
      
      <div className="pt-8">
        <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
      </div>
    </div>
  );
}
