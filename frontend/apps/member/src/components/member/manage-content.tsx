"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button, FormSection, FormSheet, Input, Textarea, formatDate, richTextToPlain } from "@alumni/ui";
import {
  closeGroupJob, createGroupJob, createGroupNews, createGroupResource, deleteGroupNews, deleteGroupResource, updateGroupJob, updateGroupNews, updateGroupResource,
  type GroupJob, type GroupNews, type GroupResource, type ManageGroup, type SaveGroupJob, type SaveGroupNews, type SaveGroupResource,
} from "@/lib/member-api";
import { BannerField, GalleryField, Thumb, VideoField, useMedia } from "@/components/member/manage-media";
import { Choice, Confirm, Field, ItemMenu, ListState, Panel, ROW, RichField, errorText } from "@/components/member/manage-ui";
import { manageQueries, removeById, upsertById } from "@/components/member/manage-queries";

const NEWS_CATEGORIES = ["Announcement", "Achievement", "News", "Event", "Opportunity", "Other"];
const JOB_TYPES = ["Full-time", "Part-time", "Contract", "Internship"];
const toDateInput = (iso?: string | null) => (iso ? iso.slice(0, 10) : "");

// ── News ─────────────────────────────────────────────────────────────────────

function NewsForm({ id, initial, onSave, onProblem }: { id: string; initial: GroupNews | null; onSave: (b: SaveGroupNews) => void; onProblem: (m: string) => void }) {
  const [title, setTitle] = useState(initial?.title ?? "");
  const [category, setCategory] = useState(initial?.category && NEWS_CATEGORIES.includes(initial.category) ? initial.category : "Announcement");
  const [content, setContent] = useState(initial?.content ?? "");
  const media = useMedia({ images: initial?.imageUrls, videos: initial?.youtubeVideoUrls });
  return (
    <form id={id} className="space-y-5" onSubmit={(e) => {
      e.preventDefault();
      if (!richTextToPlain(content).trim()) return onProblem("Write something to share");
      onSave({ title: title.trim(), content, category, ...media.input() });
    }}>
      <FormSection title="The post">
        <Field id="nw-title" label="Headline"><Input id="nw-title" autoFocus required maxLength={200} value={title} onChange={(e) => setTitle(e.target.value)} /></Field>
        <Field id="nw-cat" label="Kind of news"><Choice label="Kind of news" value={category} options={NEWS_CATEGORIES} onChange={setCategory} /></Field>
        <RichField id="nw-body" label="What do you want to say?" minHeight={180} value={content} onChange={setContent} />
      </FormSection>
      <FormSection title="Photos and video" hint="All optional.">
        <GalleryField media={media} id="nw-photos" />
        <VideoField media={media} id="nw-videos" />
      </FormSection>
    </form>
  );
}

