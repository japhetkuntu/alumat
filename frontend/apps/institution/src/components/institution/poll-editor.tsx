"use client";

import { memo, useCallback, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  AudienceScopePicker, Button, Card, CardContent, FormError, FormSection, Input, Label, Textarea, UserAvatar,
  ArrowUp, ArrowDown, Plus, X, Camera, Loader2, compressImage, cn, type AudienceMode,
} from "@alumni/ui";
import { createPoll, getCommunities, updatePoll, uploadImage, type PollDetail, type PollResultsMode, type SavePollBody } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { useInstitutionNavTheme } from "@/components/institution/institution-layout";
import { RESULTS_MODE_LABEL, toLocalInput } from "@/components/institution/poll-ui";

interface AspirantForm { key: number; name: string; details: string; photoUrl: string; memberId?: string }
interface PositionForm { key: number; title: string; description: string; aspirants: AspirantForm[] }

let nextKey = 1;
function swap<T>(arr: T[], i: number, dir: -1 | 1): T[] { const j = i + dir; if (j < 0 || j >= arr.length) return arr; const c = [...arr]; [c[i], c[j]] = [c[j], c[i]]; return c; }
const blankAspirant = (): AspirantForm => ({ key: nextKey++, name: "", details: "", photoUrl: "" });
const blankPosition = (): PositionForm => ({ key: nextKey++, title: "", description: "", aspirants: [blankAspirant()] });

function fromDetail(p: PollDetail): PositionForm[] {
  return p.positions.map((pos) => ({
    key: nextKey++, title: pos.title, description: pos.description ?? "",
    aspirants: pos.aspirants.map((a) => ({ key: nextKey++, name: a.name, details: a.details ?? "", photoUrl: a.photoUrl ?? "", memberId: a.memberId ?? undefined })),
  }));
}

/** One aspirant's photo: a round picture, tap to choose or change. Shown at once from the file chosen, then swapped for the uploaded link; the file is shrunk first, to what a small round picture needs, so uploads are quick even on a slow connection. */
function AspirantPhoto({ name, url, disabled, onChange, onBusy }: { name: string; url: string; disabled: boolean; onChange: (url: string) => void; onBusy: (delta: 1 | -1) => void }) {
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [preview, setPreview] = useState<string | null>(null);
  async function pick(file: File | undefined) {
    if (!file) return;
    const local = URL.createObjectURL(file);
    setPreview(local); setBusy(true); onBusy(1);
    try { onChange((await uploadImage(await compressImage(file, { maxDimension: 640, quality: 0.8, minBytes: 60 * 1024 }))).url); }
    catch (e) { toast.error(handleApiError(e)); }
    finally { setBusy(false); onBusy(-1); setPreview(null); URL.revokeObjectURL(local); if (input.current) input.current.value = ""; }
  }
  return (
    <div className="relative shrink-0">
      <button type="button" disabled={disabled || busy} onClick={() => input.current?.click()} aria-label={url ? `Change photo of ${name || "aspirant"}` : `Add photo of ${name || "aspirant"}`}
        className="block rounded-full focus:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed">
        <UserAvatar name={name || "?"} src={preview || url || undefined} size="xl" />
      </button>
      <span className="pointer-events-none absolute -bottom-0.5 -right-0.5 flex size-6 items-center justify-center rounded-full border border-border bg-background text-muted-foreground">
        {busy ? <Loader2 size={11} className="animate-spin" /> : <Camera size={11} />}
      </span>
      <input ref={input} type="file" accept="image/*" className="hidden" onChange={(e) => pick(e.target.files?.[0])} />
    </div>
  );
}

interface RowCallbacks {
  onAspirant: (pk: number, ak: number, patch: Partial<AspirantForm>) => void;
  onMoveAspirant: (pk: number, index: number, dir: -1 | 1) => void;
  onRemoveAspirant: (pk: number, ak: number) => void;
  onAddAspirant: (pk: number) => void;
  onBusy: (delta: 1 | -1) => void;
}

/**
 * One aspirant. Memoised, and fed only values that do not change while someone types elsewhere, so typing in one aspirant's details redraws
 * that row alone and not every row of a ballot that may hold dozens of them.
 */
