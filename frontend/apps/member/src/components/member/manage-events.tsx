"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button, FormSection, FormSheet, Input, formatDateTime } from "@alumni/ui";
import { cancelGroupEvent, createGroupEvent, updateGroupEvent, type GroupEvent, type ManageGroup, type SaveGroupEvent } from "@/lib/member-api";
import { BannerField, GalleryField, Thumb, VideoField, useMedia } from "@/components/member/manage-media";
import { Confirm, Field, ItemMenu, ListState, Panel, ROW, RichField, errorText } from "@/components/member/manage-ui";
import { manageQueries, upsertById } from "@/components/member/manage-queries";

/** An ISO time as the value a datetime-local box wants, in the person's own time zone. */
const toLocalInput = (iso?: string | null) => {
  if (!iso) return "";
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

function EventForm({ id, initial, onSave }: { id: string; initial: GroupEvent | null; onSave: (b: SaveGroupEvent) => void }) {
  const [title, setTitle] = useState(initial?.title ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [venue, setVenue] = useState(initial?.venue ?? "");
  const [start, setStart] = useState(toLocalInput(initial?.startDate));
  const [end, setEnd] = useState(toLocalInput(initial?.endDate));
  const [capacity, setCapacity] = useState(initial?.capacity ? String(initial.capacity) : "");
  const [mapLink, setMapLink] = useState(initial?.googleLocationUrl ?? "");
  const media = useMedia({ banner: initial?.bannerImageUrl, images: initial?.imageUrls, videos: initial?.youtubeVideoUrls });

  return (
    <form id={id} className="space-y-5" onSubmit={(e) => {
      e.preventDefault();
      onSave({ title: title.trim(), venue: venue.trim(), description: description || undefined, startDate: new Date(start).toISOString(), endDate: end ? new Date(end).toISOString() : null, capacity: capacity ? Number(capacity) : null, googleLocationUrl: mapLink.trim() || undefined, ...media.input() });
    }}>
      <FormSection title="The event">
        <Field id="ev-title" label="Event title"><Input id="ev-title" autoFocus required maxLength={150} placeholder="e.g. Reunion lunch" value={title} onChange={(e) => setTitle(e.target.value)} /></Field>
        <RichField id="ev-desc" label="Details" optional placeholder="What the event is, who should come, and what to bring." value={description} onChange={setDescription} />
      </FormSection>
      <FormSection title="When and where" hint="Members see the date, time and venue on the event and in their calendar.">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field id="ev-start" label="Starts"><Input id="ev-start" type="datetime-local" required value={start} onChange={(e) => setStart(e.target.value)} /></Field>
          <Field id="ev-end" label="Ends" optional><Input id="ev-end" type="datetime-local" min={start} value={end} onChange={(e) => setEnd(e.target.value)} /></Field>
        </div>
        <Field id="ev-venue" label="Venue"><Input id="ev-venue" required maxLength={200} placeholder="e.g. Main Hall, Community Centre, or Online" value={venue} onChange={(e) => setVenue(e.target.value)} /></Field>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field id="ev-map" label="Map or meeting link" optional hint="Members get a button beside the venue that opens it."><Input id="ev-map" type="url" inputMode="url" placeholder="Google Maps, Zoom, Meet, Teams…" value={mapLink} onChange={(e) => setMapLink(e.target.value)} /></Field>
          <Field id="ev-cap" label="Places" optional hint="It's free to attend. Leave empty for no limit."><Input id="ev-cap" type="number" inputMode="numeric" min={1} placeholder="No limit" value={capacity} onChange={(e) => setCapacity(e.target.value)} /></Field>
        </div>
      </FormSection>
      <FormSection title="Photos and video" hint="All optional. A banner appears at the top of the event.">
        <BannerField media={media} id="ev-banner" />
        <GalleryField media={media} id="ev-photos" label="Event photos" />
        <VideoField media={media} id="ev-videos" />
      </FormSection>
    </form>
  );
}

export function Events({ group }: { group: ManageGroup }) {
  const qc = useQueryClient();
  const key = manageQueries.events(group).queryKey;
  const { data = [], isLoading, isError, refetch } = useQuery(manageQueries.events(group));
  const [editing, setEditing] = useState<GroupEvent | "new" | null>(null);
  const [cancelling, setCancelling] = useState<GroupEvent | null>(null);

  const save = useMutation({
    mutationFn: ({ id, body }: { id?: string; body: SaveGroupEvent }) => (id ? updateGroupEvent(group, id, body) : createGroupEvent(group, body)),
    onSuccess: (saved, { id }) => { qc.setQueryData<GroupEvent[]>(key, (l) => upsertById(l, saved)); toast.success(id ? "Event updated" : "Event created"); setEditing(null); },
  });
  const cancel = useMutation({
    mutationFn: (e: GroupEvent) => cancelGroupEvent(group, e.id),
    onSuccess: (_r, e) => { qc.setQueryData<GroupEvent[]>(key, (l) => (l ?? []).map((x) => (x.id === e.id ? { ...x, status: "Cancelled", canEdit: false } : x))); setCancelling(null); toast.success("Event cancelled"); },
    onError: (e) => { setCancelling(null); toast.error(errorText(e) ?? "That didn't work"); },
  });

  return (
    <Panel id="mg-events" title="Events" summary={data.length > 0 ? `${data.length} ${data.length === 1 ? "event" : "events"}` : undefined} action={<Button size="sm" onClick={() => { save.reset(); setEditing("new"); }}>New event</Button>}>
      <ListState loading={isLoading} error={isError} onRetry={() => refetch()} empty={data.length === 0} emptyText="No events yet. Create one and the group is told about it.">
        <ul className="mt-2 divide-y divide-border">
          {data.map((e) => (
            <li key={e.id} className={`flex items-start gap-3 py-3 ${ROW}`}>
              <Thumb src={e.bannerImageUrl ?? e.imageUrls?.[0]} alt="" />
              <div className="min-w-0 flex-1">
                <p className="text-[14px] font-semibold leading-snug">{e.title}{e.status === "Cancelled" && <span className="ml-2 rounded-full bg-secondary px-2 py-0.5 text-[11px] font-medium text-muted-foreground">Cancelled</span>}</p>
                <p className="mt-0.5 text-[12.5px] text-muted-foreground">{formatDateTime(e.startDate)} · {e.venue}</p>
                <p className="mt-0.5 text-[12.5px] text-muted-foreground">{e.rsvpCount} going{e.capacity ? ` of ${e.capacity}` : ""}</p>
              </div>
              <ItemMenu label={e.title} onEdit={e.canEdit ? () => { save.reset(); setEditing(e); } : undefined} onRemove={e.canEdit ? () => setCancelling(e) : undefined} removeLabel="Cancel event" />
            </li>
          ))}
        </ul>
      </ListState>

      <FormSheet open={editing !== null} onClose={() => setEditing(null)} title={editing && editing !== "new" ? "Edit event" : "New event"}
        description={`For ${group.name} only. Members are told about it.`} formId="manage-event-form" submitLabel={editing && editing !== "new" ? "Save changes" : "Create event"}
        saving={save.isPending} error={errorText(save.error)}>
        {editing !== null && <EventForm key={editing === "new" ? "new" : editing.id} id="manage-event-form" initial={editing === "new" ? null : editing}
          onSave={(body) => save.mutate({ id: editing === "new" ? undefined : editing.id, body })} />}
      </FormSheet>
      {cancelling && <Confirm title={`Cancel "${cancelling.title}"?`} message="Everyone who said they're going is told it's cancelled. This can't be undone." confirmLabel="Cancel the event" busy={cancel.isPending} onConfirm={() => cancel.mutate(cancelling)} onCancel={() => setCancelling(null)} />}
    </Panel>
  );
}