export function NewsSection({ group }: { group: ManageGroup }) {
  const qc = useQueryClient();
  const key = manageQueries.news(group).queryKey;
  const q = useQuery(manageQueries.news(group));
  const [editing, setEditing] = useState<GroupNews | "new" | null>(null);
  const [deleting, setDeleting] = useState<GroupNews | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: ({ id, body }: { id?: string; body: SaveGroupNews }) => (id ? updateGroupNews(group, id, body) : createGroupNews(group, body)),
    onSuccess: (saved, { id }) => { qc.setQueryData<GroupNews[]>(key, (l) => upsertById(l, saved)); toast.success(id ? "Saved" : "Posted"); setEditing(null); },
  });
  const del = useMutation({
    mutationFn: (n: GroupNews) => deleteGroupNews(group, n.id),
    onSuccess: (_r, n) => { qc.setQueryData<GroupNews[]>(key, (l) => removeById(l, n.id)); setDeleting(null); toast.success("Deleted"); },
    onError: (e) => { setDeleting(null); toast.error(errorText(e) ?? "That didn't work"); },
  });
  const open = (v: GroupNews | "new") => { save.reset(); setProblem(null); setEditing(v); };
  const data = q.data ?? [];
  return (
    <Panel id="mg-news" title="News" summary={data.length > 0 ? `${data.length} ${data.length === 1 ? "post" : "posts"}` : undefined} action={<Button size="sm" onClick={() => open("new")}>Post news</Button>}>
      <ListState loading={q.isLoading} error={q.isError} onRetry={() => q.refetch()} empty={data.length === 0} emptyText="Nothing posted yet. News you post shows in the News page for the people in this group.">
        <ul className="mt-2 divide-y divide-border">
          {data.map((n) => (
            <li key={n.id} className={`flex items-start gap-3 py-3 ${ROW}`}>
              <Thumb src={n.imageUrls?.[0]} alt="" />
              <div className="min-w-0 flex-1">
                <p className="text-[14px] font-semibold leading-snug">{n.title}</p>
                <p className="mt-0.5 text-[12.5px] text-muted-foreground">{n.category}{n.publishedAt ? ` · ${formatDate(n.publishedAt)}` : ""}</p>
              </div>
              <ItemMenu label={n.title} onEdit={n.canEdit ? () => open(n) : undefined} onRemove={n.canEdit ? () => setDeleting(n) : undefined} removeLabel="Delete" />
            </li>
          ))}
        </ul>
      </ListState>
      <FormSheet open={editing !== null} onClose={() => setEditing(null)} title={editing && editing !== "new" ? "Edit news" : "Post news"} description={`Members of ${group.name} see it in News.`}
        formId="manage-news-form" submitLabel={editing && editing !== "new" ? "Save changes" : "Post"} saving={save.isPending} error={problem ?? errorText(save.error)}>
        {editing !== null && <NewsForm key={editing === "new" ? "new" : editing.id} id="manage-news-form" initial={editing === "new" ? null : editing} onProblem={setProblem}
          onSave={(body) => { setProblem(null); save.mutate({ id: editing === "new" ? undefined : editing.id, body }); }} />}
      </FormSheet>
      {deleting && <Confirm title={`Delete "${deleting.title}"?`} message="It disappears from the News page for everyone in the group. This can't be undone." confirmLabel="Delete" busy={del.isPending} onConfirm={() => del.mutate(deleting)} onCancel={() => setDeleting(null)} />}
    </Panel>
  );
}

// ── Jobs ─────────────────────────────────────────────────────────────────────

function JobForm({ id, initial, onSave }: { id: string; initial: GroupJob | null; onSave: (b: SaveGroupJob) => void }) {
  const [title, setTitle] = useState(initial?.title ?? "");
  const [company, setCompany] = useState(initial?.company ?? "");
  const [location, setLocation] = useState(initial?.location ?? "");
  const [type, setType] = useState(initial?.type ?? "Full-time");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [applyUrl, setApplyUrl] = useState(initial?.applyUrl ?? "");
  const [deadline, setDeadline] = useState(toDateInput(initial?.deadline));
  const media = useMedia({ banner: initial?.bannerImageUrl });
  return (
    <form id={id} className="space-y-5" onSubmit={(e) => {
      e.preventDefault();
      onSave({ title: title.trim(), company: company.trim(), location: location.trim(), type, description: description || undefined, applyUrl: applyUrl.trim() || undefined, deadline: deadline ? new Date(`${deadline}T23:59:00`).toISOString() : null, bannerImage: media.banner, removeBanner: media.input().removeBanner });
    }}>
      <FormSection title="The role">
        <Field id="jb-title" label="Job title"><Input id="jb-title" autoFocus required maxLength={150} value={title} onChange={(e) => setTitle(e.target.value)} /></Field>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field id="jb-co" label="Company"><Input id="jb-co" required maxLength={150} value={company} onChange={(e) => setCompany(e.target.value)} /></Field>
          <Field id="jb-loc" label="Where"><Input id="jb-loc" required maxLength={150} placeholder="City, or Remote" value={location} onChange={(e) => setLocation(e.target.value)} /></Field>
        </div>
        <Field id="jb-type" label="Type"><Choice label="Type" value={type} options={JOB_TYPES} onChange={setType} /></Field>
        <RichField id="jb-desc" label="About the role" optional placeholder="What the role involves and what you are looking for." minHeight={160} value={description} onChange={setDescription} />
      </FormSection>
      <FormSection title="Applying">
        <Field id="jb-url" label="Apply link" optional hint="People get a button that opens it. Leave it empty and they get none."><Input id="jb-url" type="url" inputMode="url" placeholder="https://" value={applyUrl} onChange={(e) => setApplyUrl(e.target.value)} /></Field>
        <Field id="jb-dl" label="Closes on" optional><Input id="jb-dl" type="date" value={deadline} onChange={(e) => setDeadline(e.target.value)} /></Field>
      </FormSection>
      <FormSection title="Picture" hint="Optional. A company logo or a photo of the place.">
        <BannerField media={media} id="jb-banner" label="Banner image" />
      </FormSection>
    </form>
  );
}

