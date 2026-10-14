"use client";

import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  Button, Card, CardContent, Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle, FormError, Input, Label, LoadError, Plus, Sitemap, UserAvatar, cn,
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

/** One group or community in the chart, with the people who look after it. A dashed amber edge means nobody does yet. */
function NodeCard({ node, admins, onOpen }: { node: Node; admins: StructureAdmin[]; onOpen: () => void }) {
  const unmanaged = admins.length === 0;
  const count = node.data.memberCount;
  const quiet = !node.data.isActive;
  return (
    <button
      type="button"
      onClick={onOpen}
      className={cn(
        "group relative w-full border bg-card px-3.5 py-3 text-left transition-colors hover:bg-muted/50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary",
        unmanaged ? "border-dashed border-warning/70" : "border-border",
        quiet && "opacity-70",
      )}
    >
      <span className="flex items-start justify-between gap-3">
        <span className="min-w-0">
          <span className="block truncate text-[14.5px] font-semibold">{node.data.name}</span>
          <span className="mt-0.5 block text-[12.5px] text-muted-foreground">
            {count} member{count === 1 ? "" : "s"}
            {node.kind === "cohort" && ` · ${node.data.year}`}
            {node.kind === "community" && node.data.pendingCount > 0 && ` · ${node.data.pendingCount} waiting`}
            {quiet && " · inactive"}
          </span>
        </span>
      </span>

      <span className="mt-2.5 flex flex-wrap items-center gap-x-3 gap-y-1">
        {admins.map((a) => <Person key={a.id} admin={a} small />)}
        {node.kind === "community" && node.data.leaders.map((l) => (
          <span key={l.memberId} className="inline-flex items-center gap-1.5 text-[12.5px] text-muted-foreground" title="Community leader (a member)">
            <UserAvatar name={l.name} size="sm" />
            <span className="truncate">{l.name.split(" ")[0]} · leader</span>
          </span>
        ))}
        {unmanaged && (
          <span className="text-[12.5px] font-semibold text-warning">
            {node.kind === "community" && node.data.leaders.length > 0 ? "No administrator yet" : "No one looks after this yet"}
          </span>
        )}
      </span>
    </button>
  );
}

function Branch({ title, count, hint, children, empty, split }: { title: string; count: number; hint: string; children: React.ReactNode; empty?: React.ReactNode; split: boolean }) {
  return (
    // On wide screens each branch hangs from the bar under the institution by a short drop line.
    <section className={cn("relative min-w-0", split && "lg:before:absolute lg:before:-top-5 lg:before:left-1/2 lg:before:h-5 lg:before:w-0.5 lg:before:bg-border")}>
      <div className="border-b border-border pb-2.5">
        <h2 className="flex items-baseline gap-2 text-[15px] font-bold">
          {title}
          <span className="bg-muted px-1.5 py-0.5 text-[11.5px] font-semibold text-muted-foreground">{count}</span>
        </h2>
        <p className="text-[12.5px] text-muted-foreground">{hint}</p>
      </div>
      {/* The spine and the little ticks are what make a list read as a tree. */}
      <ul className="relative mt-3 space-y-2.5 border-l-2 border-border pl-4 sm:pl-5">
        {children}
      </ul>
      {empty}
    </section>
  );
}

function Leaf({ children }: { children: React.ReactNode }) {
  return <li className="relative before:absolute before:-left-4 before:top-6 before:h-0.5 before:w-4 before:bg-border sm:before:-left-5 sm:before:w-5">{children}</li>;
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
  const [form, setForm] = useState({ firstName: "", lastName: "", email: "", password: "" });
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
    onSuccess: () => { toast.success("Administrator added"); setCreating(false); setForm({ firstName: "", lastName: "", email: "", password: "" }); onChanged(); },
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
              <div className="space-y-1.5"><Label htmlFor="n-pass">Temporary password</Label><Input id="n-pass" type="text" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} placeholder="At least 8 characters" /></div>
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
                disabled={!form.firstName.trim() || !form.lastName.trim() || !form.email.includes("@") || form.password.length < 8}
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
            {!isCommunityOrg && <div aria-hidden className="mx-auto hidden h-0.5 w-1/2 bg-border lg:block" />}
            <div className={cn("grid gap-x-8 gap-y-8", !isCommunityOrg && "lg:grid-cols-2 lg:pt-5")}>
              {!isCommunityOrg && (
                <Branch
                  split
                  title={plural}
                  count={s.cohorts.length}
                  hint={`Members grouped by the year they graduated`}
                  empty={s.cohorts.length === 0 && (
                    <p className="mt-3 border border-dashed border-border p-4 text-[13.5px] text-muted-foreground">
                      No {plural.toLowerCase()} yet. Add one for each year of members, and give each a scoped administrator to look after it.
                    </p>
                  )}
                >
                  {s.cohorts.map((c) => {
                    const node: Node = { kind: "cohort", data: c };
                    return <Leaf key={c.id}><NodeCard node={node} admins={adminsOf(s, node)} onOpen={() => setOpen({ kind: "cohort", id: c.id })} /></Leaf>;
                  })}
                </Branch>
              )}
              <Branch
                split={!isCommunityOrg}
                title="Groups"
                count={s.communities.length}
                hint="Interest and location groups, each with its own leaders"
                empty={s.communities.length === 0 && (
                  <p className="mt-3 border border-dashed border-border p-4 text-[13.5px] text-muted-foreground">
                    No communities yet. Add one for a profession, a place or an interest.
                  </p>
                )}
              >
                {s.communities.map((c) => {
                  const node: Node = { kind: "community", data: c };
                  return <Leaf key={c.id}><NodeCard node={node} admins={adminsOf(s, node)} onOpen={() => setOpen({ kind: "community", id: c.id })} /></Leaf>;
                })}
              </Branch>
            </div>
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
