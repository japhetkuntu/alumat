"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Calendar, MapPin, Users, Pencil } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { FormSelect } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { formatDate, formatCurrency } from "@/lib/utils";
import { getEvents, createEvent, updateEvent, cancelEvent, deleteEvent } from "@/lib/admin-api";
import { ImageUpload } from "@/components/ui/image-upload";
import { MultiImageUpload } from "@/components/ui/multi-image-upload";
import { YouTubePreview } from "@/components/ui/youtube-embed";
import { YearGroupPicker } from "@/components/ui/year-group-picker";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { useAuth } from "@/hooks/use-auth";
import { CardSkeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import type { AlumniEvent, EventStatus } from "@/types";

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
  googleLocationUrl: string;
  status: string;
  yearGroupsAll: boolean;
  yearGroups: number[];
  bannerImage: File | null;
  existingBannerUrl: string;
  images: File[];
  existingImageUrls: string[];
  youtubeVideoUrls: string;
}

const emptyForm: FormState = {
  title: "", description: "", startDate: "", endDate: "", venue: "", capacity: "",
  googleLocationUrl: "", status: "Upcoming",
  yearGroupsAll: true, yearGroups: [],
  bannerImage: null, existingBannerUrl: "", images: [], existingImageUrls: [], youtubeVideoUrls: "",
};

function commaToArray(s: string): string[] | undefined {
  const arr = s.split(",").map(x => x.trim()).filter(Boolean);
  return arr.length > 0 ? arr : undefined;
}

function arrayToComma(arr?: string[]): string {
  return arr?.join(", ") ?? "";
}

function EventForm({ init, onSave, onCancel, saving, showStatus, title, isSuperAdmin }: {
  init: FormState; onSave: (f: FormState) => void;
  onCancel: () => void; saving: boolean; showStatus: boolean; title: string; isSuperAdmin: boolean;
}) {
  const [form, setForm] = useState(init);
  const f = <K extends keyof FormState>(k: K, v: FormState[K]) => setForm(prev => ({ ...prev, [k]: v }));
  return (
    <Card>
      <CardHeader><CardTitle className="text-base">{title}</CardTitle></CardHeader>
      <CardContent>
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); onSave(form); }}>
          <div className="space-y-2"><Label>Event Title</Label>
            <Input placeholder="e.g. UMaT Alumni Homecoming 2027" value={form.title} onChange={(e) => f("title", e.target.value)} required /></div>
          <div className="space-y-2"><Label>Description</Label>
            <Textarea placeholder="Describe the event..." rows={3} value={form.description} onChange={(e) => f("description", e.target.value)} /></div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="space-y-2"><Label>Start Date</Label>
              <Input type="datetime-local" value={form.startDate} onChange={(e) => f("startDate", e.target.value)} required /></div>
            <div className="space-y-2"><Label>End Date (optional)</Label>
              <Input type="datetime-local" value={form.endDate} onChange={(e) => f("endDate", e.target.value)} /></div>
            <div className="space-y-2"><Label>Venue</Label>
              <Input placeholder="e.g. UMaT Campus" value={form.venue} onChange={(e) => f("venue", e.target.value)} required /></div>
            <div className="space-y-2"><Label>Google Maps location URL (optional)</Label>
              <Input placeholder="https://maps.app.goo.gl/..." value={form.googleLocationUrl} onChange={(e) => f("googleLocationUrl", e.target.value)} /></div>
            <div className="space-y-2"><Label>Capacity (optional)</Label>
              <Input type="number" placeholder="200" value={form.capacity} onChange={(e) => f("capacity", e.target.value)} /></div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <p className="text-sm font-semibold">Free event only</p>
              <p className="text-xs text-muted-foreground">Paid / ticketed events are not supported yet.</p>
            </div>
            <div className="space-y-2">
              {isSuperAdmin ? (
                <>
                  <div className="flex items-center justify-between">
                    <Label>Target year groups</Label>
                    <label className="flex items-center gap-2 text-sm font-medium">
                      <input
                        type="checkbox"
                        checked={form.yearGroupsAll}
                        onChange={(e) => setForm((prev) => ({ ...prev, yearGroupsAll: e.target.checked }))}
                        className="h-4 w-4 rounded border border-muted-foreground"
                      />
                      All years
                    </label>
                  </div>
                  {!form.yearGroupsAll ? (
                    <YearGroupPicker
                      value={form.yearGroups}
                      onChange={(years) => setForm((prev) => ({ ...prev, yearGroups: years }))}
                    />
                  ) : (
                    <p className="text-xs text-muted-foreground">This event will be visible to all year groups.</p>
                  )}
                </>
              ) : (
                <p className="text-xs text-muted-foreground">For regular admins, this event will be restricted to your graduation year group.</p>
              )}
            </div>
            {showStatus && (
              <div className="space-y-2"><Label>Status</Label>
                <FormSelect value={form.status} onValueChange={(v) => f("status", v)}
                  options={statusOptions.map(s => ({ value: s, label: s }))} /></div>
            )}
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-1 gap-4">
            <div className="space-y-2"><Label>Banner Image (optional)</Label>
              <ImageUpload file={form.bannerImage} existingUrl={form.existingBannerUrl} onChange={(file) => setForm(prev => ({ ...prev, bannerImage: file }))} onClearExisting={() => setForm(prev => ({ ...prev, existingBannerUrl: "" }))} label="Upload banner image" /></div>
            <div className="space-y-2"><Label>Event Photos (optional)</Label>
              <MultiImageUpload files={form.images} existingUrls={form.existingImageUrls}
                onAddFile={(file) => setForm(prev => ({ ...prev, images: [...prev.images, file] }))}
                onRemoveFile={(i) => setForm(prev => ({ ...prev, images: prev.images.filter((_, idx) => idx !== i) }))}
                onRemoveExisting={(i) => setForm(prev => ({ ...prev, existingImageUrls: prev.existingImageUrls.filter((_, idx) => idx !== i) }))}
                label="Add photo" /></div>
            <div className="space-y-2"><Label>YouTube URLs (comma-separated, optional)</Label>
              <Input placeholder="https://youtube.com/..." value={form.youtubeVideoUrls} onChange={(e) => f("youtubeVideoUrls", e.target.value)} />
              {form.youtubeVideoUrls && (
                <div className="grid grid-cols-2 gap-2">
                  {form.youtubeVideoUrls.split(",").map((u, i) => u.trim() && <YouTubePreview key={i} url={u.trim()} />)}
                </div>
              )}
            </div>
          </div>
          <div className="flex gap-3">
            <Button type="submit" size="sm" isLoading={saving} loadingText="Saving">Save</Button>
            <Button type="button" size="sm" variant="outline" onClick={onCancel}>Cancel</Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

