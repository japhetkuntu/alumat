"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button, FormSection, FormSheet, Input, UserAvatar, formatCurrencyFit, formatDate } from "@alumni/ui";
import { closeGroupCampaign, createGroupCampaign, getGroupGifts, updateGroupCampaign, type GroupCampaign, type ManageGroup, type SaveGroupCampaign } from "@/lib/member-api";
import { BannerField, GalleryField, Thumb, VideoField, useMedia } from "@/components/member/manage-media";
import { Confirm, Field, ItemMenu, ListState, Panel, ROW, RichField, errorText } from "@/components/member/manage-ui";
import { manageQueries, upsertById } from "@/components/member/manage-queries";

function CampaignForm({ id, initial, onSave }: { id: string; initial: GroupCampaign | null; onSave: (b: SaveGroupCampaign) => void }) {
  const [title, setTitle] = useState(initial?.title ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [target, setTarget] = useState(initial ? String(initial.targetAmount) : "");
  const [each, setEach] = useState(initial ? String(initial.amountPerMember) : "");
  const [deadline, setDeadline] = useState(initial ? initial.deadline.slice(0, 10) : "");
  // Once someone has given, what each person was asked for is what they agreed to, so it no longer changes.
  const locked = !!initial && (initial.paidCount > 0 || initial.collectedAmount > 0);
  const media = useMedia({ banner: initial?.bannerImageUrl, videos: initial?.youtubeVideoUrl ? [initial.youtubeVideoUrl] : [] });
  return (
    <form id={id} className="space-y-5" onSubmit={(e) => {
      e.preventDefault();
      onSave({ title: title.trim(), description: description || undefined, targetAmount: Number(target), amountPerMember: Number(each), deadline: new Date(`${deadline}T23:59:00`).toISOString(), bannerImage: media.banner, removeBanner: media.input().removeBanner, youtubeVideoUrls: media.input().youtubeVideoUrls });
    }}>
      <FormSection title="The fundraiser">
        <Field id="fr-title" label="What is it for?"><Input id="fr-title" autoFocus required maxLength={200} placeholder="e.g. Library fund" value={title} onChange={(e) => setTitle(e.target.value)} /></Field>
        <RichField id="fr-desc" label="Why are you raising it?" optional placeholder="Tell the group what the money will do." value={description} onChange={setDescription} />
      </FormSection>
      <FormSection title="The goal">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field id="fr-target" label="Goal"><Input id="fr-target" type="number" inputMode="decimal" required min={1} value={target} onChange={(e) => setTarget(e.target.value)} /></Field>
          <Field id="fr-each" label="Asked of each member" hint={locked ? "Someone has already given, so this can't change." : undefined}>
            <Input id="fr-each" type="number" inputMode="decimal" required min={1} disabled={locked} value={each} onChange={(e) => setEach(e.target.value)} /></Field>
        </div>
        <Field id="fr-dl" label="Closes on"><Input id="fr-dl" type="date" required value={deadline} onChange={(e) => setDeadline(e.target.value)} /></Field>
      </FormSection>
      <FormSection title="Picture and video" hint="Optional. They appear with the fundraiser when members see it.">
        <BannerField media={media} id="fr-banner" />
        <VideoField media={media} id="fr-video" label="YouTube link" single />
      </FormSection>
      <FormSection title="Where the money goes" hint="Gifts go to the account your administrators have set up for the group. You don't add any account details.">
        <span className="sr-only">Handled by administrators</span>
      </FormSection>
    </form>
  );
}

export function GiftsSheet({ group, campaign, onClose }: { group: ManageGroup; campaign: GroupCampaign | null; onClose: () => void }) {
  const q = useQuery({
    queryKey: ["manage-gifts", group.scopeType, group.scopeId, campaign?.id],
    queryFn: () => getGroupGifts(group, campaign!.id),
    enabled: !!campaign, staleTime: 30_000,
  });
  const gifts = q.data ?? [];
  return (
    <FormSheet open={!!campaign} onClose={onClose} title="Who has given" description={campaign?.title} formId="manage-gifts" hideSubmit cancelLabel="Close">
      <ListState loading={q.isLoading} error={q.isError} onRetry={() => q.refetch()} empty={gifts.length === 0} emptyText="Nobody has given yet.">
        <ul className="divide-y divide-border">
          {gifts.map((g, i) => (
            <li key={i} className={`flex items-center gap-3 py-3 ${ROW}`}>
              <UserAvatar src={g.giverPhotoUrl} name={g.giverName} size="default" />
              <div className="min-w-0 flex-1">
                <p className="truncate text-[14px] font-semibold leading-tight">{g.giverName}</p>
                <p className="truncate text-[12.5px] text-muted-foreground">{formatDate(g.givenAt)} · {g.method}{g.status !== "Received" ? ` · ${g.status}` : ""}</p>
              </div>
              <p className="shrink-0 text-[14px] font-semibold tabular-nums">{formatCurrencyFit(g.amount)}</p>
            </li>
          ))}
        </ul>
      </ListState>
    </FormSheet>
  );
}

export function FundraisersSection({ group }: { group: ManageGroup }) {
  const qc = useQueryClient();
  const key = manageQueries.campaigns(group).queryKey;
  const q = useQuery(manageQueries.campaigns(group));
  const canRun = group.privileges.includes("ManageFundraisers");
  const [editing, setEditing] = useState<GroupCampaign | "new" | null>(null);
  const [closing, setClosing] = useState<GroupCampaign | null>(null);
  const [viewing, setViewing] = useState<GroupCampaign | null>(null);
  const save = useMutation({
    mutationFn: ({ id, body }: { id?: string; body: SaveGroupCampaign }) => (id ? updateGroupCampaign(group, id, body) : createGroupCampaign(group, body)),
    onSuccess: (saved, { id }) => { qc.setQueryData<GroupCampaign[]>(key, (l) => upsertById(l, { ...saved, canSeeGiving: group.privileges.includes("ViewGiving") })); toast.success(id ? "Saved" : "Fundraiser started"); setEditing(null); },
  });
  const close = useMutation({
    mutationFn: (c: GroupCampaign) => closeGroupCampaign(group, c.id),
    onSuccess: (_r, c) => { qc.setQueryData<GroupCampaign[]>(key, (l) => (l ?? []).map((x) => (x.id === c.id ? { ...x, status: "Closed", canEdit: false } : x))); setClosing(null); toast.success("Fundraiser closed"); },
    onError: (e) => { setClosing(null); toast.error(errorText(e) ?? "That didn't work"); },
  });
  const open = (v: GroupCampaign | "new") => { save.reset(); setEditing(v); };
  const data = q.data ?? [];

  return (
    <Panel id="mg-funds" title="Fundraisers" summary={data.length > 0 ? `${data.length} ${data.length === 1 ? "fundraiser" : "fundraisers"}` : undefined} action={canRun ? <Button size="sm" onClick={() => open("new")}>Start a fundraiser</Button> : undefined}>
      <p className="mt-1 text-[12.5px] leading-relaxed text-muted-foreground">Gifts go to the account your administrators have set up for this group.</p>
      <ListState loading={q.isLoading} error={q.isError} onRetry={() => q.refetch()} empty={data.length === 0} emptyText="No fundraisers for this group yet.">
        <ul className="mt-2 divide-y divide-border">
          {data.map((c) => {
            const pct = c.targetAmount > 0 ? Math.min(100, Math.round((c.collectedAmount / c.targetAmount) * 100)) : 0;
            return (
              <li key={c.id} className={`py-3.5 ${ROW}`}>
                <div className="flex items-start gap-3">
                  <Thumb src={c.bannerImageUrl} alt="" />
                  <div className="min-w-0 flex-1">
                    <p className="text-[14px] font-semibold leading-snug">{c.title}{c.status !== "Active" && <span className="ml-2 rounded-full bg-secondary px-2 py-0.5 text-[11px] font-medium text-muted-foreground">{c.status}</span>}</p>
                    <p className="mt-0.5 text-[12.5px] text-muted-foreground">{formatCurrencyFit(c.collectedAmount)} of {formatCurrencyFit(c.targetAmount)} · {c.paidCount} {c.paidCount === 1 ? "gift" : "gifts"} · {c.status === "Active" ? `closes ${formatDate(c.deadline)}` : `closed ${formatDate(c.deadline)}`}</p>
                  </div>
                  <ItemMenu label={c.title} onEdit={c.canEdit ? () => open(c) : undefined} onRemove={c.canEdit ? () => setClosing(c) : undefined} removeLabel="Close fundraiser" />
                </div>
                <div className="mt-2.5 h-1.5 overflow-hidden rounded-full bg-muted" role="progressbar" aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100} aria-label={`${c.title} progress`}>
                  <div className="h-full rounded-full bg-primary transition-[width] duration-500" style={{ width: `${pct}%` }} />
                </div>
                {c.canSeeGiving && <button type="button" onClick={() => setViewing(c)} className="mt-2 min-h-10 text-[13px] font-semibold text-primary underline-offset-2 hover:underline">Who has given</button>}
              </li>
            );
          })}
        </ul>
      </ListState>
      <FormSheet open={editing !== null} onClose={() => setEditing(null)} title={editing && editing !== "new" ? "Edit fundraiser" : "Start a fundraiser"} description={`For ${group.name}. Members are told about it.`}
        formId="manage-campaign-form" submitLabel={editing && editing !== "new" ? "Save changes" : "Start fundraiser"} saving={save.isPending} error={errorText(save.error)}>
        {editing !== null && <CampaignForm key={editing === "new" ? "new" : editing.id} id="manage-campaign-form" initial={editing === "new" ? null : editing}
          onSave={(body) => save.mutate({ id: editing === "new" ? undefined : editing.id, body })} />}
      </FormSheet>
      <GiftsSheet group={group} campaign={viewing} onClose={() => setViewing(null)} />
      {closing && <Confirm title={`Close "${closing.title}"?`} message="People can no longer give to it. What has been given stays. This can't be undone." confirmLabel="Close the fundraiser" busy={close.isPending} onConfirm={() => close.mutate(closing)} onCancel={() => setClosing(null)} />}
    </Panel>
  );
}
