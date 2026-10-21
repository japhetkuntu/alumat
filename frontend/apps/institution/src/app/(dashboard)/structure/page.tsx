"use client";

import * as React from "react";
import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  Button, Card, CardContent, ChevronDown, Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle, FormError, Input, Label, LoadError, Plus, Search, Sitemap, UserAvatar, X, cn,
} from "@alumni/ui";
import {
  createBatch, createCommunity, createInstitutionStaff, getStructure, updateInstitutionStaff,
  type InstitutionStructure, type StructureAdmin, type StructureCohort, type StructureCommunity,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { DEFAULT_COHORT_LABEL, DEFAULT_COHORT_LABEL_PLURAL } from "@/components/institution/institution-layout";

type Node = { kind: "cohort"; data: StructureCohort } | { kind: "community"; data: StructureCommunity };

/** The administrators responsible for one node: scoped administrators whose scope names it. */
function adminsOf(s: InstitutionStructure, node: Node): StructureAdmin[] {
  return s.admins.filter((a) => a.role === "ScopedAdmin" && !a.isDisabled
    && (node.kind === "cohort" ? a.yearGroups.includes(node.data.year) : a.communityIds.includes(node.data.id)));
}

function Person({ admin, small }: { admin: StructureAdmin; small?: boolean }) {
  return (
    <span className="inline-flex min-w-0 items-center gap-1.5">
      <UserAvatar name={admin.name} size={small ? "sm" : "default"} />
      <span className={cn("truncate font-semibold", small ? "text-[12.5px]" : "text-[13.5px]")}>{admin.firstName || admin.name}</span>
    </span>
  );
}

/** One cohort or group as a single row: what it is on the left, who looks after it on the right. A dashed amber edge means nobody does. */
function NodeCard({ node, admins, onOpen }: { node: Node; admins: StructureAdmin[]; onOpen: () => void }) {
  const unmanaged = admins.length === 0;
  const count = node.data.memberCount;
  const quiet = !node.data.isActive;
  const leaders = node.kind === "community" ? node.data.leaders : [];
  return (
    <button
      type="button"
      onClick={onOpen}
      className={cn(
        "flex w-full flex-wrap items-center gap-x-4 gap-y-1.5 border bg-card px-3.5 py-2.5 text-left transition-colors hover:bg-muted/50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary",
        unmanaged ? "border-dashed border-muted-foreground/50" : "border-border",
        quiet && "opacity-70",
      )}
    >
      <span className="min-w-0 flex-1 basis-48">
        <span className="block truncate text-[14px] font-semibold">{node.data.name}</span>
        <span className="block text-[12.5px] text-muted-foreground">
          {count} member{count === 1 ? "" : "s"}
          {node.kind === "community" && node.data.pendingCount > 0 && ` · ${node.data.pendingCount} waiting`}
          {quiet && " · inactive"}
        </span>
      </span>
      <span className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1">
        {admins.slice(0, 3).map((a) => <Person key={a.id} admin={a} small />)}
        {admins.length > 3 && <span className="text-[12.5px] text-muted-foreground">+{admins.length - 3}</span>}
        {leaders.slice(0, 2).map((l) => (
          <span key={l.memberId} className="inline-flex items-center gap-1.5 text-[12.5px] text-muted-foreground" title="Group leader (a member)">
            <UserAvatar name={l.name} size="sm" />
            <span className="truncate">{l.name.split(" ")[0]} · leader</span>
          </span>
        ))}
        {unmanaged && (
          <span className="text-[12.5px] text-muted-foreground">
            {leaders.length > 0 ? "No administrator" : "No administrator"}
          </span>
        )}
      </span>
    </button>
  );
}

/** Past this many entries a branch shows the first few and offers the rest, so a big institution is not one endless scroll. */
const BRANCH_PREVIEW = 8;

function Branch({ title, count, hint, children, empty, split, preview = true }: { title: string; count: number; hint: string; children: React.ReactNode; empty?: React.ReactNode; split: boolean; preview?: boolean }) {
  const storageKey = `structure-branch-open:${title}`;
  // What each person left open is remembered between visits. This only renders once the structure has loaded in the
  // browser, never on the server, so reading storage when it is first created cannot disagree with a server render.
  const [open, setOpen] = useState(() => {
    try { return localStorage.getItem(storageKey) !== "0"; } catch { return true; }
  });
  const [showAll, setShowAll] = useState(false);
  const toggle = () => {
    setOpen((v) => {
      try { localStorage.setItem(storageKey, v ? "0" : "1"); } catch { /* storage unavailable */ }
      return !v;
    });
  };

  const items = React.Children.toArray(children);
  const overflow = preview && items.length > BRANCH_PREVIEW + 2;
  const shown = overflow && !showAll ? items.slice(0, BRANCH_PREVIEW) : items;
  const panelId = `branch-${title.replace(/\s+/g, "-").toLowerCase()}`;

  return (
    // On wide screens each branch hangs from the bar under the institution by a short drop line.
    <section className={cn("relative min-w-0", split && "lg:before:absolute lg:before:-top-5 lg:before:left-1/2 lg:before:h-5 lg:before:w-0.5 lg:before:bg-border")}>
      <button
        type="button"
        onClick={toggle}
        aria-expanded={open}
        aria-controls={panelId}
        className="flex w-full items-start gap-2.5 border border-border bg-muted/40 px-3.5 py-3 text-left focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary"
      >
        <ChevronDown size={14} className={cn("mt-1.5 shrink-0 text-muted-foreground transition-transform", !open && "-rotate-90")} />
        <span className="min-w-0">
          <span className="flex items-baseline gap-2 text-[16px] font-bold">
            {title}
            <span className="bg-muted px-1.5 py-0.5 text-[11.5px] font-semibold text-muted-foreground">{count}</span>
          </span>
          <span className="block text-[12.5px] font-normal text-muted-foreground">{open ? hint : `${count} hidden. Tap to show.`}</span>
        </span>
      </button>
      {open && (
        <div id={panelId}>
          {/* The spine and the little ticks are what make a list read as a tree. */}
          <ul className="relative ml-4 mt-0 space-y-2.5 border-l-2 border-muted-foreground/40 pb-1 pl-5 pt-3">
            {shown}
          </ul>
          {overflow && (
            <button
              type="button"
              onClick={() => setShowAll((v) => !v)}
              className="mt-3 min-h-10 text-[13px] font-semibold text-primary underline-offset-2 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary"
            >
              {showAll ? "Show fewer" : `Show all ${items.length}`}
            </button>
          )}
          {empty}
        </div>
      )}
    </section>
  );
}

function Leaf({ children }: { children: React.ReactNode }) {
  // The elbow that joins an entry to the line running down its parent. It sits at the middle of the first row.
  return <li className="relative before:absolute before:-left-5 before:top-[22px] before:h-0.5 before:w-5 before:bg-muted-foreground/40">{children}</li>;
}

function RootNode({ s }: { s: InstitutionStructure }) {
  const supers = s.admins.filter((a) => a.role === "SuperAdmin" && !a.isDisabled);
  return (
    <div className="mx-auto w-full max-w-[560px] border border-border bg-card p-4 text-center sm:p-5">
      <div className="mx-auto flex size-12 items-center justify-center overflow-hidden bg-primary/10 text-primary">
        {s.logoUrl ? <img src={s.logoUrl} alt="" className="size-12 object-cover" /> : <Sitemap size={22} />}
      </div>
      <h2 className="mt-2.5 text-[17px] font-bold leading-tight">{s.institutionName}</h2>
      <p className="mt-0.5 text-[12.5px] text-muted-foreground">
        {supers.length} full administrator{supers.length === 1 ? "" : "s"} · {s.admins.filter((a) => a.role === "ScopedAdmin" && !a.isDisabled).length} scoped
      </p>
      <div className="mt-3 flex flex-wrap items-center justify-center gap-x-4 gap-y-1.5">
        {supers.map((a) => <Person key={a.id} admin={a} />)}
      </div>
    </div>
  );
}

// ── Manage one node ────────────────────────────────────────────────────────

function ManageDialog({
  node, s, label, onClose, onChanged,
}: { node: Node; s: InstitutionStructure; label: string; onClose: () => void; onChanged: () => void }) {
  const assigned = adminsOf(s, node);
  const candidates = s.admins.filter((a) => a.role === "ScopedAdmin" && !a.isDisabled && !assigned.some((x) => x.id === a.id));
  const [pick, setPick] = useState("");
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState({ firstName: "", lastName: "", email: "" });
  const [error, setError] = useState<string | null>(null);

  const withScope = (a: StructureAdmin, years: number[], communities: string[]) => updateInstitutionStaff(a.id, {
    firstName: a.firstName, lastName: a.lastName, phone: a.phone ?? undefined, role: "ScopedAdmin", yearGroups: years, communityIds: communities, isDisabled: a.isDisabled,
  });

  const assign = useMutation({
    mutationFn: async (adminId: string) => {
      const a = s.admins.find((x) => x.id === adminId)!;
      // A scoped administrator holds one group at a time, so assigning a group replaces the previous one.
      return node.kind === "cohort"
        ? withScope(a, [node.data.year], a.communityIds)
        : withScope(a, a.yearGroups, [...new Set([...a.communityIds, node.data.id])]);
    },
    onSuccess: () => { toast.success("Assigned"); setPick(""); onChanged(); },
    onError: (e) => setError(handleApiError(e)),
  });

  const unassign = useMutation({
    mutationFn: async (a: StructureAdmin) => node.kind === "cohort"
      ? withScope(a, a.yearGroups.filter((y) => y !== node.data.year), a.communityIds)
      : withScope(a, a.yearGroups, a.communityIds.filter((c) => c !== node.data.id)),
    onSuccess: () => { toast.success("Removed from this " + (node.kind === "cohort" ? label.toLowerCase() : "community")); onChanged(); },
    onError: (e) => setError(handleApiError(e)),
  });

  const invite = useMutation({
    mutationFn: () => createInstitutionStaff({
      ...form, role: "ScopedAdmin",
      yearGroups: node.kind === "cohort" ? [node.data.year] : [],
      communityIds: node.kind === "community" ? [node.data.id] : [],
    }),
    onSuccess: () => { toast.success("Administrator added. We emailed them a link to choose their password."); setCreating(false); setForm({ firstName: "", lastName: "", email: "" }); onChanged(); },
    onError: (e) => setError(handleApiError(e)),
  });

  const replaced = (() => {
    if (node.kind !== "cohort" || !pick) return null;
    const a = s.admins.find((x) => x.id === pick);
    return a && a.yearGroups.length > 0 && !a.yearGroups.includes(node.data.year) ? a : null;
  })();

  return (
    <Dialog open onOpenChange={(v) => { if (!v) onClose(); }}>
      <DialogContent>
        <DialogHeader><DialogTitle>{node.data.name}</DialogTitle></DialogHeader>
        <div className="space-y-5">
          <p className="text-[13.5px] text-muted-foreground">
            {node.data.memberCount} active member{node.data.memberCount === 1 ? "" : "s"}
            {node.kind === "cohort" ? ` · graduating ${node.data.year}` : ""}
          </p>

          <section aria-labelledby="mgr-admins">
            <h3 id="mgr-admins" className="text-[13px] font-semibold">Administrators</h3>
            {assigned.length === 0 && <p className="mt-1 text-[13px] text-muted-foreground">Nobody looks after this yet. Add someone below.</p>}
            <ul className="mt-1.5 divide-y divide-border">
              {assigned.map((a) => (
                <li key={a.id} className="flex items-center justify-between gap-3 py-2">
                  <span className="flex min-w-0 items-center gap-2.5">
                    <UserAvatar name={a.name} />
                    <span className="min-w-0"><span className="block truncate text-[14px] font-semibold">{a.name}</span><span className="block truncate text-[12.5px] text-muted-foreground">{a.email}</span></span>
                  </span>
                  <Button size="sm" variant="outline" onClick={() => unassign.mutate(a)} isLoading={unassign.isPending && unassign.variables?.id === a.id}>Remove</Button>
                </li>
              ))}
            </ul>
          </section>

          {!creating && (
            <section aria-labelledby="mgr-add" className="space-y-2">
              <h3 id="mgr-add" className="text-[13px] font-semibold">Add someone who already has access</h3>
              {candidates.length === 0 ? (
                <p className="text-[13px] text-muted-foreground">Every scoped administrator is already here. Add a new one instead.</p>
              ) : (
                <div className="flex flex-wrap gap-2">
                  <select value={pick} onChange={(e) => setPick(e.target.value)} aria-label="Scoped administrator" className="h-11 min-w-0 flex-1 basis-48 border border-border bg-background px-3 text-[16px] md:text-[14px]">
                    <option value="">Choose an administrator</option>
                    {candidates.map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
                  </select>
                  <Button disabled={!pick} onClick={() => assign.mutate(pick)} isLoading={assign.isPending}>Assign</Button>
                </div>
              )}
              {replaced && <p className="text-[12.5px] text-muted-foreground">{replaced.firstName} looks after another {label.toLowerCase()} now. Assigning this one moves them here.</p>}
              <Button variant="outline" size="sm" onClick={() => setCreating(true)}>Add a new administrator</Button>
            </section>
          )}

          {creating && (
            <section aria-labelledby="mgr-new" className="space-y-3">
              <h3 id="mgr-new" className="text-[13px] font-semibold">New scoped administrator</h3>
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <div className="space-y-1.5"><Label htmlFor="n-first">First name</Label><Input id="n-first" value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} /></div>
                <div className="space-y-1.5"><Label htmlFor="n-last">Last name</Label><Input id="n-last" value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} /></div>
              </div>
              <div className="space-y-1.5"><Label htmlFor="n-email">Email</Label><Input id="n-email" type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} /></div>
              <p className="border border-border bg-muted/40 p-3 text-[13px] leading-relaxed">We email them a link to choose their own password. You do not set one.</p>
            </section>
          )}
          <FormError message={error} />
        </div>
        {/* The footer is the pinned action bar on a phone, so while a new administrator is being written the actions that matter live there. */}
        <DialogFooter>
          {creating ? (
            <>
              <Button variant="outline" onClick={() => setCreating(false)}>Back</Button>
              <Button
                disabled={!form.firstName.trim() || !form.lastName.trim() || !form.email.includes("@")}
                isLoading={invite.isPending} loadingText="Adding" onClick={() => invite.mutate()}
              >Add administrator</Button>
            </>
          ) : (
            <Button variant="outline" onClick={onClose}>Done</Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** One decade of cohorts, folded by default unless it is the current one. Built so a school founded in 1940 stays navigable. */
function DecadeGroup({
  decade, cohorts, s, open, onToggle, onOpenNode, singular, plural,
}: {
  decade: number; cohorts: StructureCohort[]; s: InstitutionStructure; open: boolean; onToggle: () => void; onOpenNode: (id: string) => void; singular: string; plural: string;
}) {
  const needing = cohorts.filter((c) => adminsOf(s, { kind: "cohort", data: c }).length === 0).length;
  const members = cohorts.reduce((n, c) => n + c.memberCount, 0);
  return (
    <div>
      <button
        type="button"
        onClick={onToggle}
        aria-expanded={open}
        className="flex w-full flex-wrap items-center gap-x-3 gap-y-0.5 border border-border bg-card px-3.5 py-2.5 text-left transition-colors hover:bg-muted/50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary"
      >
        <ChevronDown size={13} className={cn("shrink-0 text-muted-foreground transition-transform", !open && "-rotate-90")} />
        <span className="text-[15px] font-bold">{decade}s</span>
        <span className="text-[12.5px] text-muted-foreground">
          {cohorts.length} {(cohorts.length === 1 ? singular : plural).toLowerCase()} · {members.toLocaleString()} member{members === 1 ? "" : "s"}
        </span>
        {needing > 0 && <span className="ml-auto text-[12.5px] font-semibold text-warning">{needing} without an administrator</span>}
      </button>
      {open && (
        <ul className="relative ml-4 space-y-2 border-l-2 border-muted-foreground/40 pb-0.5 pl-5 pt-2.5">
          {cohorts.map((c) => {
            const node: Node = { kind: "cohort", data: c };
            return <Leaf key={c.id}><NodeCard node={node} admins={adminsOf(s, node)} onOpen={() => onOpenNode(c.id)} /></Leaf>;
          })}
        </ul>
      )}
    </div>
  );
}

// ── Page ────────────────────────────────────────────────────────────────────

export default function StructurePage() {
  const qc = useQueryClient();
  const { data: s, isLoading, isError, refetch } = useQuery({ queryKey: ["structure"], queryFn: getStructure });
  const [open, setOpen] = useState<{ kind: "cohort" | "community"; id: string } | null>(null);
  const [adding, setAdding] = useState<"cohort" | "community" | null>(null);
  const [newName, setNewName] = useState("");
  const [newYear, setNewYear] = useState(String(new Date().getFullYear()));
  const [error, setError] = useState<string | null>(null);

  const singular = s?.cohortLabel?.trim() || DEFAULT_COHORT_LABEL;
  const plural = s?.cohortLabelPlural?.trim() || DEFAULT_COHORT_LABEL_PLURAL;
  const isCommunityOrg = s?.organizationType === "Community";
  const [q, setQ] = useState("");
  const [needsOnly, setNeedsOnly] = useState(false);
  const [decadeOpen, setDecadeOpen] = useState<Record<string, boolean>>(() => {
    try { const raw = localStorage.getItem("structure-decades"); return raw ? JSON.parse(raw) : {}; } catch { return {}; }
  });
  const saveDecades = (next: Record<string, boolean>) => {
    setDecadeOpen(next);
    try { localStorage.setItem("structure-decades", JSON.stringify(next)); } catch { /* storage unavailable */ }
  };
  const currentDecade = Math.floor(new Date().getFullYear() / 10) * 10;
  const refresh = () => { qc.invalidateQueries({ queryKey: ["structure"] }); qc.invalidateQueries({ queryKey: ["batches"] }); qc.invalidateQueries({ queryKey: ["staff"] }); };

  const addCohort = useMutation({
    mutationFn: () => createBatch({ name: newName.trim() || `${singular} of ${newYear}`, year: Number(newYear) }),
    onSuccess: () => { toast.success(`${singular} added`); setAdding(null); setNewName(""); refresh(); },
    onError: (e) => setError(handleApiError(e)),
  });
  const addCommunity = useMutation({
    mutationFn: () => createCommunity({ name: newName.trim() }),
    onSuccess: () => { toast.success("Community added"); setAdding(null); setNewName(""); refresh(); },
    onError: (e) => setError(handleApiError(e)),
  });
  const createMissing = useMutation({
    mutationFn: async (years: number[]) => { for (const y of years) await createBatch({ name: `${singular} of ${y}`, year: y }); },
    onSuccess: () => { toast.success(`${plural} created`); refresh(); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const opened = useMemo<Node | null>(() => {
    if (!s || !open) return null;
    if (open.kind === "cohort") { const c = s.cohorts.find((x) => x.id === open.id); return c ? { kind: "cohort", data: c } : null; }
    const c = s.communities.find((x) => x.id === open.id);
    return c ? { kind: "community", data: c } : null;
  }, [s, open]);

  const query = q.trim().toLowerCase();
  const filtering = query.length > 0 || needsOnly;
  const view = useMemo(() => {
    if (!s) return null;
    const matches = (text: string) => !query || text.toLowerCase().includes(query);
    const cohorts = s.cohorts.filter((c) => {
      const node: Node = { kind: "cohort", data: c };
      const adminList = adminsOf(s, node);
      if (needsOnly && adminList.length > 0) return false;
      return matches(`${c.name} ${c.year} ${adminList.map((a) => a.name).join(" ")}`);
    });
    const communities = s.communities.filter((c) => {
      const node: Node = { kind: "community", data: c };
      const adminList = adminsOf(s, node);
      if (needsOnly && adminList.length > 0) return false;
      return matches(`${c.name} ${c.leaders.map((l) => l.name).join(" ")} ${adminList.map((a) => a.name).join(" ")}`);
    });
    const byDecade = new Map<number, StructureCohort[]>();
    for (const c of cohorts) {
      const d = Math.floor(c.year / 10) * 10;
      byDecade.set(d, [...(byDecade.get(d) ?? []), c]);
    }
    const decades = [...byDecade.entries()]
      .sort((a, b) => b[0] - a[0])
      .map(([decade, list]) => ({ decade, list: [...list].sort((a, b) => b.year - a.year) }));
    const needing = s.unmanagedCohortIds.length + s.unmanagedCommunityIds.length;
    return { cohorts, communities, decades, needing };
  }, [s, query, needsOnly]);

  if (isError) return <div className="mx-auto max-w-[1100px] p-4 sm:p-7"><LoadError title="The structure couldn’t load" onRetry={() => void refetch()} /></div>;

  const scopeless = s?.admins.filter((a) => s.adminsWithoutScope.includes(a.id)) ?? [];
  const unmanaged = (s?.unmanagedCohortIds.length ?? 0) + (s?.unmanagedCommunityIds.length ?? 0);

  return (
    <div className="mx-auto max-w-[1100px] space-y-6 p-4 sm:p-7">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="m-0 text-[20px] font-bold sm:text-[25px]">Structure</h1>
          <p className="mt-1.5 max-w-[62ch] text-[13px] text-muted-foreground">
            How your institution is organised and who looks after each part. Tap any {singular.toLowerCase()} or community to change who is responsible for it.
          </p>
        </div>
        {s && (
          <div className="flex flex-wrap gap-2">
            {!isCommunityOrg && <Button size="sm" onClick={() => { setError(null); setAdding("cohort"); }}><Plus size={14} /> Add {singular.toLowerCase()}</Button>}
            <Button size="sm" variant="outline" onClick={() => { setError(null); setAdding("community"); }}><Plus size={14} /> Add community</Button>
          </div>
        )}
      </header>

      {isLoading && <div className="skeleton h-64 w-full" aria-busy="true" />}

      {s && (
        <>
          {/* What to do next, only when there is something to do. */}
          {(!isCommunityOrg && s.yearsWithoutGroup.length > 0) || scopeless.length > 0 || unmanaged > 0 ? (
            <section aria-label="To tidy up" className="space-y-2 border border-border bg-card p-4">
              <h2 className="text-[13px] font-semibold text-muted-foreground">To tidy up</h2>
              {!isCommunityOrg && s.yearsWithoutGroup.length > 0 && (
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="text-[13.5px]">
                    {s.membersWithoutGroup} member{s.membersWithoutGroup === 1 ? " is" : "s are"} in years with no {singular.toLowerCase()} yet ({s.yearsWithoutGroup.slice(0, 6).join(", ")}{s.yearsWithoutGroup.length > 6 ? "…" : ""}).
                  </p>
                  <Button size="sm" onClick={() => createMissing.mutate(s.yearsWithoutGroup)} isLoading={createMissing.isPending} loadingText="Creating">
                    Create {s.yearsWithoutGroup.length} {s.yearsWithoutGroup.length === 1 ? singular.toLowerCase() : plural.toLowerCase()}
                  </Button>
                </div>
              )}
              {scopeless.length > 0 && (
                <p className="text-[13.5px]">
                  {scopeless.map((a) => a.name).join(", ")} {scopeless.length === 1 ? "has" : "have"} no {singular.toLowerCase()} or community and can&apos;t see anything yet. Open one below and assign them.
                </p>
              )}
              {unmanaged > 0 && (
                <p className="text-[13.5px] text-muted-foreground">{unmanaged} part{unmanaged === 1 ? "" : "s"} of the structure have no administrator (marked with a dashed edge). Full administrators still manage them.</p>
              )}
            </section>
          ) : null}

          <div className="space-y-5">
            <RootNode s={s} />
            <div aria-hidden className="mx-auto h-5 w-0.5 bg-border" />

            {/* Finding things is the main job once there are dozens. */}
            {view && (s.cohorts.length + s.communities.length > 6) && (
              <div className="flex flex-col gap-2.5 sm:flex-row sm:items-center">
                <div className="relative min-w-0 flex-1">
                  <Search size={14} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    value={q}
                    onChange={(e) => setQ(e.target.value)}
                    placeholder={`Find a ${singular.toLowerCase()}, year, group or administrator`}
                    aria-label="Find in the structure"
                    className="h-11 pl-9 pr-9"
                  />
                  {q && (
                    <button type="button" onClick={() => setQ("")} aria-label="Clear search" className="absolute right-1 top-1/2 flex size-9 -translate-y-1/2 items-center justify-center text-muted-foreground hover:text-foreground">
                      <X size={14} />
                    </button>
                  )}
                </div>
                <div className="flex gap-2">
                  <button
                    type="button"
                    aria-pressed={!needsOnly}
                    onClick={() => setNeedsOnly(false)}
                    className={cn("h-11 border px-3.5 text-[13px] font-semibold", !needsOnly ? "border-primary bg-primary text-primary-foreground" : "border-border bg-card hover:bg-muted")}
                  >All</button>
                  <button
                    type="button"
                    aria-pressed={needsOnly}
                    onClick={() => setNeedsOnly(true)}
                    className={cn("h-11 border px-3.5 text-[13px] font-semibold", needsOnly ? "border-primary bg-primary text-primary-foreground" : "border-border bg-card hover:bg-muted")}
                  >Needs an administrator <span className="tabular-nums">{view.needing}</span></button>
                </div>
              </div>
            )}

            {view && !isCommunityOrg && (
              <Branch
                split={false}
                preview={false}
                title={plural}
                count={filtering ? view.cohorts.length : s.cohorts.length}
                hint={filtering ? `${view.cohorts.length} of ${s.cohorts.length} match` : "Members grouped by the year they graduated, newest decade first"}
                empty={view.cohorts.length === 0 && (
                  <p className="mt-3 border border-dashed border-border p-4 text-[13.5px] text-muted-foreground">
                    {s.cohorts.length === 0
                      ? `No ${plural.toLowerCase()} yet. Add one for each year of members, and give each a scoped administrator to look after it.`
                      : `No ${plural.toLowerCase()} match${filtering ? " that search" : ""}.`}
                  </p>
                )}
              >
                {view.decades.length > 1 || view.decades.length === 1 ? (
                  [
                    <li key="__bar" className="list-none">
                      <div className="-mt-1 mb-1 flex gap-1">
                        <Button size="sm" variant="ghost" onClick={() => saveDecades(Object.fromEntries(view.decades.map((d) => [String(d.decade), true])))}>Expand all</Button>
                        <Button size="sm" variant="ghost" onClick={() => saveDecades(Object.fromEntries(view.decades.map((d) => [String(d.decade), false])))}>Collapse all</Button>
                      </div>
                    </li>,
                    ...view.decades.map(({ decade, list }) => {
                      const stored = decadeOpen[String(decade)];
                      const isOpen = filtering ? true : stored ?? decade === currentDecade;
                      return (
                        <Leaf key={decade}>
                          <DecadeGroup
                            decade={decade}
                            cohorts={list}
                            s={s}
                            open={isOpen}
                            onToggle={() => saveDecades({ ...decadeOpen, [String(decade)]: !isOpen })}
                            onOpenNode={(id) => setOpen({ kind: "cohort", id })}
                            singular={singular}
                            plural={plural}
                          />
                        </Leaf>
                      );
                    }),
                  ]
                ) : null}
              </Branch>
            )}

            {view && (
              <Branch
                split={false}
                title="Groups"
                count={filtering ? view.communities.length : s.communities.length}
                hint={filtering ? `${view.communities.length} of ${s.communities.length} match` : "Interest and location groups, each with its own leaders"}
                empty={view.communities.length === 0 && (
                  <p className="mt-3 border border-dashed border-border p-4 text-[13.5px] text-muted-foreground">
                    {s.communities.length === 0 ? "No communities yet. Add one for a profession, a place or an interest." : `No groups match${filtering ? " that search" : ""}.`}
                  </p>
                )}
              >
                {view.communities.map((c) => {
                  const node: Node = { kind: "community", data: c };
                  return <Leaf key={c.id}><NodeCard node={node} admins={adminsOf(s, node)} onOpen={() => setOpen({ kind: "community", id: c.id })} /></Leaf>;
                })}
              </Branch>
            )}
          </div>

          <Card className="border-border/40">
            <CardContent className="flex flex-wrap items-center gap-x-5 gap-y-1.5 p-3.5 text-[12.5px] text-muted-foreground">
              <span className="font-semibold text-foreground">Key</span>
              <span className="inline-flex items-center gap-1.5"><span className="inline-block h-3 w-5 border border-border" /> looked after</span>
              <span className="inline-flex items-center gap-1.5"><span className="inline-block h-3 w-5 border border-dashed border-warning/70" /> no administrator yet</span>
              <span>Full administrators manage everything; scoped administrators look after the parts they are assigned.</span>
            </CardContent>
          </Card>
        </>
      )}

      {s && opened && (
        <ManageDialog node={opened} s={s} label={singular} onClose={() => setOpen(null)} onChanged={refresh} />
      )}

      <Dialog open={adding !== null} onOpenChange={(v) => { if (!v) { setAdding(null); setError(null); } }}>
        <DialogContent>
          <DialogHeader><DialogTitle>{adding === "cohort" ? `Add ${singular.toLowerCase()}` : "Add community"}</DialogTitle></DialogHeader>
          <div className="space-y-4">
            {adding === "cohort" && (
              <div className="space-y-1.5">
                <Label htmlFor="s-year">Graduating year</Label>
                <Input id="s-year" inputMode="numeric" value={newYear} onChange={(e) => setNewYear(e.target.value.replace(/\D/g, "").slice(0, 4))} />
              </div>
            )}
            <div className="space-y-1.5">
              <Label htmlFor="s-name">{adding === "cohort" ? "Name (optional)" : "Name"}</Label>
              <Input id="s-name" value={newName} onChange={(e) => setNewName(e.target.value)} placeholder={adding === "cohort" ? `${singular} of ${newYear}` : "e.g. Engineers"} maxLength={80} />
            </div>
            <FormError message={error} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAdding(null)}>Cancel</Button>
            <Button
              disabled={adding === "cohort" ? newYear.length !== 4 : newName.trim().length < 2}
              isLoading={addCohort.isPending || addCommunity.isPending} loadingText="Adding"
              onClick={() => (adding === "cohort" ? addCohort.mutate() : addCommunity.mutate())}
            >Add</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