export function JobsSection({ group }: { group: ManageGroup }) {
  const qc = useQueryClient();
  const key = manageQueries.jobs(group).queryKey;
  const q = useQuery(manageQueries.jobs(group));
  const [editing, setEditing] = useState<GroupJob | "new" | null>(null);
  const [closing, setClosing] = useState<GroupJob | null>(null);
  const save = useMutation({
    mutationFn: ({ id, body }: { id?: string; body: SaveGroupJob }) => (id ? updateGroupJob(group, id, body) : createGroupJob(group, body)),
    onSuccess: (saved, { id }) => { qc.setQueryData<GroupJob[]>(key, (l) => upsertById(l, saved)); toast.success(id ? "Saved" : "Shared"); setEditing(null); },
  });
  const close = useMutation({
    mutationFn: (j: GroupJob) => closeGroupJob(group, j.id),
    onSuccess: (_r, j) => { qc.setQueryData<GroupJob[]>(key, (l) => (l ?? []).map((x) => (x.id === j.id ? { ...x, status: "Closed", canEdit: false } : x))); setClosing(null); toast.success("Job closed"); },
    onError: (e) => { setClosing(null); toast.error(errorText(e) ?? "That didn't work"); },
  });
  const open = (v: GroupJob | "new") => { save.reset(); setEditing(v); };
  const data = q.data ?? [];
  return (
    <Panel id="mg-jobs" title="Jobs and opportunities" summary={data.length > 0 ? `${data.length} ${data.length === 1 ? "job" : "jobs"}` : undefined} action={<Button size="sm" onClick={() => open("new")}>Share a job</Button>}>
      <ListState loading={q.isLoading} error={q.isError} onRetry={() => q.refetch()} empty={data.length === 0} emptyText="No jobs shared with this group yet.">
        <ul className="mt-2 divide-y divide-border">
          {data.map((j) => (
            <li key={j.id} className={`flex items-start gap-3 py-3 ${ROW}`}>
              <Thumb src={j.bannerImageUrl} alt="" />
              <div className="min-w-0 flex-1">
                <p className="text-[14px] font-semibold leading-snug">{j.title}{j.status === "Closed" && <span className="ml-2 rounded-full bg-secondary px-2 py-0.5 text-[11px] font-medium text-muted-foreground">Closed</span>}</p>
                <p className="mt-0.5 text-[12.5px] text-muted-foreground">{j.company} · {j.location} · {j.type}{j.deadline ? ` · closes ${formatDate(j.deadline)}` : ""}</p>
              </div>
              <ItemMenu label={j.title} onEdit={j.canEdit ? () => open(j) : undefined} onRemove={j.canEdit ? () => setClosing(j) : undefined} removeLabel="Close job" />
            </li>
          ))}
        </ul>
      </ListState>
      <FormSheet open={editing !== null} onClose={() => setEditing(null)} title={editing && editing !== "new" ? "Edit job" : "Share a job"} description={`For ${group.name}. Members are told about it.`}
        formId="manage-job-form" submitLabel={editing && editing !== "new" ? "Save changes" : "Share job"} saving={save.isPending} error={errorText(save.error)}>
        {editing !== null && <JobForm key={editing === "new" ? "new" : editing.id} id="manage-job-form" initial={editing === "new" ? null : editing}
          onSave={(body) => save.mutate({ id: editing === "new" ? undefined : editing.id, body })} />}
      </FormSheet>
      {closing && <Confirm title={`Close "${closing.title}"?`} message="People can no longer apply through the group. This can't be undone." confirmLabel="Close the job" busy={close.isPending} onConfirm={() => close.mutate(closing)} onCancel={() => setClosing(null)} />}
    </Panel>
  );
}

// ── Links ────────────────────────────────────────────────────────────────────

