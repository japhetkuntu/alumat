"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Plus, Trash2, Users } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { cn } from "@alumni/ui";
import {
  deleteSegment, emptySegmentCriteria, getCommunities, getSegments, previewSegment, saveSegment,
  type Segment, type SegmentCriteria,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";

const STATUSES = ["Active", "Pending", "Suspended"];

/** A small toggle chip, used for picking groups and kinds. */
function Chip({ on, onClick, children }: { on: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      aria-pressed={on}
      onClick={onClick}
      className={cn(
        "border px-3 py-1.5 text-[13px] font-medium transition-colors",
        on ? "border-primary bg-primary text-primary-foreground" : "border-border text-muted-foreground hover:text-foreground",
      )}
    >
      {children}
    </button>
  );
}

function toggle(list: string[], value: string) {
  return list.includes(value) ? list.filter((x) => x !== value) : [...list, value];
}

/** One sentence describing a rule, so a list of audiences can be read at a glance. */
function describe(c: SegmentCriteria, groupNames: Record<string, string>): string {
  const parts: string[] = [];
  parts.push(c.status ? `${c.status} members` : "All members");
  if (c.groupKinds.length) parts.push(`in any ${c.groupKinds.join(" or ")}`);
  if (c.groupIds.length) parts.push(`in ${c.groupIds.map((id) => groupNames[id] ?? "a removed group").join(", ")}`);
  if (c.graduationYearFrom || c.graduationYearTo) parts.push(`graduated ${c.graduationYearFrom ?? "…"} to ${c.graduationYearTo ?? "…"}`);
  if (c.duesStatus) parts.push(`dues ${c.duesStatus.toLowerCase()} this year`);
  if (c.answerContains) parts.push(`answered “${c.answerContains}”`);
  if (c.dormantOnly) parts.push("not signed in for 60 days");
  if (c.joinedWithinDays) parts.push(`joined in the last ${c.joinedWithinDays} days`);
  return parts.join(", ");
}

function Editor({ initial, groups, onDone }: {
  initial: Segment | null;
  groups: { id: string; name: string; kind?: string | null }[];
  onDone: () => void;
}) {
  const qc = useQueryClient();
  const [name, setName] = useState(initial?.name ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [c, setC] = useState<SegmentCriteria>(initial?.criteria ?? emptySegmentCriteria());
  const kinds = Array.from(new Set(groups.map((g) => g.kind).filter((k): k is string => !!k))).sort();
  const set = (patch: Partial<SegmentCriteria>) => setC((cur) => ({ ...cur, ...patch }));

  // The count follows the rule as it is edited, so nobody saves an audience without knowing who is in it.
  const [debounced, setDebounced] = useState(c);
  useEffect(() => { const t = setTimeout(() => setDebounced(c), 350); return () => clearTimeout(t); }, [c]);
  const preview = useQuery({
    queryKey: ["segment-preview", debounced],
    queryFn: () => previewSegment(debounced),
    placeholderData: (prev) => prev,
  });

  const save = useMutation({
    mutationFn: () => saveSegment(initial?.id ?? null, { name: name.trim(), description: description.trim() || undefined, criteria: c }),
    onSuccess: () => {
      toast.success("Audience saved");
      qc.invalidateQueries({ queryKey: ["segments"] });
      qc.invalidateQueries({ queryKey: ["report-segments"] });
      onDone();
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  return (
    <Card>
      <CardContent className="space-y-5 p-5">
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="seg-name">Name</Label>
            <Input id="seg-name" value={name} maxLength={80} onChange={(e) => setName(e.target.value)} placeholder="e.g. Kumasi chapter, dues unpaid" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="seg-desc">Note (optional)</Label>
            <Input id="seg-desc" value={description} maxLength={300} onChange={(e) => setDescription(e.target.value)} placeholder="What you use it for" />
          </div>
        </div>

        <div className="space-y-2">
          <Label>Member status</Label>
          <div className="flex flex-wrap gap-2">
            <Chip on={!c.status} onClick={() => set({ status: null })}>Any</Chip>
            {STATUSES.map((s) => <Chip key={s} on={c.status === s} onClick={() => set({ status: s })}>{s}</Chip>)}
          </div>
        </div>

        {kinds.length > 0 && (
          <div className="space-y-2">
            <Label>Any group of this kind</Label>
            <div className="flex flex-wrap gap-2">
              {kinds.map((k) => <Chip key={k} on={c.groupKinds.includes(k)} onClick={() => set({ groupKinds: toggle(c.groupKinds, k) })}>{k}</Chip>)}
            </div>
          </div>
        )}

        {groups.length > 0 && (
          <div className="space-y-2">
            <Label>Or these specific groups</Label>
            <div className="flex flex-wrap gap-2">
              {groups.map((g) => <Chip key={g.id} on={c.groupIds.includes(g.id)} onClick={() => set({ groupIds: toggle(c.groupIds, g.id) })}>{g.name}</Chip>)}
            </div>
          </div>
        )}

        <div className="grid gap-3 sm:grid-cols-4">
          <div className="space-y-1.5">
            <Label htmlFor="seg-from">Graduated from</Label>
            <Input id="seg-from" type="number" value={c.graduationYearFrom ?? ""} onChange={(e) => set({ graduationYearFrom: e.target.value ? Number(e.target.value) : null })} placeholder="e.g. 2010" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="seg-to">Graduated to</Label>
            <Input id="seg-to" type="number" value={c.graduationYearTo ?? ""} onChange={(e) => set({ graduationYearTo: e.target.value ? Number(e.target.value) : null })} placeholder="e.g. 2020" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="seg-joined">Joined in the last (days)</Label>
            <Input id="seg-joined" type="number" min={1} value={c.joinedWithinDays ?? ""} onChange={(e) => set({ joinedWithinDays: e.target.value ? Number(e.target.value) : null })} placeholder="e.g. 30" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="seg-answer">Answers contain</Label>
            <Input id="seg-answer" value={c.answerContains ?? ""} maxLength={80} onChange={(e) => set({ answerContains: e.target.value || null })} placeholder="e.g. Nurse" />
          </div>
        </div>

        <div className="space-y-2">
          <Label>This year&apos;s membership dues</Label>
          <div className="flex flex-wrap gap-2">
            <Chip on={!c.duesStatus} onClick={() => set({ duesStatus: null })}>Any</Chip>
            <Chip on={c.duesStatus === "Paid"} onClick={() => set({ duesStatus: "Paid" })}>Paid</Chip>
            <Chip on={c.duesStatus === "Unpaid"} onClick={() => set({ duesStatus: "Unpaid" })}>Not paid</Chip>
          </div>
        </div>

        <label className="flex items-center gap-2 text-[13.5px]">
          <input type="checkbox" className="h-4 w-4" checked={c.dormantOnly} onChange={(e) => set({ dormantOnly: e.target.checked })} />
          Only members who haven&apos;t signed in for 60 days
        </label>

        <div className="border border-border bg-muted/30 p-3.5" aria-live="polite">
          <p className="flex items-center gap-2 text-[14px] font-semibold">
            <Users size={14} />
            {preview.isLoading ? "Counting…" : `${preview.data?.count ?? 0} ${preview.data?.count === 1 ? "member" : "members"} right now`}
          </p>
          {(preview.data?.sample.length ?? 0) > 0 && (
            <p className="mt-1 text-[12.5px] text-muted-foreground">
              Including {preview.data!.sample.map((m) => m.name).join(", ")}{preview.data!.count > preview.data!.sample.length ? "…" : ""}
            </p>
          )}
          <p className="mt-1 text-[12px] text-muted-foreground">An audience is a rule, so it always reflects who matches today.</p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button isLoading={save.isPending} disabled={!name.trim()} onClick={() => save.mutate()}>Save audience</Button>
          <Button variant="outline" onClick={onDone}>Cancel</Button>
        </div>
      </CardContent>
    </Card>
  );
}

export default function SegmentsPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";
  const qc = useQueryClient();
  const [editing, setEditing] = useState<Segment | "new" | null>(null);
  const [removing, setRemoving] = useState<Segment | null>(null);

  const segments = useQuery({ queryKey: ["segments"], queryFn: getSegments, enabled: isSuperAdmin });
  const groups = useQuery({ queryKey: ["segment-groups"], queryFn: getCommunities, enabled: isSuperAdmin });
  const groupList = (groups.data ?? []).filter((g) => g.isActive);
  const groupNames = Object.fromEntries((groups.data ?? []).map((g) => [g.id, g.name]));

  const remove = useMutation({
    mutationFn: (id: string) => deleteSegment(id),
    onSuccess: () => { toast.success("Audience deleted"); setRemoving(null); qc.invalidateQueries({ queryKey: ["segments"] }); qc.invalidateQueries({ queryKey: ["report-segments"] }); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (!isSuperAdmin) {
    return <div className="p-4 sm:p-6"><EmptyState title="Saved audiences are managed by full administrators" description="Ask a full administrator to set one up for you." /></div>;
  }

  return (
    <div className="mx-auto max-w-[1100px] space-y-5 p-4 sm:p-[26px]">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="m-0 text-[20px] font-bold sm:text-[25px]">Saved audiences</h1>
          <p className="mt-1.5 text-[13px] text-muted-foreground">
            Define a group of members once, by chapter, dues, year or answers, then reuse it for broadcasts and reports. An audience is a rule, so it always matches who fits today.
          </p>
        </div>
        {editing === null && <Button onClick={() => setEditing("new")}><Plus size={15} className="mr-1.5" />New audience</Button>}
      </div>

      {editing !== null && (
        <Editor
          key={editing === "new" ? "new" : editing.id}
          initial={editing === "new" ? null : editing}
          groups={groupList}
          onDone={() => setEditing(null)}
        />
      )}

      {segments.isLoading ? null : (segments.data ?? []).length === 0 && editing === null ? (
        <EmptyState
          icon={<Users size={40} />}
          title="No saved audiences yet"
          description="Try one like “Kumasi chapter, dues unpaid” or “Joined in the last 30 days”, then send it a broadcast or pull a report."
        />
      ) : (
        <ul className="space-y-2.5">
          {(segments.data ?? []).map((s) => (
            <li key={s.id} className="flex flex-wrap items-start justify-between gap-3 border border-border bg-card p-4">
              <div className="min-w-0">
                <p className="text-[15px] font-semibold">{s.name}</p>
                <p className="mt-0.5 text-[13px] text-muted-foreground">{describe(s.criteria, groupNames)}</p>
                {s.description && <p className="mt-1 text-[12.5px] text-muted-foreground">{s.description}</p>}
              </div>
              <div className="flex shrink-0 gap-2">
                <Button size="sm" variant="outline" onClick={() => setEditing(s)}>Edit</Button>
                <Button size="sm" variant="ghost" aria-label={`Delete ${s.name}`} onClick={() => setRemoving(s)}><Trash2 size={14} /></Button>
              </div>
            </li>
          ))}
        </ul>
      )}

      <ConfirmModal
        open={!!removing}
        onCancel={() => setRemoving(null)}
        onConfirm={() => removing && remove.mutate(removing.id)}
        title="Delete this audience?"
        message={`“${removing?.name}” will be removed. Broadcasts and reports already sent are not affected.`}
        confirmLabel="Delete"
        isLoading={remove.isPending}
        variant="destructive"
      />
    </div>
  );
}
