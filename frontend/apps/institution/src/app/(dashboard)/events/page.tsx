"use client";

import { ReadMore } from "@alumni/ui";
import { shareMessages } from "@alumni/ui";
import { richTextToPlain } from "@alumni/ui";
import { RichTextEditor } from "@alumni/ui";
import { LoadError } from "@alumni/ui";
import { ZoomableImage } from "@alumni/ui";
import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Calendar, MapPin, Users, Pencil, Ticket, Bell, Copy } from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import Link from "next/link";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { FormSheet, FormSection, CheckOption, DropdownMenu, DropdownMenuTrigger, DropdownMenuContent, DropdownMenuItem, MoreHorizontal } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { formatDate, formatCurrency, cn } from "@alumni/ui";
import { getEvents, createEvent, updateEvent, cancelEvent, deleteEvent, getCommunities, getEventTicketing, setEventRegistrationClosed } from "@/lib/institution-api";
import { ImageUpload } from "@alumni/ui";
import { MultiImageUpload } from "@alumni/ui";
import { YouTubePreview } from "@alumni/ui";
import { AudienceScopePicker, inferAudienceMode, type AudienceMode } from "@alumni/ui";
import { useInstitutionNavTheme } from "@/components/institution/institution-layout";
import { PublicOption } from "@/components/institution/public-option";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { useAuth } from "@/hooks/use-auth";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import type { AlumniEvent, EventStatus } from "@/types";
import { MemberShareButton } from "@/components/institution/member-share-button";

function isPastEvent(e: { startDate: string; endDate?: string | null }): boolean {
  return new Date(e.endDate ?? e.startDate).getTime() < Date.now();
}

const statusVariant: Record<EventStatus, "success" | "secondary" | "info" | "warning" | "destructive"> = {
  Upcoming: "info", Ongoing: "success", Completed: "secondary", Cancelled: "destructive",
};

const statusOptions = ["Upcoming", "Ongoing", "Completed", "Cancelled"];

interface FormState {
  title: string;
  description: string;
  startDate: string;
  endDate: string;
  venue: string;
  capacity: string;
  isTicketed: boolean;
  ticketPrice: string;
  /** Tickets already bought. Above zero the price is locked and the event stays paid. */
  paidTicketCount: number;
  googleLocationUrl: string;
  status: string;
  audienceMode: AudienceMode;
  isPublic: boolean;
  yearGroups: number[];
  bannerImage: File | null;
  existingBannerUrl: string;
  images: File[];
  existingImageUrls: string[];
  youtubeVideoUrls: string;
  communityId: string;
}

const emptyForm: FormState = {
  title: "", description: "", startDate: "", endDate: "", venue: "", capacity: "",
  isTicketed: false, ticketPrice: "", paidTicketCount: 0,
  googleLocationUrl: "", status: "Upcoming",
  audienceMode: "everyone", isPublic: false, yearGroups: [],
  bannerImage: null, existingBannerUrl: "", images: [], existingImageUrls: [], youtubeVideoUrls: "",
  communityId: "",
};

function commaToArray(s: string): string[] | undefined {
  const arr = s.split(",").map(x => x.trim()).filter(Boolean);
  return arr.length > 0 ? arr : undefined;
}

function arrayToComma(arr?: string[]): string {
  return arr?.join(", ") ?? "";
}

