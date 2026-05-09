"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { Calendar, MapPin, Users, Ticket, ArrowLeft, Loader2, PlayCircle, Image as ImageIcon, CheckCircle2, Navigation, ChevronRight } from "lucide-react";
import Link from "next/link";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { MediaGallery } from "@/components/ui/media-gallery";
import { formatCurrency, formatDate } from "@/lib/utils";
import { getEventById, rsvpEvent, cancelRsvp, getMyRsvps } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { EmptyState } from "@/components/ui/empty-state";

export default function EventDetailPage() {
  const { id } = useParams() as { id: string };
  const router = useRouter();
  const qc = useQueryClient();
  const [rsvpConfirmOpen, setRsvpConfirmOpen] = useState(false);

  const { data: event, isLoading: isLoadingEvent } = useQuery({
    queryKey: ["event", id],
    queryFn: () => getEventById(id),
  });

  const { data: myRsvps } = useQuery({
    queryKey: ["m-rsvps"],
    queryFn: () => getMyRsvps(),
  });

  const rsvpMut = useMutation({
    mutationFn: () => rsvpEvent(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-rsvps"] });
      qc.invalidateQueries({ queryKey: ["event", id] });
      setRsvpConfirmOpen(false);
      toast.success("RSVP confirmed!");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const cancelMut = useMutation({
    mutationFn: () => cancelRsvp(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["m-rsvps"] });
      qc.invalidateQueries({ queryKey: ["event", id] });
      toast.success("RSVP cancelled");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (isLoadingEvent) {
    return (
      <div className="p-8 lg:p-12 max-w-5xl mx-auto space-y-8 animate-pulse">
        <div className="h-8 w-32 bg-muted rounded-lg" />
        <div className="aspect-video w-full bg-muted rounded-2xl" />
        <div className="space-y-4">
          <div className="h-10 w-2/3 bg-muted rounded-lg" />
          <div className="h-4 w-full bg-muted rounded-lg" />
          <div className="h-4 w-5/6 bg-muted rounded-lg" />
        </div>
      </div>
    );
  }

  if (!event) return (
    <div className="p-8 lg:p-12 max-w-5xl mx-auto">
      <Button variant="ghost" size="sm" className="mb-6 font-bold" onClick={() => router.push("/events")}>
        <ArrowLeft size={16} className="mr-2" /> Back to Events
      </Button>
      <EmptyState icon={<Calendar size={48} />} title="Event not found" description="This event may have been removed or the link is incorrect." />
    </div>
  );

  const mapUrl = event.googleLocationUrl?.trim() ? event.googleLocationUrl.trim() : `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(event.venue)}`;

  const hasRsvp = (myRsvps ?? []).some(r => r.eventId === id && r.status === "Confirmed");
  const canRsvp = event.status === "Upcoming" || event.status === "Ongoing";
  const isFull = event.capacity ? event.rsvpCount >= event.capacity : false;
  
  const statusVariant: Record<string, "info" | "success" | "secondary" | "destructive"> = {
    Upcoming: "info",
    Ongoing: "success",
    Completed: "secondary",
    Cancelled: "destructive",
  };

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-6 sm:space-y-8 lg:space-y-12 pb-24 selection:bg-primary/20">
      {/* Breadcrumb + Navigation */}
      <nav className="flex items-center justify-between animate-in fade-in slide-in-from-top-4 duration-500">
        <div className="flex items-center gap-1.5 text-sm">
          <Button variant="ghost" size="sm" className="h-8 px-2 rounded-lg hover:bg-black/5 dark:hover:bg-white/5 font-bold group" onClick={() => router.push("/events")}>
            <ArrowLeft size={15} className="mr-1 group-hover:-translate-x-0.5 transition-transform" />
            Events
          </Button>
          <ChevronRight size={14} className="text-muted-foreground/50" />
          <span className="text-[13px] font-semibold text-foreground/70 truncate max-w-[200px] sm:max-w-xs">{event.title}</span>
        </div>
        <Badge variant={statusVariant[event.status] || "secondary"} className="h-7 px-3 font-black uppercase tracking-widest text-[10px]">
          {event.status}
        </Badge>
      </nav>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 sm:gap-8 lg:gap-12">
        {/* Main Content Area */}
        <div className="lg:col-span-8 space-y-10">
          <header className="space-y-4 animate-in fade-in slide-in-from-bottom-4 duration-700">
            <h1 className="text-2xl sm:text-3xl lg:text-4xl xl:text-5xl font-black tracking-tight text-foreground leading-[1.1]">{event.title}</h1>
            <div className="flex flex-wrap items-center gap-3 sm:gap-6 text-sm font-bold text-muted-foreground">
              <div className="flex items-center gap-2">
                <Calendar size={18} className="text-primary" />
                {formatDate(event.startDate)}
              </div>
              <div className="flex items-center gap-2">
                <MapPin size={18} className="text-primary" />
                {event.venue}
              </div>
              <div className="flex items-center gap-2">
                <Users size={18} className="text-primary" />
                {event.rsvpCount} Participating
              </div>
            </div>
          </header>

          {/* Featured Visual */}
          <section className="animate-in fade-in zoom-in-95 duration-1000 delay-200">
            {event.bannerImageUrl ? (
              <div className="rounded-[2rem] overflow-hidden shadow-2xl border-4 border-white dark:border-white/5 ring-1 ring-black/5 ring-offset-4 ring-offset-background aspect-video relative group">
                <img src={event.bannerImageUrl} alt={event.title} className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-1000" />
                <div className="absolute inset-0 bg-gradient-to-t from-black/20 to-transparent" />
              </div>
            ) : (
              <div className="rounded-[2rem] bg-gradient-to-br from-primary/10 to-muted/20 aspect-video flex items-center justify-center border-4 border-white dark:border-white/5 ring-1 ring-black/5">
                <ImageIcon size={64} className="text-primary/20" />
              </div>
            )}
          </section>

          {/* Description & Details */}
          <section className="space-y-6 prose prose-lg dark:prose-invert max-w-none animate-in fade-in slide-in-from-bottom-6 duration-700 delay-300">
            <h2 className="text-2xl font-black tracking-tight border-b border-border/40 pb-4">Event details</h2>
            <div className="whitespace-pre-wrap font-medium text-muted-foreground leading-relaxed text-lg">
              {event.description || "Join us for this special UMaT Alumni gathering. More details will be shared as we get closer to the date."}
            </div>
          </section>

          {/* Media Links / Extras */}
          {((event.imageUrls && event.imageUrls.length > 0) || (event.youtubeVideoUrls && event.youtubeVideoUrls.length > 0)) && (
            <section className="animate-in fade-in slide-in-from-bottom-8 duration-700 delay-500">
              <h3 className="text-xl font-bold mb-6 flex items-center gap-2">
                <PlayCircle size={20} className="text-primary" />
                Event Gallery & Media
              </h3>
              <div className="rounded-[2rem] overflow-hidden border border-border/40 bg-muted/5 p-6 sm:p-8">
                <MediaGallery imageUrls={event.imageUrls} youtubeUrls={event.youtubeVideoUrls} />
              </div>
            </section>
          )}
        </div>

        {/* Sidebar: RSVP & Meta Action */}
        <aside className="lg:col-span-4 space-y-8 animate-in fade-in slide-in-from-right-8 duration-1000 delay-300">
          <Card className="sticky top-24 overflow-hidden border-border/40 shadow-2xl shadow-primary/5 bg-card/80 backdrop-blur-xl">
            <div className="absolute top-0 left-0 right-0 h-2 bg-primary" />
            <CardContent className="p-5 sm:p-8 lg:p-10 space-y-6 sm:space-y-8">
              <div className="space-y-6">
                <div>
                  <p className="text-[11px] font-bold text-muted-foreground uppercase tracking-[0.2em] mb-2">Registration</p>
                  <div className="flex items-center justify-between">
                    <p className="text-3xl font-black tracking-tight text-foreground">
                      {event.isTicketed && event.ticketPrice ? formatCurrency(event.ticketPrice) : "Free Access"}
                    </p>
                    {event.isTicketed && <Ticket size={24} className="text-primary" />}
                  </div>
                </div>

                <div className="space-y-4 pt-4 border-t border-border/40">
                  {hasRsvp && (
                    <div className="flex items-center gap-3 p-4 rounded-2xl bg-primary/5 border border-primary/20 animate-in zoom-in-95 duration-500">
                      <div className="w-10 h-10 rounded-xl bg-primary/10 flex items-center justify-center text-primary shrink-0">
                        <CheckCircle2 size={24} />
                      </div>
                      <div>
                        <p className="text-[10px] font-bold text-primary uppercase tracking-widest leading-none mb-1">Status</p>
                        <p className="text-sm font-black text-primary/80">You&apos;re participating</p>
                      </div>
                    </div>
                  )}

                  {!canRsvp ? (
                    <div className="p-6 rounded-2xl bg-muted/50 border border-border/40 text-center space-y-2">
                        <p className="text-sm font-bold text-muted-foreground">Registration Closed</p>
                        <p className="text-[11px] font-medium text-muted-foreground/60 uppercase tracking-widest">This event has already taken place or was cancelled.</p>
                    </div>
                  ) : hasRsvp ? (
                    <Button 
                      variant="outline"
                      className="w-full h-16 rounded-2xl font-black text-lg border-destructive/20 text-destructive hover:bg-destructive hover:text-white transition-all hover:scale-[1.02] active:scale-[0.98] group/btn" 
                      disabled={cancelMut.isPending}
                      onClick={() => cancelMut.mutate()}
                    >
                      {cancelMut.isPending ? (
                        <Loader2 size={24} className="animate-spin" />
                      ) : (
                        "Cancel Participation"
                      )}
                    </Button>
                  ) : (
                    <>
                      <Button 
                        className="w-full h-16 rounded-2xl font-black text-lg shadow-2xl shadow-primary/20 hover:shadow-primary/40 transition-all hover:scale-[1.02] active:scale-[0.98] group/btn" 
                        disabled={rsvpMut.isPending || isFull}
                        onClick={() => {
                          if (isFull) return;
                          setRsvpConfirmOpen(true);
                        }}
                      >
                        {rsvpMut.isPending ? (
                          <Loader2 size={24} className="animate-spin" />
                        ) : (
                          <>
                            <Calendar size={22} className="mr-3 group-hover/btn:rotate-12 transition-transform" />
                            {isFull ? "Event fully booked" : "Confirm RSVP"}
                          </>
                        )}
                      </Button>
                    </>
                  )}
                </div>
              </div>

              <div className="space-y-4 pt-4">
                <div className="flex items-center gap-3 p-4 rounded-2xl bg-muted/20 border border-border/40 relative group/loc overflow-hidden">
                  <div className="absolute inset-0 bg-primary/5 translate-y-full group-hover/loc:translate-y-0 transition-transform duration-500" />
                  <div className="w-10 h-10 rounded-xl bg-primary/10 flex items-center justify-center text-primary shrink-0 transition-transform group-hover/loc:scale-110 z-10">
                    <MapPin size={20} />
                  </div>
                  <div className="z-10">
                    <p className="text-[10px] font-bold text-muted-foreground uppercase tracking-widest leading-none mb-1">Venue</p>
                    <p className="text-sm font-black truncate max-w-[160px]">{event.venue}</p>
                  </div>
                  <a 
                    href={mapUrl}
                    target="_blank" 
                    rel="noopener noreferrer"
                    className="ml-auto w-8 h-8 rounded-lg bg-background border border-border/40 flex items-center justify-center hover:bg-primary hover:text-white transition-all z-10"
                  >
                    <Navigation size={14} />
                  </a>
                </div>
                
                <div className="flex items-center gap-3 p-4 rounded-2xl bg-muted/20 border border-border/40">
                  <div className="w-10 h-10 rounded-xl bg-primary/10 flex items-center justify-center text-primary shrink-0">
                    <Users size={20} />
                  </div>
                  <div>
                    <p className="text-[10px] font-bold text-muted-foreground uppercase tracking-widest leading-none mb-1">Attendance</p>
                    <p className="text-sm font-black">{event.rsvpCount} Alumni</p>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </aside>
      </div>

      <ConfirmModal
        open={rsvpConfirmOpen}
        title="Confirm RSVP"
        message="Reserve your spot for this event?"
        confirmLabel="Yes, RSVP"
        variant="default"
        isLoading={rsvpMut.isPending}
        onConfirm={() => rsvpMut.mutate()}
        onCancel={() => setRsvpConfirmOpen(false)}
      />
    </div>
  );
}