const AspirantRow = memo(function AspirantRow({ posKey, a, index, count, locked, cb }: { posKey: number; a: AspirantForm; index: number; count: number; locked: boolean; cb: RowCallbacks }) {
  return (
    <div className="flex items-start gap-3 border border-border p-3">
      <AspirantPhoto name={a.name} url={a.photoUrl} disabled={locked} onBusy={cb.onBusy} onChange={(url) => cb.onAspirant(posKey, a.key, { photoUrl: url })} />
      <div className="min-w-0 flex-1 space-y-2">
        <Input value={a.name} maxLength={120} disabled={locked} onChange={(e) => cb.onAspirant(posKey, a.key, { name: e.target.value })} placeholder="Aspirant's name" aria-label={`Aspirant ${index + 1} name`} />
        <Textarea rows={3} value={a.details} maxLength={2000} disabled={locked} onChange={(e) => cb.onAspirant(posKey, a.key, { details: e.target.value })} placeholder="Who they are and what they stand for. Voters read this before choosing." aria-label={`About aspirant ${index + 1}`} />
      </div>
      {!locked && (
        <div className="flex shrink-0 flex-col">
          <button type="button" disabled={index === 0} onClick={() => cb.onMoveAspirant(posKey, index, -1)} aria-label="Move aspirant up" className="flex size-8 items-center justify-center text-muted-foreground hover:bg-muted disabled:opacity-30"><ArrowUp size={13} /></button>
          <button type="button" disabled={index === count - 1} onClick={() => cb.onMoveAspirant(posKey, index, 1)} aria-label="Move aspirant down" className="flex size-8 items-center justify-center text-muted-foreground hover:bg-muted disabled:opacity-30"><ArrowDown size={13} /></button>
          <button type="button" onClick={() => cb.onRemoveAspirant(posKey, a.key)} aria-label={`Remove ${a.name || "aspirant"}`} className="flex size-8 items-center justify-center text-destructive hover:bg-destructive/10"><X size={14} /></button>
        </div>
      )}
    </div>
  );
});

interface PositionCallbacks extends RowCallbacks {
  onPosition: (pk: number, patch: Partial<PositionForm>) => void;
  onMovePosition: (index: number, dir: -1 | 1) => void;
  onRemovePosition: (pk: number) => void;
}

const PositionCard = memo(function PositionCard({ p, index, total, locked, cb }: { p: PositionForm; index: number; total: number; locked: boolean; cb: PositionCallbacks }) {
  return (
    <Card className={cn(locked && "opacity-80")}>
      <CardContent className="space-y-4 p-4 sm:p-5">
        <div className="flex items-start gap-2">
          <div className="min-w-0 flex-1 space-y-2">
            <Label htmlFor={`pos-${p.key}`}>Position {index + 1}</Label>
            <Input id={`pos-${p.key}`} value={p.title} maxLength={120} disabled={locked} onChange={(e) => cb.onPosition(p.key, { title: e.target.value })} placeholder="e.g. President" />
          </div>
          {!locked && (
            <div className="flex shrink-0 gap-0.5 pt-6">
              <button type="button" disabled={index === 0} onClick={() => cb.onMovePosition(index, -1)} aria-label="Move position up" className="flex size-9 items-center justify-center text-muted-foreground hover:bg-muted disabled:opacity-30"><ArrowUp size={14} /></button>
              <button type="button" disabled={index === total - 1} onClick={() => cb.onMovePosition(index, 1)} aria-label="Move position down" className="flex size-9 items-center justify-center text-muted-foreground hover:bg-muted disabled:opacity-30"><ArrowDown size={14} /></button>
              <button type="button" onClick={() => cb.onRemovePosition(p.key)} aria-label={`Remove ${p.title || "position"}`} className="flex size-9 items-center justify-center text-destructive hover:bg-destructive/10"><X size={15} /></button>
            </div>
          )}
        </div>
        <Input value={p.description} maxLength={500} disabled={locked} onChange={(e) => cb.onPosition(p.key, { description: e.target.value })} placeholder="What this role does (optional)" aria-label={`About ${p.title || "this position"}`} />

        <p className="text-[12.5px] font-semibold text-muted-foreground">
          {p.aspirants.length === 1 ? "Members will vote Yes, No or Abstain on this aspirant" : p.aspirants.length > 1 ? "Members will choose one of these" : "Add at least one aspirant"}
        </p>
        <div className="space-y-3">
          {p.aspirants.map((a, ai) => <AspirantRow key={a.key} posKey={p.key} a={a} index={ai} count={p.aspirants.length} locked={locked} cb={cb} />)}
        </div>
        {!locked && <Button type="button" variant="outline" size="sm" onClick={() => cb.onAddAspirant(p.key)}><Plus size={13} className="mr-1.5" /> Add an aspirant</Button>}
      </CardContent>
    </Card>
  );
});

const SHORTCUTS = [{ label: "1 day", hours: 24 }, { label: "3 days", hours: 72 }, { label: "1 week", hours: 168 }];