function EventForm({ id, init, onSave, showStatus, isSuperAdmin }: {
  id: string;
  init: FormState; onSave: (f: FormState) => void;
    showStatus: boolean;  isSuperAdmin: boolean;
}) {
  const [form, setForm] = useState(init);
  const f = <K extends keyof FormState>(k: K, v: FormState[K]) => setForm(prev => ({ ...prev, [k]: v }));
  const { data: communities = [] } = useQuery({ queryKey: ["communities"], queryFn: getCommunities });
  const { data: navTheme } = useInstitutionNavTheme();
  const isCommunity = navTheme?.organizationType === "Community";
  const { data: ticketing } = useQuery({ queryKey: ["event-ticketing"], queryFn: getEventTicketing });
  // Once tickets have sold the event stays a paid one (so no on/off choice), but its price can still change.
  const sold = form.paidTicketCount;
  const canCharge = !!ticketing?.paidEventsEnabled && !!ticketing?.payoutReady;
  return (
    <form id={id} className="space-y-5" onSubmit={(e) => { e.preventDefault(); onSave(form); }}>
          <FormSection title="The event">
            <div className="space-y-2"><Label>Event title</Label>
              <Input placeholder="e.g. Annual Gathering 2027" value={form.title} onChange={(e) => f("title", e.target.value)} required /></div>
            <div className="space-y-2"><Label>Description</Label>
              <RichTextEditor variant="compact" placeholder="What the event is, who should come, and what to bring." minHeight={140} value={form.description} onChange={(v) => f("description", v)} /></div>
          </FormSection>

          <FormSection title="When and where" hint="Members see the date, time and venue on the event and in their calendar.">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="space-y-2"><Label>Starts</Label>
                <Input type="datetime-local" value={form.startDate} onChange={(e) => f("startDate", e.target.value)} required /></div>
              <div className="space-y-2"><Label>Ends <span className="font-normal text-muted-foreground">(optional)</span></Label>
                <Input type="datetime-local" value={form.endDate} onChange={(e) => f("endDate", e.target.value)} /></div>
            </div>
            <div className="space-y-2"><Label>Venue</Label>
              <Input placeholder="e.g. Main Campus, Great Hall, or Online" value={form.venue} onChange={(e) => f("venue", e.target.value)} required /></div>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="space-y-2"><Label>Map or meeting link <span className="font-normal text-muted-foreground">(optional)</span></Label>
                <Input type="url" placeholder="Google Maps, Zoom, Meet, Teams…" value={form.googleLocationUrl} onChange={(e) => f("googleLocationUrl", e.target.value)} />
                <p className="text-[12.5px] text-muted-foreground">Members get a button beside the venue that opens it. Leave it empty and they get no button.</p></div>
              <div className="space-y-2"><Label>Capacity <span className="font-normal text-muted-foreground">(optional)</span></Label>
                <Input type="number" min={1} placeholder="No limit" value={form.capacity} onChange={(e) => f("capacity", e.target.value)} /></div>
            </div>
          </FormSection>

          <FormSection
            title="Paid RSVP"
            hint={form.isTicketed ? "Members pay when they RSVP." : "RSVPs are free unless you charge for them."}
          >
            {canCharge || form.isTicketed ? (
              <>
                {/* Before any sale it is a free choice; after, the event stays a paid one. */}
                {sold === 0 && (
                  <CheckOption
                    id="event-ticketed"
                    checked={form.isTicketed}
                    onChange={(v) => f("isTicketed", v)}
                    label="Charge to RSVP"
                    hint="Members pay online when they RSVP, instead of RSVPing for free."
                  />
                )}
                {form.isTicketed && (
                  <div className="space-y-4">
                    <div className="space-y-2 sm:max-w-[calc(50%-0.5rem)]"><Label>RSVP price (GH₵)</Label>
                      <Input type="number" min={1} step="0.01" placeholder="e.g. 150" value={form.ticketPrice}
                        onChange={(e) => f("ticketPrice", e.target.value)} required />
                      {sold > 0 && (
                        <p className="text-[12.5px] leading-relaxed text-muted-foreground">
                          {sold} paid {sold === 1 ? "RSVP" : "RSVPs"} already. {sold === 1 ? "That person keeps" : "Those people keep"} what they paid; a new price applies to anyone who RSVPs from now on.
                        </p>
                      )}
                    </div>
                    <div className="space-y-1.5 border border-border bg-muted/40 p-3 text-[13px] leading-relaxed">
                      <p className="font-semibold text-foreground">Good to know</p>
                      <ul className="list-disc space-y-1 pl-4 text-muted-foreground">
                        <li><span className="text-foreground">Paid RSVPs are final.</span> Attendees can&apos;t cancel and nobody is refunded. A paid event can&apos;t be cancelled either, so no money ever has to go back.</li>
                        <li>You can change the price whenever you like. People who already paid keep what they paid, and new RSVPs pay the new price.</li>
                        <li>A paid event can&apos;t be deleted once someone has paid to RSVP, or made free. You can still change its date, venue and details, and attendees are told.</li>
                        <li>You receive the full RSVP price in your payout account.</li>
                      </ul>
                    </div>
                  </div>
                )}
              </>
            ) : (
              <p className="border border-border bg-muted/40 p-3 text-[13px] leading-relaxed text-muted-foreground">
                {ticketing && !ticketing.paidEventsEnabled
                  ? "Charging to RSVP isn't turned on for your community yet. Contact AlumUnion support to turn it on."
                  : ticketing && !ticketing.payoutReady
                    ? "Charging to RSVP is turned on, but your payout account isn't set up yet, so the money has nowhere to go. Contact AlumUnion support to set it up, then you can create paid events."
                    : "Checking whether you can charge to RSVP…"}
              </p>
            )}
          </FormSection>

          <FormSection title="Who it is for">
            <AudienceScopePicker
              mode={form.audienceMode}
              onModeChange={(mode) => f("audienceMode", mode)}
              communityId={form.communityId}
              onCommunityChange={(v) => f("communityId", v)}
              communities={communities}
              yearGroups={form.yearGroups}
              onYearGroupsChange={(years) => setForm((prev) => ({ ...prev, yearGroups: years }))}
              supportsCommunity
              hideYearGroups={isCommunity}
              restricted={!isSuperAdmin ? { reason: "Regular admins cannot choose an audience. This event will be restricted to your assigned year group or community." } : undefined}
            />
            <PublicOption id="event-public" checked={form.isPublic} onChange={(v) => f("isPublic", v)} forEveryone={isSuperAdmin && form.audienceMode === "everyone"} noun="event" />
            {showStatus && (
              <div className="space-y-2 sm:max-w-[calc(50%-0.5rem)]"><Label>Status</Label>
                <FormSelect className="w-full" value={form.status} onValueChange={(v) => f("status", v)}
                  options={statusOptions.filter(s => !(form.isTicketed && s === "Cancelled")).map(s => ({ value: s, label: s }))} /></div>
            )}
          </FormSection>

          <FormSection title="Photos and video" hint="All optional. A banner appears at the top of the event.">
            <div className="space-y-2"><Label>Banner image</Label>
              <ImageUpload file={form.bannerImage} existingUrl={form.existingBannerUrl} onChange={(file) => setForm(prev => ({ ...prev, bannerImage: file }))} onClearExisting={() => setForm(prev => ({ ...prev, existingBannerUrl: "" }))} label="Upload banner image" /></div>
            <div className="space-y-2"><Label>Event photos</Label>
              <MultiImageUpload files={form.images} existingUrls={form.existingImageUrls}
                onAddFile={(file) => setForm(prev => ({ ...prev, images: [...prev.images, file] }))}
                onRemoveFile={(i) => setForm(prev => ({ ...prev, images: prev.images.filter((_, idx) => idx !== i) }))}
                onRemoveExisting={(i) => setForm(prev => ({ ...prev, existingImageUrls: prev.existingImageUrls.filter((_, idx) => idx !== i) }))}
                label="Add photo" /></div>
            <div className="space-y-2"><Label>YouTube links</Label>
              <Input placeholder="https://youtube.com/… (separate several with commas)" value={form.youtubeVideoUrls} onChange={(e) => f("youtubeVideoUrls", e.target.value)} />
              {form.youtubeVideoUrls && (
                <div className="grid grid-cols-2 gap-2">
                  {form.youtubeVideoUrls.split(",").map((u, i) => u.trim() && <YouTubePreview key={i} url={u.trim()} />)}
                </div>
              )}
            </div>
          </FormSection>
          </form>
  );
}