function LinkForm({ id, initial, onSave }: { id: string; initial: GroupResource | null; onSave: (b: SaveGroupResource) => void }) {
  const [title, setTitle] = useState(initial?.title ?? "");
  const [url, setUrl] = useState(initial?.externalUrl ?? "");
  const [category, setCategory] = useState(initial?.category && initial.category !== "General" ? initial.category : "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const media = useMedia({ banner: initial?.bannerImageUrl });
  return (
    <form id={id} className="space-y-5" onSubmit={(e) => {
      e.preventDefault();
      onSave({ title: title.trim(), externalUrl: url.trim(), category: category.trim() || undefined, description: description.trim() || undefined, bannerImage: media.banner, removeBanner: media.input().removeBanner });
    }}>
      <FormSection title="The link">
        <Field id="ln-title" label="Title"><Input id="ln-title" autoFocus required maxLength={150} value={title} onChange={(e) => setTitle(e.target.value)} /></Field>
        <Field id="ln-url" label="Address"><Input id="ln-url" type="url" inputMode="url" required placeholder="https://" value={url} onChange={(e) => setUrl(e.target.value)} /></Field>
        <Field id="ln-cat" label="Category" optional><Input id="ln-cat" maxLength={60} placeholder="e.g. Guides" value={category} onChange={(e) => setCategory(e.target.value)} /></Field>
        <Field id="ln-desc" label="What is it?" optional><Textarea id="ln-desc" rows={3} maxLength={500} value={description} onChange={(e) => setDescription(e.target.value)} /></Field>
      </FormSection>
      <FormSection title="Cover picture" hint="Optional. It shows beside the link in Resources.">
        <BannerField media={media} id="ln-banner" label="Cover image" />
      </FormSection>
    </form>
  );
}

export function LinksSection({ group }: { group: ManageGroup }) {
  const qc = useQueryClient();
  const key = manageQueries.links(group).queryKey;
  const q = useQuery(manageQueries.links(group));
  const [editing, setEditing] = useState<GroupResource | "new" | null>(null);
  const [removing, setRemoving] = useState<GroupResource | null>(null);
  const save = useMutation({
    mutationFn: ({ id, body }: { id?: string; body: SaveGroupResource }) => (id ? updateGroupResource(group, id, body) : createGroupResource(group, body)),
    onSuccess: (saved, { id }) => { qc.setQueryData<GroupResource[]>(key, (l) => upsertById(l, saved)); toast.success(id ? "Saved" : "Shared"); setEditing(null); },
  });
  const del = useMutation({
    mutationFn: (r: GroupResource) => deleteGroupResource(group, r.id),
    onSuccess: (_r, r) => { qc.setQueryData<GroupResource[]>(key, (l) => removeById(l, r.id)); setRemoving(null); toast.success("Removed"); },
    onError: (e) => { setRemoving(null); toast.error(errorText(e) ?? "That didn't work"); },
  });
  const open = (v: GroupResource | "new") => { save.reset(); setEditing(v); };
  const data = q.data ?? [];
  return (
    <Panel id="mg-links" title="Shared links" summary={data.length > 0 ? `${data.length} ${data.length === 1 ? "link" : "links"}` : undefined} action={<Button size="sm" onClick={() => open("new")}>Share a link</Button>}>
      <ListState loading={q.isLoading} error={q.isError} onRetry={() => q.refetch()} empty={data.length === 0} emptyText="No links shared with this group yet.">
        <ul className="mt-2 divide-y divide-border">
          {data.map((r) => (
            <li key={r.id} className={`flex items-start gap-3 py-3 ${ROW}`}>
              <Thumb src={r.bannerImageUrl} alt="" />
              <div className="min-w-0 flex-1">
                <p className="text-[14px] font-semibold leading-snug">{r.title}</p>
                <a href={r.externalUrl} target="_blank" rel="noopener noreferrer" className="mt-0.5 block truncate text-[12.5px] text-primary underline-offset-2 hover:underline">{r.externalUrl}</a>
              </div>
              <ItemMenu label={r.title} onEdit={r.canEdit ? () => open(r) : undefined} onRemove={r.canEdit ? () => setRemoving(r) : undefined} removeLabel="Remove" />
            </li>
          ))}
        </ul>
      </ListState>
      <FormSheet open={editing !== null} onClose={() => setEditing(null)} title={editing && editing !== "new" ? "Edit link" : "Share a link"} description={`Members of ${group.name} find it under Resources.`}
        formId="manage-link-form" submitLabel={editing && editing !== "new" ? "Save changes" : "Share link"} saving={save.isPending} error={errorText(save.error)}>
        {editing !== null && <LinkForm key={editing === "new" ? "new" : editing.id} id="manage-link-form" initial={editing === "new" ? null : editing}
          onSave={(body) => save.mutate({ id: editing === "new" ? undefined : editing.id, body })} />}
      </FormSheet>
      {removing && <Confirm title={`Remove "${removing.title}"?`} message="It disappears from Resources for everyone in the group." confirmLabel="Remove" busy={del.isPending} onConfirm={() => del.mutate(removing)} onCancel={() => setRemoving(null)} />}
    </Panel>
  );
}