function toFormState(e: AlumniEvent): FormState {
  return {
    title: e.title, description: e.description ?? "", venue: e.venue,
    startDate: e.startDate ? e.startDate.slice(0, 16) : "",
    endDate: e.endDate ? e.endDate.slice(0, 16) : "",
    capacity: e.capacity != null ? String(e.capacity) : "",
    googleLocationUrl: e.googleLocationUrl ?? "",
    status: e.status,
    yearGroupsAll: !e.yearGroups || e.yearGroups.length === 0,
    yearGroups: e.yearGroups ?? [],
    bannerImage: null,
    existingBannerUrl: e.bannerImageUrl ?? "",
    images: [], existingImageUrls: e.imageUrls ?? [],
    youtubeVideoUrls: arrayToComma(e.youtubeVideoUrls),
  };
}

export default function AdminEventsPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";

  const [showCreate, setShowCreate] = useState(false);
  const [editEvent, setEditEvent] = useState<AlumniEvent | null>(null);
  const [cancelTarget, setCancelTarget] = useState<AlumniEvent | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<AlumniEvent | null>(null);
  const [page, setPage] = useState(1);
  const pageSize = 12;
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ["admin-events", page],
    queryFn: () => getEvents(page, pageSize),
    placeholderData: (prev) => prev,
  });

  const createMut = useMutation({
    mutationFn: (f: FormState) => createEvent({
      title: f.title, description: f.description || undefined,
      startDate: f.startDate, endDate: f.endDate || undefined, venue: f.venue,
      capacity: f.capacity ? Number(f.capacity) : undefined,
      isTicketed: false,
      ticketPrice: undefined,
      googleLocationUrl: f.googleLocationUrl || undefined,
      yearGroups: isSuperAdmin ? (f.yearGroupsAll ? undefined : f.yearGroups) : undefined,
      bannerImage: f.bannerImage || undefined,
      images: f.images.length > 0 ? f.images : undefined,
      youtubeVideoUrls: commaToArray(f.youtubeVideoUrls),
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-events"] }); setShowCreate(false); toast.success("Event created"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: ({ id, f }: { id: string; f: FormState }) => updateEvent(id, {
      title: f.title, description: f.description || undefined,
      startDate: f.startDate, endDate: f.endDate || undefined, venue: f.venue,
      capacity: f.capacity ? Number(f.capacity) : undefined,
      status: f.status,
      isTicketed: false,
      ticketPrice: undefined,
      googleLocationUrl: f.googleLocationUrl || undefined,
      yearGroups: isSuperAdmin ? (f.yearGroupsAll ? undefined : f.yearGroups) : undefined,
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

  const deleteMut = useMutation({
    mutationFn: (id: string) => deleteEvent(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-events"] }); setDeleteTarget(null); toast.success("Event deleted"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const events = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="p-6 lg:p-8 space-y-6 page-enter">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-[22px] font-bold tracking-tight">Events</h1>
          <p className="text-muted-foreground text-[13px] mt-1">Manage alumni events and reunions</p>
        </div>
        <Button size="sm" onClick={() => setShowCreate(!showCreate)}><Plus size={14} />New Event</Button>
      </div>

      {showCreate && (
        <EventForm title="Create New Event" init={emptyForm} saving={createMut.isPending} showStatus={false}
          isSuperAdmin={isSuperAdmin}
          onSave={(f) => createMut.mutate(f)} onCancel={() => setShowCreate(false)} />
      )}

      {editEvent && (
        <EventForm title={`Edit — ${editEvent.title}`} init={toFormState(editEvent)} saving={updateMut.isPending} showStatus
          isSuperAdmin={isSuperAdmin}
          onSave={(f) => updateMut.mutate({ id: editEvent.id, f })} onCancel={() => setEditEvent(null)} />
      )}

      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">{Array.from({ length: 4 }).map((_, i) => <CardSkeleton key={i} />)}</div>
      ) : events.length === 0 ? (
        <EmptyState icon={<Calendar size={40} />} title="No events yet" description="Create your first alumni event or reunion." action={<Button size="sm" onClick={() => setShowCreate(true)}><Plus size={14} />New Event</Button>} />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {events.map((e) => (
            <Card key={e.id} className="stagger-item hover:shadow-md transition-shadow overflow-hidden">
              {e.bannerImageUrl && (
                <div className="h-36 overflow-hidden">
                  <img src={e.bannerImageUrl} alt={e.title} className="w-full h-full object-cover" loading="lazy" />
                </div>
              )}
              <CardContent className="p-5">
                <div className="flex flex-wrap items-start justify-between mb-3 gap-2">
                  <Badge variant={statusVariant[e.status]}>{e.status}</Badge>
                  {e.isTicketed && e.ticketPrice && (
                    <span className="text-sm font-semibold text-green-600">{formatCurrency(e.ticketPrice)}</span>
                  )}
                  <div className="flex items-center gap-2">
                    {e.yearGroups && e.yearGroups.length > 0 ? (
                      <Badge variant="secondary" className="text-[9px] font-bold uppercase tracking-widest">
                        {e.yearGroups.length === 1
                          ? `Class of ${e.yearGroups[0]}`
                          : `Classes of ${e.yearGroups.slice(0, 2).join(", ")}${e.yearGroups.length > 2 ? "…" : ""}`}
                      </Badge>
                    ) : (
                      <Badge variant="outline" className="text-[9px] font-bold uppercase tracking-widest">
                        All years
                      </Badge>
                    )}
                  </div>
                </div>
                <h3 className="font-semibold mb-2">{e.title}</h3>
                {e.description && <p className="text-sm text-muted-foreground mb-3 line-clamp-2">{e.description}</p>}
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
                    <Users size={11} />{e.rsvpCount} RSVPs{e.capacity && ` / ${e.capacity} capacity`}
                  </div>
                </div>
                <div className="flex gap-2 mt-4 flex-wrap">
                  <Link href={`/events/${e.id}/rsvps`}>
                    <Button size="sm" variant="outline"><Users size={12} />View RSVPs</Button>
                  </Link>
                  {e.status !== "Cancelled" && e.status !== "Completed" && (
                    <Button size="sm" variant="outline" onClick={() => setEditEvent(e)}><Pencil size={12} />Edit</Button>
                  )}
                  {e.status !== "Cancelled" && e.status !== "Completed" && (
                    <Button size="sm" variant="destructive" onClick={() => setCancelTarget(e)}>Cancel</Button>
                  )}
                  {(e.status === "Cancelled" || e.status === "Completed") && (
                    <Button size="sm" variant="destructive" onClick={() => setDeleteTarget(e)}>Delete</Button>
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