function toFormState(e: AlumniEvent): FormState {
  return {
    title: e.title, description: e.description ?? "", venue: e.venue,
    startDate: e.startDate ? e.startDate.slice(0, 16) : "",
    endDate: e.endDate ? e.endDate.slice(0, 16) : "",
    capacity: e.capacity != null ? String(e.capacity) : "",
    isTicketed: e.isTicketed,
    ticketPrice: e.ticketPrice != null ? String(e.ticketPrice) : "",
    paidTicketCount: e.paidTicketCount ?? 0,
    googleLocationUrl: e.googleLocationUrl ?? "",
    status: e.status,
    audienceMode: inferAudienceMode(e.communityId, e.yearGroups),
    isPublic: !!e.isPublic,
    yearGroups: e.yearGroups ?? [],
    bannerImage: null,
    existingBannerUrl: e.bannerImageUrl ?? "",
    images: [], existingImageUrls: e.imageUrls ?? [],
    youtubeVideoUrls: arrayToComma(e.youtubeVideoUrls),
    communityId: e.communityId ?? "",
  };
}

export default function AdminEventsPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";

  const focusId = useSearchParams().get("focus");
  const [showCreate, setShowCreate] = useState(false);
  const [copyFrom, setCopyFrom] = useState<AlumniEvent | null>(null);
  const [editEvent, setEditEvent] = useState<AlumniEvent | null>(null);
  const [cancelTarget, setCancelTarget] = useState<AlumniEvent | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<AlumniEvent | null>(null);
  const [page, setPage] = useState(1);
  const pageSize = 12;
  const qc = useQueryClient();

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ["admin-events", page],
    queryFn: () => getEvents(page, pageSize),
    placeholderData: (prev) => prev,
  });

  const createMut = useMutation({
    mutationFn: ({ f, copyOf }: { f: FormState; copyOf?: AlumniEvent }) => createEvent({
      communityId: isSuperAdmin && f.audienceMode === "community" ? f.communityId || undefined : undefined,
      title: f.title, description: f.description || undefined,
      startDate: f.startDate, endDate: f.endDate || undefined, venue: f.venue,
      capacity: f.capacity ? Number(f.capacity) : undefined,
      isTicketed: f.isTicketed,
      ticketPrice: f.isTicketed ? Number(f.ticketPrice) : undefined,
      googleLocationUrl: f.googleLocationUrl || undefined,
      yearGroups: isSuperAdmin && f.audienceMode === "yearGroups" ? f.yearGroups : undefined,
      isPublic: isSuperAdmin && f.audienceMode === "everyone" ? f.isPublic : false,
      bannerImage: f.bannerImage || undefined,
      images: f.images.length > 0 ? f.images : undefined,
      youtubeVideoUrls: commaToArray(f.youtubeVideoUrls),
      // A copy reuses the original's photos that were left in the form.
      ...(copyOf ? { copyFromEventId: copyOf.id, keepBanner: !f.bannerImage && !!f.existingBannerUrl, keepImageUrls: f.existingImageUrls } : {}),
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-events"] }); setShowCreate(false); setCopyFrom(null); toast.success(copyFrom ? "Event duplicated" : "Event created"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: ({ id, f }: { id: string; f: FormState }) => updateEvent(id, {
      communityId: isSuperAdmin && f.audienceMode === "community" ? f.communityId || undefined : undefined,
      title: f.title, description: f.description || undefined,
      startDate: f.startDate, endDate: f.endDate || undefined, venue: f.venue,
      capacity: f.capacity ? Number(f.capacity) : undefined,
      status: f.status,
      isTicketed: f.isTicketed,
      ticketPrice: f.isTicketed ? Number(f.ticketPrice) : undefined,
      googleLocationUrl: f.googleLocationUrl || undefined,
      yearGroups: isSuperAdmin && f.audienceMode === "yearGroups" ? f.yearGroups : undefined,
      isPublic: isSuperAdmin && f.audienceMode === "everyone" ? f.isPublic : false,
      bannerImage: f.bannerImage || undefined,
      images: f.images.length > 0 ? f.images : undefined,
      existingImageUrls: f.existingImageUrls.length > 0 ? f.existingImageUrls : undefined,
      youtubeVideoUrls: commaToArray(f.youtubeVideoUrls),
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-events"] }); setEditEvent(null); toast.success("Event updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const cancelMut = useMutation({
    mutationFn: (id: string) => cancelEvent(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-events"] }); setCancelTarget(null); toast.success("Event cancelled"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const registrationMut = useMutation({
    mutationFn: ({ e, closed }: { e: AlumniEvent; closed: boolean }) => setEventRegistrationClosed(e.id, closed),
    onSuccess: (_d, { e, closed }) => {
      qc.invalidateQueries({ queryKey: ["admin-events"] });
      toast.success(closed
        ? "RSVPs closed. People already going keep their place."
        : "RSVPs reopened.");
    },
    onError: (err) => toast.error(handleApiError(err)),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => deleteEvent(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-events"] }); setDeleteTarget(null); toast.success("Event deleted"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const events = data?.results ?? [];

  // A link from the checklist or a recommendation names one event: bring it into view.
  useEffect(() => {
    if (focusId) document.getElementById(`event-${focusId}`)?.scrollIntoView({ block: "center", behavior: "smooth" });
  }, [focusId, data]);
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      <div className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Events</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">Publish gatherings and track RSVP capacity in one place.</p>
        </div>
        <Button onClick={() => setShowCreate(true)}><Plus size={14} />Create event</Button>
      </div>

      <FormSheet
        open={showCreate || editEvent !== null || copyFrom !== null}
        onClose={() => { setShowCreate(false); setEditEvent(null); setCopyFrom(null); }}
        title={editEvent ? "Edit event" : copyFrom ? "Duplicate event" : "Create event"}
        description={editEvent ? editEvent.title : copyFrom ? `A new event started from "${copyFrom.title}"` : "Members RSVP from their portal, paying if you charge, and you see who is coming."}
        formId="event-form"
        submitLabel={editEvent ? "Save changes" : copyFrom ? "Create the copy" : "Create event"}
        saving={createMut.isPending || updateMut.isPending}
        size="lg"
      >
        {editEvent ? (
          <>
          {(isPastEvent(editEvent) || editEvent.status === "Completed" || editEvent.status === "Cancelled") && (
            <p className="mb-5 border border-border bg-muted/30 px-3.5 py-3 text-[13px] leading-relaxed text-muted-foreground">
              {editEvent.status === "Cancelled"
                ? "This event was cancelled. You can still fix its details or photos; people are not told again."
                : "This event is over. You can still add a recap, photos or fix details; people who went are not notified about edits."}
              {" "}Moving it to a future date does notify the people going.
            </p>
          )}
          <EventForm
            key={editEvent.id}
            id="event-form"
            init={toFormState(editEvent)}
            showStatus
            isSuperAdmin={isSuperAdmin}
            onSave={(f) => updateMut.mutate({ id: editEvent.id, f })}
          />
          </>
        ) : copyFrom ? (
          <>
            <p className="mb-5 border border-border bg-muted/30 px-3.5 py-3 text-[13px] leading-relaxed text-muted-foreground">
              Everything is copied except the date: choose when this one happens. People who RSVPed, payments and updates stay with the original.
            </p>
            <EventForm
              key={`copy-${copyFrom.id}`}
              id="event-form"
              init={{ ...toFormState(copyFrom), startDate: "", endDate: "", status: "Upcoming", paidTicketCount: 0 }}
              showStatus={false}
              isSuperAdmin={isSuperAdmin}
              onSave={(f) => createMut.mutate({ f, copyOf: copyFrom })}
            />
          </>
        ) : showCreate ? (
          <EventForm id="event-form" init={emptyForm} showStatus={false} isSuperAdmin={isSuperAdmin} onSave={(f) => createMut.mutate({ f })} />
        ) : null}
      </FormSheet>

      {isError || (!isLoading && !data) ? (
        <LoadError onRetry={() => refetch()} />
      ) : isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">{Array.from({ length: 4 }).map((_, i) => <CardSkeleton key={i} />)}</div>
      ) : events.length === 0 ? (
        <EmptyState icon={<Calendar size={40} />} title="Events give members a reason to show up" description="Post a reunion, dinner or meetup with the date and venue. Members RSVP from their portal and you can see who is coming." action={<Button size="sm" onClick={() => setShowCreate(true)}><Plus size={14} />Create event</Button>} />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {events.map((e) => (
            <Card key={e.id} id={`event-${e.id}`} className={cn("stagger-item hover:shadow-md transition-shadow overflow-hidden scroll-mt-24", isPastEvent(e) && "opacity-70", e.id === focusId && "ring-2 ring-primary")}>
              {e.bannerImageUrl && (
                <ZoomableImage src={e.bannerImageUrl} alt={e.title} wrapperClassName="h-36" className="w-full h-full object-cover" />
              )}
              <CardContent className="p-5">
                <div className="flex flex-wrap items-start justify-between mb-3 gap-2">
                  <div className="flex items-center gap-1.5">
                    <Badge variant={statusVariant[e.status]}>{e.status}</Badge>
                    {isPastEvent(e) && <Badge variant="secondary">Past</Badge>}
                    {e.registrationClosed && e.status !== "Cancelled" && e.status !== "Completed" && <Badge variant="warning">RSVPs closed</Badge>}
                  </div>
                  {e.isTicketed && e.ticketPrice && (
                    <span className="inline-flex items-center gap-1.5 text-sm font-semibold text-success"><Ticket size={13} />{formatCurrency(e.ticketPrice)}</span>
                  )}
                  <div className="flex items-center gap-2">
                    {e.yearGroups && e.yearGroups.length > 0 ? (
                      <Badge variant="secondary" className="text-[12px] font-bold uppercase tracking-widest">
                        {e.yearGroups.length === 1
                          ? `Cohort ${e.yearGroups[0]}`
                          : `Cohorts ${e.yearGroups.slice(0, 2).join(", ")}${e.yearGroups.length > 2 ? "…" : ""}`}
                      </Badge>
                    ) : (
                      <Badge variant="outline" className="text-[12px] font-bold uppercase tracking-widest">
                        All years
                      </Badge>
                    )}
                  </div>
                </div>
                <h3 className="font-semibold mb-2">{e.title}</h3>
                {e.description && <div className="mb-3"><ReadMore lines={2} className="text-sm text-muted-foreground">{richTextToPlain(e.description)}</ReadMore></div>}
                <div className="space-y-1.5 text-xs text-muted-foreground">
                  <div className="flex items-center gap-1.5"><Calendar size={11} />{formatDate(e.startDate)}</div>
                  <div className="flex items-center gap-1.5">
                    <MapPin size={11} />
                    {e.googleLocationUrl ? (
                      <a href={e.googleLocationUrl} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:underline">
                        {e.venue}
                      </a>
                    ) : (
                      <span>{e.venue}</span>
                    )}
                  </div>
                  <div className="flex items-center gap-1.5">
                    <Users size={11} />{e.rsvpCount} {e.rsvpCount === 1 ? "RSVP" : "RSVPs"}{e.capacity && ` / ${e.capacity} capacity`}
                  </div>
                  {e.isTicketed && (
                    <div className="flex items-center gap-1.5">
                      <Ticket size={11} />{formatCurrency(e.ticketAmountCollected ?? 0)} collected
                    </div>
                  )}
                </div>
                <div className="mt-4 space-y-2">
                  <div className="flex gap-2">
                    <Link href={`/events/${e.id}/rsvps`} className="flex-1">
                      <Button size="sm" variant="outline" className="w-full"><Users size={12} />View RSVPs</Button>
                    </Link>
                    <Link href={`/events/${e.id}/updates`} className="flex-1">
                      <Button size="sm" variant="outline" className="w-full"><Bell size={12} />Updates</Button>
                    </Link>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button size="sm" variant="outline" aria-label={`More actions for ${e.title}`} className="px-3"><MoreHorizontal size={15} /></Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end" className="min-w-[170px]">
                        <DropdownMenuItem onSelect={() => setEditEvent(e)}><Pencil size={14} />Edit</DropdownMenuItem>
                        <DropdownMenuItem onSelect={() => setCopyFrom(e)}><Copy size={14} />Duplicate</DropdownMenuItem>
                        {e.status !== "Cancelled" && e.status !== "Completed" && (
                          <DropdownMenuItem onSelect={() => registrationMut.mutate({ e, closed: !e.registrationClosed })}>
                            {e.registrationClosed
                              ? "Reopen RSVPs"
                              : "Stop taking RSVPs"}
                          </DropdownMenuItem>
                        )}
                        {e.status !== "Cancelled" && e.status !== "Completed" && (e.isTicketed
                          ? <DropdownMenuItem disabled>Paid events can&apos;t be cancelled</DropdownMenuItem>
                          : <DropdownMenuItem destructive onSelect={() => setCancelTarget(e)}>Cancel event</DropdownMenuItem>)}
                        {(e.status === "Cancelled" || e.status === "Completed" || e.isTicketed) && (e.isTicketed && (e.paidTicketCount ?? 0) > 0
                          ? <DropdownMenuItem disabled>Can&apos;t delete: paid RSVPs</DropdownMenuItem>
                          : <DropdownMenuItem destructive onSelect={() => setDeleteTarget(e)}>Delete</DropdownMenuItem>)}
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>
                  {e.status !== "Cancelled" && (
                    <MemberShareButton fill memberPath={`/events/${e.id}`} title={e.title} message={shareMessages.event({ title: e.title, startDate: e.startDate, venue: e.venue, description: e.description })} />
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />

      <ConfirmModal
        open={!!cancelTarget}
        title="Cancel Event"
        message={`Cancel "${cancelTarget?.title}"? This cannot be undone.`}
        confirmLabel="Cancel Event"
        variant="destructive"
        isLoading={cancelMut.isPending}
        onConfirm={() => cancelTarget && cancelMut.mutate(cancelTarget.id)}
        onCancel={() => setCancelTarget(null)}
      />
      <ConfirmModal
        open={!!deleteTarget}
        title="Delete Event"
        message={`Permanently delete "${deleteTarget?.title}"?`}
        confirmLabel="Delete"
        variant="destructive"
        isLoading={deleteMut.isPending}
        onConfirm={() => deleteTarget && deleteMut.mutate(deleteTarget.id)}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}