/** Create or edit a poll. Once voting has started the ballot and audience are shown but cannot be changed. */
export function PollEditor({ poll }: { poll?: PollDetail }) {
  const router = useRouter();
  const qc = useQueryClient();
  const { data: navTheme } = useInstitutionNavTheme();
  const { data: communities = [] } = useQuery({ queryKey: ["communities"], queryFn: getCommunities, staleTime: 60_000 });
  const locked = !!poll?.locked;
  const opened = poll ? poll.state === "Open" : false;

  const [title, setTitle] = useState(poll?.title ?? "");
  const [description, setDescription] = useState(poll?.description ?? "");
  const [opensAt, setOpensAt] = useState(poll ? toLocalInput(poll.opensAt) : toLocalInput(new Date(Date.now() + 3600_000).toISOString()));
  const [closesAt, setClosesAt] = useState(poll ? toLocalInput(poll.closesAt) : toLocalInput(new Date(Date.now() + 3600_000 + 72 * 3600_000).toISOString()));
  const [mode, setMode] = useState<AudienceMode>(poll?.communityId ? "community" : poll?.yearGroups?.length ? "yearGroups" : "everyone");
  const [communityId, setCommunityId] = useState(poll?.communityId ?? "");
  const [yearGroups, setYearGroups] = useState<number[]>(poll?.yearGroups ?? []);
  const [resultsMode, setResultsMode] = useState<PollResultsMode>(poll?.resultsMode ?? "AfterClose");
  const [positions, setPositions] = useState<PositionForm[]>(poll ? fromDetail(poll) : [blankPosition()]);
  const [error, setError] = useState<string | null>(null);

  const save = useMutation({
    onMutate: () => setError(null),
    mutationFn: () => {
      const body: SavePollBody = {
        title: title.trim(), description: description.trim() || undefined,
        opensAt: new Date(opensAt).toISOString(), closesAt: new Date(closesAt).toISOString(),
        yearGroups: mode === "yearGroups" ? yearGroups : undefined, communityId: mode === "community" ? communityId : undefined,
        resultsMode,
        ...(locked ? {} : { positions: positions.map((p) => ({ title: p.title.trim(), description: p.description.trim() || undefined,
          aspirants: p.aspirants.map((a) => ({ name: a.name.trim(), details: a.details.trim() || undefined, photoUrl: a.photoUrl || undefined, memberId: a.memberId })) })) }),
      };
      return poll ? updatePoll(poll.id, body) : createPoll(body);
    },
    onSuccess: (saved) => {
      qc.invalidateQueries({ queryKey: ["polls"] });
      qc.invalidateQueries({ queryKey: ["poll", saved.id] });
      toast.success(poll ? "Poll updated" : "Saved as a draft. Publish it when you are ready.");
      router.push(`/polls/${saved.id}`);
    },
    onError: (e) => { const m = handleApiError(e); setError(m); toast.error(m); },
  });

  const [uploading, setUploading] = useState(0);
  const cb = useMemo<PositionCallbacks>(() => ({
    onBusy: (d) => setUploading((n) => n + d),
    onPosition: (pk, patch) => setPositions((ps) => ps.map((p) => (p.key === pk ? { ...p, ...patch } : p))),
    onMovePosition: (i, dir) => setPositions((ps) => swap(ps, i, dir)),
    onRemovePosition: (pk) => setPositions((ps) => ps.filter((x) => x.key !== pk)),
    onAspirant: (pk, ak, patch) => setPositions((ps) => ps.map((p) => (p.key === pk ? { ...p, aspirants: p.aspirants.map((a) => (a.key === ak ? { ...a, ...patch } : a)) } : p))),
    onMoveAspirant: (pk, i, dir) => setPositions((ps) => ps.map((p) => (p.key === pk ? { ...p, aspirants: swap(p.aspirants, i, dir) } : p))),
    onRemoveAspirant: (pk, ak) => setPositions((ps) => ps.map((p) => (p.key === pk ? { ...p, aspirants: p.aspirants.filter((a) => a.key !== ak) } : p))),
    onAddAspirant: (pk) => setPositions((ps) => ps.map((p) => (p.key === pk ? { ...p, aspirants: [...p.aspirants, blankAspirant()] } : p))),
  }), []);

  const when = (hours: number) => { const o = new Date(opensAt); if (!isNaN(o.getTime())) setClosesAt(toLocalInput(new Date(o.getTime() + hours * 3600_000).toISOString())); };

  return (
    <form className="mx-auto max-w-[900px] space-y-6 p-4 pb-28 sm:p-[26px] sm:pb-28" onSubmit={(e) => { e.preventDefault(); save.mutate(); }}>
      <div>
        <h1 className="m-0 text-[20px] font-bold sm:text-[25px]">{poll ? "Edit poll" : "New poll"}</h1>
        <p className="mt-1.5 text-[13px] text-muted-foreground">
          {locked ? "Voting has started. The ballot and audience are fixed now; you can still change the wording and the closing time." : "Nothing reaches members until you publish. You can leave this and come back."}
        </p>
      </div>

      <FormSection title="The poll">
        <div className="space-y-2">
          <Label htmlFor="poll-title">Title</Label>
          <Input id="poll-title" value={title} maxLength={150} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. 2027 executive elections" required />
        </div>
        <div className="space-y-2">
          <Label htmlFor="poll-desc">What members should know <span className="font-normal text-muted-foreground">(optional)</span></Label>
          <Textarea id="poll-desc" rows={3} value={description} onChange={(e) => setDescription(e.target.value)} placeholder="How the election works, who is running, and anything voters should read first." />
        </div>
      </FormSection>

      <FormSection title="When voting happens" hint="Times are in your own time zone. A poll opens and closes on its own at these times.">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="poll-opens">Opens</Label>
            <Input id="poll-opens" type="datetime-local" value={opensAt} onChange={(e) => setOpensAt(e.target.value)} disabled={opened || locked} required />
            {(opened || locked) && <p className="text-[12px] text-muted-foreground">Voting has already opened.</p>}
          </div>
          <div className="space-y-2">
            <Label htmlFor="poll-closes">Closes</Label>
            <Input id="poll-closes" type="datetime-local" value={closesAt} onChange={(e) => setClosesAt(e.target.value)} required />
          </div>
        </div>
        {!locked && !opened && (
          <div className="flex flex-wrap items-center gap-2" role="group" aria-label="Voting length">
            <span className="text-[12.5px] text-muted-foreground">Open for</span>
            {SHORTCUTS.map((s) => (
              <button key={s.label} type="button" onClick={() => when(s.hours)} className="border border-border px-3 py-1.5 text-[12.5px] font-medium text-muted-foreground hover:border-primary hover:text-foreground">{s.label}</button>
            ))}
          </div>
        )}
      </FormSection>

      <FormSection title="Who can vote" hint="Only active members in this audience see the poll and can vote in it.">
        {locked ? (
          <p className="text-[13.5px]">Fixed while voting is under way.</p>
        ) : (
          <AudienceScopePicker
            mode={mode} onModeChange={setMode} communityId={communityId} onCommunityChange={setCommunityId}
            communities={communities.filter((c) => c.isActive).map((c) => ({ id: c.id, name: c.name }))}
            yearGroups={yearGroups} onYearGroupsChange={setYearGroups}
            supportsCommunity hideYearGroups={navTheme?.organizationType === "Community"}
          />
        )}
      </FormSection>

      <FormSection title="When members see the results" hint="You can always see turnout. The figures themselves are only shown to you once voting has closed, so nobody watches a vote while it is being cast.">
        <div className="grid gap-2.5" role="radiogroup" aria-label="When results show">
          {(Object.keys(RESULTS_MODE_LABEL) as PollResultsMode[]).map((m) => (
            <label key={m} className={cn("flex cursor-pointer items-start gap-3 border p-3.5 transition-colors", resultsMode === m ? "border-primary bg-primary/5" : "border-border hover:border-primary/40", poll?.resultsPublishedAt && "cursor-not-allowed opacity-60")}>
              <input type="radio" name="results-mode" className="mt-1 size-4 accent-[var(--primary)]" checked={resultsMode === m} disabled={!!poll?.resultsPublishedAt} onChange={() => setResultsMode(m)} />
              <span>
                <span className="block text-[14px] font-semibold">{RESULTS_MODE_LABEL[m].title}{m === "AfterClose" && <span className="ml-2 text-[11.5px] font-medium text-muted-foreground">Recommended</span>}</span>
                <span className="mt-0.5 block text-[13px] text-muted-foreground">{RESULTS_MODE_LABEL[m].hint}</span>
              </span>
            </label>
          ))}
        </div>
      </FormSection>

      <FormSection title="The ballot" hint="Add each position, then the aspirants for it. One aspirant makes it a Yes / No vote (with Abstain). Two or more, members choose one.">
        {locked && <p className="border border-border bg-muted/40 px-3.5 py-3 text-[13px] text-muted-foreground">The ballot is locked because members have voted.</p>}
        <div className="space-y-4">
          {positions.map((p, pi) => <PositionCard key={p.key} p={p} index={pi} total={positions.length} locked={locked} cb={cb} />)}
        </div>
        {!locked && <Button type="button" variant="outline" onClick={() => setPositions((ps) => [...ps, blankPosition()])}><Plus size={14} className="mr-1.5" /> Add a position</Button>}
      </FormSection>

      <FormError message={error} />

      <div className="fixed inset-x-0 bottom-0 z-30 border-t border-border bg-background/95 px-4 py-3 backdrop-blur sm:left-[var(--sidebar-width,0px)]">
        <div className="mx-auto flex max-w-[900px] items-center justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => router.push(poll ? `/polls/${poll.id}` : "/polls")}>Cancel</Button>
          <Button type="submit" isLoading={save.isPending} disabled={uploading > 0}>{uploading > 0 ? "Uploading photos…" : poll ? "Save changes" : "Save as draft"}</Button>
        </div>
      </div>
    </form>
  );
}
