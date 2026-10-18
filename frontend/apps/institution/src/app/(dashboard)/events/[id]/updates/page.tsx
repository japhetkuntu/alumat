"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ArrowDown, ArrowLeft, ArrowUp, Bell, FileText, ImageIcon, Link2, MoreHorizontal, Pencil, Pin, Plus, Trash2, Youtube } from "@alumni/ui";
import { Badge, Button, ConfirmModal, DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger, EmptyState, FormSheet, Input, Label, LoadError, CardSkeleton, cn, formatDateTime } from "@alumni/ui";
import {
  createEventUpdate, deleteEventUpdate, getEvent, getEventUpdates, pinEventUpdate, reorderEventUpdates, updateEventUpdate,
  EVENT_UPDATE_CATEGORIES, type EventUpdateDraft, type EventUpdateDraftBlock,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { EventUpdateEditor, draftProblem } from "@/components/institution/event-update-editor";
import type { EventUpdate } from "@/types";

const OTHER = "__other__";

function toDraft(u: EventUpdate): EventUpdateDraft {
  return {
    title: u.title,
    category: u.category,
    blocks: u.blocks.map((b): EventUpdateDraftBlock => ({
      id: b.id, type: b.type, heading: b.heading ?? "", text: b.text ?? "", url: b.url ?? "", label: b.label ?? "",
      items: b.items.map((i) => ({ key: i.url, name: i.name, sizeBytes: i.sizeBytes, url: i.url, contentType: i.contentType })),
    })),
  };
}

/** "3 photos · 1 video · 2 files": what an update holds, at a glance. */
function summarize(u: EventUpdate): string {
  const photos = u.blocks.filter((b) => b.type === "Images").reduce((n, b) => n + b.items.length, 0);
  const files = u.blocks.filter((b) => b.type === "Files").reduce((n, b) => n + b.items.length, 0);
  const videos = u.blocks.filter((b) => b.type === "Video").length;
  const links = u.blocks.filter((b) => b.type === "Link").length;
  const text = u.blocks.filter((b) => b.type === "Text").length;
  const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;
  return [text && "Text", photos && plural(photos, "photo"), videos && plural(videos, "video"), links && plural(links, "link"), files && plural(files, "file")].filter(Boolean).join(" · ");
}

function UpdateForm({ id, init, onSave, error }: { id: string; init: EventUpdateDraft; onSave: (d: EventUpdateDraft) => void; error: (m: string | null) => void }) {
  const [title, setTitle] = useState(init.title);
  const known = (EVENT_UPDATE_CATEGORIES as readonly string[]).includes(init.category);
  const [choice, setChoice] = useState<string>(init.category ? (known ? init.category : OTHER) : "");
  const [custom, setCustom] = useState(known ? "" : init.category);
  const [blocks, setBlocks] = useState(init.blocks);
  const category = choice === OTHER ? custom : choice;

  return (
    <form id={id} className="space-y-6" onSubmit={(e) => {
      e.preventDefault();
      const problem = draftProblem(title, category, blocks);
      error(problem);
      if (!problem) onSave({ title, category, blocks });
    }}>
      <div className="space-y-2">
        <Label>Title</Label>
        <Input value={title} maxLength={150} placeholder="e.g. Saturday's programme and venue map" onChange={(e) => setTitle(e.target.value)} required />
      </div>

      <div className="space-y-2">
        <Label>Category</Label>
        <div className="flex flex-wrap gap-2" role="group" aria-label="Category">
          {[...EVENT_UPDATE_CATEGORIES, OTHER].map((c) => (
            <button key={c} type="button" aria-pressed={choice === c} onClick={() => setChoice(c)}
              className={cn("border px-3 py-1.5 text-[13px] font-semibold transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary",
                choice === c ? "border-primary bg-primary text-primary-foreground" : "border-border bg-card hover:border-primary/50")}>
              {c === OTHER ? "Something else…" : c}
            </button>
          ))}
        </div>
        {choice === OTHER && (
          <Input value={custom} maxLength={40} placeholder="Your own label, e.g. Day 2 highlights" onChange={(e) => setCustom(e.target.value)} autoFocus />
        )}
      </div>

      <div className="space-y-2">
        <Label>What&apos;s in it</Label>
        <EventUpdateEditor blocks={blocks} onChange={setBlocks} />
      </div>
    </form>
  );
}

export default function EventUpdatesPage() {
  const { id } = useParams<{ id: string }>();
  const qc = useQueryClient();
  const [editing, setEditing] = useState<EventUpdate | "new" | null>(null);
  const [deleting, setDeleting] = useState<EventUpdate | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  const { data: event } = useQuery({ queryKey: ["admin-event", id], queryFn: () => getEvent(id) });
  const { data: updates, isLoading, isError, refetch } = useQuery({ queryKey: ["admin-event-updates", id], queryFn: () => getEventUpdates(id) });

  const refresh = () => qc.invalidateQueries({ queryKey: ["admin-event-updates", id] });
  const close = () => { setEditing(null); setFormError(null); };

  const save = useMutation({
    mutationFn: (draft: EventUpdateDraft) => (editing && editing !== "new" ? updateEventUpdate(id, editing.id, draft) : createEventUpdate(id, draft)),
    onSuccess: () => {
      const wasNew = editing === "new";
      refresh(); close();
      toast.success(wasNew ? "Update posted. People going have been notified." : "Update saved.");
    },
    onError: (e) => setFormError(handleApiError(e)),
  });
  const pin = useMutation({
    mutationFn: ({ u, pinned }: { u: EventUpdate; pinned: boolean }) => pinEventUpdate(id, u.id, pinned),
    onSuccess: () => refresh(),
    onError: (e) => toast.error(handleApiError(e)),
  });
  const arrange = useMutation({
    mutationFn: (ids: string[]) => reorderEventUpdates(id, ids),
    // Show the new order at once; the server's answer (or a reload, if it failed) settles it.
    onMutate: async (ids) => {
      await qc.cancelQueries({ queryKey: ["admin-event-updates", id] });
      const before = qc.getQueryData<EventUpdate[]>(["admin-event-updates", id]);
      if (before) qc.setQueryData<EventUpdate[]>(["admin-event-updates", id], ids.map((i) => before.find((u) => u.id === i)!).filter(Boolean));
      return { before };
    },
    onSuccess: (list) => qc.setQueryData(["admin-event-updates", id], list),
    onError: (e, _ids, ctx) => { if (ctx?.before) qc.setQueryData(["admin-event-updates", id], ctx.before); toast.error(handleApiError(e)); refresh(); },
  });
  /** Moves an update one place up or down among those pinned alike (pinned ones always stay above the rest). */
  const move = (u: EventUpdate, by: -1 | 1) => {
    if (!updates) return;
    const group = updates.filter((x) => x.isPinned === u.isPinned);
    const at = group.findIndex((x) => x.id === u.id);
    const to = at + by;
    if (to < 0 || to >= group.length) return;
    [group[at], group[to]] = [group[to], group[at]];
    const pinned = u.isPinned ? group : updates.filter((x) => x.isPinned);
    const rest = u.isPinned ? updates.filter((x) => !x.isPinned) : group;
    arrange.mutate([...pinned, ...rest].map((x) => x.id));
  };
  const remove = useMutation({
    mutationFn: (u: EventUpdate) => deleteEventUpdate(id, u.id),
    onSuccess: () => { refresh(); setDeleting(null); toast.success("Update deleted."); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  return (
    <div className="p-4 sm:p-[26px] max-w-[980px] mx-auto space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex items-start gap-3">
          <Link href="/events"><Button size="sm" variant="ghost" aria-label="Back to events"><ArrowLeft size={14} />Events</Button></Link>
          <div>
            <h1 className="text-[22px] font-bold m-0">Updates</h1>
            <p className="text-[13px] text-muted-foreground">{event?.title ?? "Event"}</p>
          </div>
        </div>
        <Button onClick={() => { setFormError(null); setEditing("new"); }}><Plus size={14} />New update</Button>
      </div>

      <div className="flex items-start gap-2.5 border border-border bg-muted/30 px-3.5 py-3 text-[13px] leading-relaxed text-muted-foreground">
        <Bell size={14} className="mt-0.5 shrink-0 text-primary" />
        <p>Only people going can read updates; everyone else just sees that they exist. Posting one notifies the people going. Editing does not. Use the arrows to arrange the updates in the order you want them read; pinned ones always stay on top, and a new update starts at the top.</p>
      </div>

      <FormSheet
        open={editing !== null}
        onClose={close}
        title={editing === "new" ? "New update" : "Edit update"}
        description={event?.title}
        formId="event-update-form"
        submitLabel={editing === "new" ? "Post update" : "Save changes"}
        savingLabel={editing === "new" ? "Posting" : "Saving"}
        saving={save.isPending}
        error={formError}
        size="lg"
      >
        {editing !== null && (
          <UpdateForm
            key={editing === "new" ? "new" : editing.id}
            id="event-update-form"
            init={editing === "new" ? { title: "", category: "", blocks: [] } : toDraft(editing)}
            onSave={(d) => { setFormError(null); save.mutate(d); }}
            error={setFormError}
          />
        )}
      </FormSheet>

      {isError ? (
        <LoadError onRetry={() => refetch()} />
      ) : isLoading ? (
        <div className="space-y-3">{Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}</div>
      ) : !updates || updates.length === 0 ? (
        <EmptyState
          icon={<Bell size={40} />}
          title="Keep your attendees in the loop"
          description="Share the programme, a venue map, the slides, or the day's photos with everyone who is going. Mix text, photos, video, links and files in one update."
          action={<Button size="sm" onClick={() => { setFormError(null); setEditing("new"); }}><Plus size={14} />Post the first update</Button>}
        />
      ) : (
        <ul className="space-y-3">
          {updates.map((u) => {
            const sameGroup = updates.filter((x) => x.isPinned === u.isPinned);
            const place = sameGroup.findIndex((x) => x.id === u.id);
            const firstText = u.blocks.find((b) => b.type === "Text")?.text;
            const photos = u.blocks.filter((b) => b.type === "Images").flatMap((b) => b.items).slice(0, 4);
            return (
              <li key={u.id} className="border border-border bg-card">
                <div className="flex items-start justify-between gap-3 p-4">
                  <div className="min-w-0 space-y-1.5">
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge variant="info">{u.category}</Badge>
                      {u.isPinned && <Badge variant="secondary" className="gap-1"><Pin size={10} />Pinned</Badge>}
                    </div>
                    <h2 className="text-[15.5px] font-semibold leading-snug">{u.title}</h2>
                    <p className="text-[12.5px] text-muted-foreground">
                      {formatDateTime(u.createdAt)}{u.postedByName ? ` · ${u.postedByName}` : ""}{u.updatedAt ? " · edited" : ""}
                    </p>
                  </div>
                  <div className="flex shrink-0 items-center gap-1">
                  {sameGroup.length > 1 && (
                    <>
                      <Button size="sm" variant="ghost" className="px-2" disabled={place === 0 || arrange.isPending} onClick={() => move(u, -1)} aria-label={`Move ${u.title} up`}><ArrowUp size={14} /></Button>
                      <Button size="sm" variant="ghost" className="px-2" disabled={place === sameGroup.length - 1 || arrange.isPending} onClick={() => move(u, 1)} aria-label={`Move ${u.title} down`}><ArrowDown size={14} /></Button>
                    </>
                  )}
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button size="sm" variant="outline" className="shrink-0 px-3" aria-label={`More actions for ${u.title}`}><MoreHorizontal size={15} /></Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end" className="min-w-[160px]">
                      <DropdownMenuItem onSelect={() => { setFormError(null); setEditing(u); }}><Pencil size={14} />Edit</DropdownMenuItem>
                      <DropdownMenuItem onSelect={() => pin.mutate({ u, pinned: !u.isPinned })}><Pin size={14} />{u.isPinned ? "Unpin" : "Pin to top"}</DropdownMenuItem>
                      <DropdownMenuItem destructive onSelect={() => setDeleting(u)}><Trash2 size={14} />Delete</DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                  </div>
                </div>
                {(firstText || photos.length > 0) && (
                  <div className="space-y-3 border-t border-border px-4 py-3">
                    {firstText && <p className="line-clamp-2 whitespace-pre-line text-[13.5px] text-muted-foreground">{firstText}</p>}
                    {photos.length > 0 && (
                      <div className="flex gap-1.5">
                        {photos.map((p) => <img key={p.url} src={p.url} alt="" className="size-14 border border-border object-cover" loading="lazy" />)}
                      </div>
                    )}
                  </div>
                )}
                <div className="flex items-center gap-3 border-t border-border bg-muted/20 px-4 py-2 text-[12.5px] text-muted-foreground">
                  <span className="flex items-center gap-1.5">
                    {u.blocks.some((b) => b.type === "Images") && <ImageIcon size={12} />}
                    {u.blocks.some((b) => b.type === "Video") && <Youtube size={12} />}
                    {u.blocks.some((b) => b.type === "Link") && <Link2 size={12} />}
                    {u.blocks.some((b) => b.type === "Files") && <FileText size={12} />}
                  </span>
                  {summarize(u)}
                </div>
              </li>
            );
          })}
        </ul>
      )}

      <ConfirmModal
        open={!!deleting}
        title="Delete this update?"
        message={`"${deleting?.title ?? ""}" disappears for everyone who is going. This can't be undone.`}
        confirmLabel="Delete update"
        variant="destructive"
        isLoading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting)}
        onCancel={() => setDeleting(null)}
      />
    </div>
  );
}
