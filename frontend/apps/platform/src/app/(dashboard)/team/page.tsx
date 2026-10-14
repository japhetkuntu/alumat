"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button, Badge, Card, CardContent, Dialog, DialogContent, DialogHeader, DialogTitle, EmptyState, LoadError, Sitemap, UserAvatar, cn } from "@alumni/ui";
import {
  getInstitutions, getOnboardingLeads, getTeam, getTeamAssignees, reassignWork, setTeamManager,
  type InstitutionListItem, type OnboardingLead, type TeamMember,
} from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";
import { PageHeading } from "@/components/platform/page-heading";

/** Work nobody owns. Only a SuperAdmin ever sees it, so it is shown as its own place in the chart. */
const UNASSIGNED = "__unassigned__";

type Node = TeamMember & { children: Node[] };

/** Builds the reporting tree from a flat list. Anyone whose manager is outside the list (a representative looking at their own branch) is a root. */
function buildTree(people: TeamMember[]): Node[] {
  const byId = new Map<string, Node>(people.map((p) => [p.id, { ...p, children: [] }]));
  const roots: Node[] = [];
  for (const n of byId.values()) {
    const parent = n.reportsToStaffId ? byId.get(n.reportsToStaffId) : undefined;
    if (parent && parent.id !== n.id) parent.children.push(n); else roots.push(n);
  }
  const sort = (n: Node, seen: Set<string>) => {
    n.children.sort((a, b) => a.name.localeCompare(b.name));
    for (const c of n.children) if (!seen.has(c.id)) { seen.add(c.id); sort(c, seen); }
  };
  roots.sort((a, b) => (a.role === "SuperAdmin" ? 0 : 1) - (b.role === "SuperAdmin" ? 0 : 1) || a.name.localeCompare(b.name));
  for (const r of roots) sort(r, new Set([r.id]));
  return roots;
}

/** A branch's own totals plus everyone beneath it, for whoever leads it. */
function rollup(n: Node): { institutions: number; openLeads: number; people: number } {
  return n.children.reduce(
    (t, c) => { const r = rollup(c); return { institutions: t.institutions + r.institutions, openLeads: t.openLeads + r.openLeads, people: t.people + r.people }; },
    { institutions: n.institutions, openLeads: n.openLeads, people: 1 },
  );
}

function plural(n: number, one: string, many = one + "s") { return `${n.toLocaleString()} ${n === 1 ? one : many}`; }

/** One person in the chart. A dashed amber edge means a representative who has nothing to look after yet. */
function PersonCard({ node, myId, onOpen }: { node: Node; myId: string; onOpen: () => void }) {
  const total = rollup(node);
  const idle = node.role === "Representative" && !node.isDisabled && node.institutions === 0 && node.openLeads === 0 && node.children.length === 0;
  return (
    <button
      type="button"
      onClick={onOpen}
      className={cn(
        "w-full border bg-card px-3.5 py-3 text-left transition-colors hover:bg-muted/50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary",
        idle ? "border-dashed border-warning/70" : "border-border",
        node.isDisabled && "opacity-60",
      )}
    >
      <span className="flex min-w-0 items-center gap-3">
        <UserAvatar name={node.name} size="sm" />
        <span className="min-w-0 flex-1">
          <span className="block truncate text-[14.5px] font-semibold">
            {node.name}{node.id === myId && <span className="ml-1.5 text-[12px] font-normal text-muted-foreground">(you)</span>}
          </span>
          <span className="block text-[12.5px] leading-snug text-muted-foreground">
            {plural(node.institutions, "institution")} · {plural(node.openLeads, "open lead")}
            {node.children.length > 0 && ` · team of ${total.people - 1}`}
          </span>
        </span>
        <Badge variant={node.role === "SuperAdmin" ? "info" : "secondary"}>{node.role === "SuperAdmin" ? "SuperAdmin" : "Rep"}</Badge>
      </span>
      {(idle || node.isDisabled || (node.children.length > 0 && total.institutions !== node.institutions)) && (
        <span className="mt-2 block text-[12.5px]">
          {node.isDisabled ? <span className="text-muted-foreground">Disabled</span>
            : idle ? <span className="font-semibold text-warning">Nothing assigned yet</span>
            : <span className="text-muted-foreground">{plural(total.institutions, "institution")} with their team</span>}
        </span>
      )}
    </button>
  );
}

/** The spine and ticks are what make a list read as a tree. */
function Branch({ node, myId, onOpen }: { node: Node; myId: string; onOpen: (id: string) => void }) {
  return (
    <li className="relative before:absolute before:-left-4 before:top-6 before:h-0.5 before:w-4 before:bg-border sm:before:-left-5 sm:before:w-5">
      <PersonCard node={node} myId={myId} onOpen={() => onOpen(node.id)} />
      {node.children.length > 0 && (
        <ul className="relative mt-2.5 space-y-2.5 border-l-2 border-border pl-4 sm:pl-5">
          {node.children.map((c) => <Branch key={c.id} node={c} myId={myId} onOpen={onOpen} />)}
        </ul>
      )}
    </li>
  );
}

function Check({ checked, onChange, label, sub }: { checked: boolean; onChange: (v: boolean) => void; label: string; sub?: string }) {
  return (
    <label className="flex cursor-pointer items-start gap-2.5 py-2">
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} className="mt-1 size-4 shrink-0 accent-[var(--primary)]" />
      <span className="min-w-0">
        <span className="block truncate text-[13.5px] font-semibold">{label}</span>
        {sub && <span className="block truncate text-[12.5px] text-muted-foreground">{sub}</span>}
      </span>
    </label>
  );
}

function ManageDialog({
  personId, people, institutions, leads, canManage, onClose,
}: {
  personId: string; people: TeamMember[]; institutions: InstitutionListItem[]; leads: OnboardingLead[]; canManage: boolean; onClose: () => void;
}) {
  const qc = useQueryClient();
  const person = personId === UNASSIGNED ? null : people.find((p) => p.id === personId) ?? null;
  const ownerId = personId === UNASSIGNED ? null : personId;
  const [pickedInst, setPickedInst] = useState<string[]>([]);
  const [pickedLeads, setPickedLeads] = useState<string[]>([]);
  const [target, setTarget] = useState("");

  const { data: assignees } = useQuery({ queryKey: ["team-assignees"], queryFn: getTeamAssignees });
  const ownedInst = institutions.filter((i) => (i.ownerStaffId ?? null) === ownerId);
  const ownedLeads = leads.filter((l) => (l.assigneeStaffId ?? null) === ownerId);
  const targets = (assignees ?? []).filter((a) => a.id !== ownerId);
  // Anyone already below this person can't also be their manager.
  const below = new Set<string>();
  for (let grew = true; grew;) {
    grew = false;
    for (const p of people) {
      if (p.reportsToStaffId && (p.reportsToStaffId === personId || below.has(p.reportsToStaffId)) && !below.has(p.id)) { below.add(p.id); grew = true; }
    }
  }
  const reportOptions = people.filter((p) => !p.isDisabled && p.id !== personId && !below.has(p.id));

  const refresh = () => {
    for (const k of ["team", "team-institutions", "team-leads", "team-assignees"]) qc.invalidateQueries({ queryKey: [k] });
  };
  const move = useMutation({
    mutationFn: () => reassignWork({ toStaffId: target, institutionIds: pickedInst, leadIds: pickedLeads }),
    onSuccess: (r) => {
      toast.success(`Handed over ${plural(r.institutions, "institution")} and ${plural(r.leads, "lead")}`);
      setPickedInst([]); setPickedLeads([]); setTarget(""); refresh();
    },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const reportsTo = useMutation({
    mutationFn: (managerId: string | null) => setTeamManager(personId, managerId),
    onSuccess: () => { toast.success("Reporting line updated"); refresh(); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const nothingPicked = pickedInst.length + pickedLeads.length === 0;
  const toggle = (list: string[], set: (v: string[]) => void, id: string, on: boolean) => set(on ? [...list, id] : list.filter((x) => x !== id));

  return (
    <Dialog open onOpenChange={(v) => { if (!v) onClose(); }}>
      <DialogContent>
        <DialogHeader><DialogTitle>{person ? person.name : "Work with no owner"}</DialogTitle></DialogHeader>
        <div className="space-y-5">
          <p className="text-[13.5px] text-muted-foreground">
            {person
              ? <>{person.email} · {person.role === "SuperAdmin" ? "SuperAdmin" : "Representative"}{person.isDisabled ? " · disabled" : ""}</>
              : "Institutions and leads nobody is responsible for yet. Hand them to someone so they get looked after."}
          </p>

          {person && canManage && person.role === "Representative" && (
            <section aria-labelledby="mg-reports" className="space-y-1.5">
              <h3 id="mg-reports" className="text-[13px] font-semibold">Reports to</h3>
              <select
                aria-labelledby="mg-reports"
                value={person.reportsToStaffId ?? ""}
                onChange={(e) => reportsTo.mutate(e.target.value || null)}
                disabled={reportsTo.isPending}
                className="h-10 w-full rounded-md border border-border bg-background px-3 text-[14px]"
              >
                <option value="" disabled>Choose someone</option>
                {reportOptions.map((o) => <option key={o.id} value={o.id}>{o.name} ({o.role === "SuperAdmin" ? "SuperAdmin" : "Representative"})</option>)}
              </select>
              <p className="text-[12.5px] text-muted-foreground">They see what they own and what their team owns. SuperAdmins see everything.</p>
            </section>
          )}

          <section aria-labelledby="mg-inst">
            <h3 id="mg-inst" className="text-[13px] font-semibold">Institutions <span className="font-normal text-muted-foreground">({ownedInst.length})</span></h3>
            {ownedInst.length === 0
              ? <p className="mt-1 text-[13px] text-muted-foreground">None owned directly.</p>
              : <div className="mt-1 max-h-48 divide-y divide-border overflow-y-auto border border-border px-3">
                  {ownedInst.map((i) => (
                    <Check key={i.id} checked={pickedInst.includes(i.id)} onChange={(v) => toggle(pickedInst, setPickedInst, i.id, v)}
                      label={i.name} sub={`${i.status} · ${plural(i.memberCount, "member")}`} />
                  ))}
                </div>}
          </section>

          <section aria-labelledby="mg-leads">
            <h3 id="mg-leads" className="text-[13px] font-semibold">Leads <span className="font-normal text-muted-foreground">({ownedLeads.length})</span></h3>
            {ownedLeads.length === 0
              ? <p className="mt-1 text-[13px] text-muted-foreground">None owned directly.</p>
              : <div className="mt-1 max-h-48 divide-y divide-border overflow-y-auto border border-border px-3">
                  {ownedLeads.map((l) => (
                    <Check key={l.id} checked={pickedLeads.includes(l.id)} onChange={(v) => toggle(pickedLeads, setPickedLeads, l.id, v)}
                      label={l.institutionName} sub={`${l.status} · ${l.contactName}`} />
                  ))}
                </div>}
          </section>

          {(ownedInst.length > 0 || ownedLeads.length > 0) && (
            <section aria-labelledby="mg-move" className="space-y-2 border-t border-border pt-4">
              <h3 id="mg-move" className="text-[13px] font-semibold">Hand the selected work to</h3>
              <div className="flex flex-wrap gap-2">
                <select
                  aria-labelledby="mg-move"
                  value={target}
                  onChange={(e) => setTarget(e.target.value)}
                  className="h-10 min-w-0 flex-1 rounded-md border border-border bg-background px-3 text-[14px]"
                >
                  <option value="">Choose someone</option>
                  {targets.map((a) => <option key={a.id} value={a.id}>{a.name} ({a.role === "SuperAdmin" ? "SuperAdmin" : "Representative"})</option>)}
                </select>
                <Button disabled={!target || nothingPicked} isLoading={move.isPending} loadingText="Handing over" onClick={() => move.mutate()}>
                  Hand over{!nothingPicked && ` ${pickedInst.length + pickedLeads.length}`}
                </Button>
              </div>
              {targets.length === 0 && <p className="text-[12.5px] text-muted-foreground">Nobody else is on your team to hand work to.</p>}
            </section>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}

export default function TeamPage() {
  const { user, isSuperAdmin } = useAuth();
  const [open, setOpen] = useState<string | null>(null);
  const { data, isLoading, isError, refetch } = useQuery({ queryKey: ["team"], queryFn: getTeam });
  const { data: instPage } = useQuery({ queryKey: ["team-institutions"], queryFn: () => getInstitutions({ page: 1, pageSize: 100 }) });
  const { data: leads } = useQuery({ queryKey: ["team-leads"], queryFn: () => getOnboardingLeads() });

  const people = data?.people ?? [];
  const tree = useMemo(() => buildTree(people), [people]);
  const myId = user?.id ?? data?.myId ?? "";
  const supers = people.filter((p) => p.role === "SuperAdmin" && !p.isDisabled);
  const reps = people.filter((p) => p.role === "Representative" && !p.isDisabled);
  const unassignedInst = data?.unassignedInstitutions ?? 0;
  const unassignedLeads = data?.unassignedLeads ?? 0;
  const idleReps = reps.filter((r) => r.institutions === 0 && r.openLeads === 0 && r.directReports === 0);
  const hasTidy = isSuperAdmin && (unassignedInst > 0 || unassignedLeads > 0 || idleReps.length > 0);

  return (
    <div className="mx-auto max-w-[1100px] space-y-6 p-4 sm:p-7">
      <PageHeading
        title="Team"
        description={isSuperAdmin
          ? "Who onboards institutions and helps them grow, and who reports to whom. You see all of it, whoever it is assigned to. Tap anyone to change who they report to or hand their work on."
          : "You and the people who report to you, with the institutions and leads each of you owns. Tap anyone to hand work to your team."}
      >
        {isSuperAdmin && <Link href="/staff" className="inline-flex h-9 items-center bg-primary px-4 text-[13px] font-semibold text-primary-foreground">Add a representative</Link>}
      </PageHeading>

      {isLoading ? <div className="skeleton h-64 w-full" aria-busy="true" /> : isError ? (
        <LoadError className="py-10" onRetry={() => refetch()} />
      ) : people.length === 0 ? (
        <EmptyState title="No one on the team yet" description="Representatives you add appear here, under whoever they report to." />
      ) : (
        <>
          {hasTidy && (
            <section aria-label="To tidy up" className="space-y-2 border border-border bg-card p-4">
              <h2 className="text-[13px] font-semibold text-muted-foreground">To tidy up</h2>
              {(unassignedInst > 0 || unassignedLeads > 0) && (
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="text-[13.5px]">
                    {[unassignedInst > 0 && plural(unassignedInst, "institution"), unassignedLeads > 0 && plural(unassignedLeads, "lead")].filter(Boolean).join(" and ")} {unassignedInst + unassignedLeads === 1 ? "has" : "have"} no owner, so only SuperAdmins can see {unassignedInst + unassignedLeads === 1 ? "it" : "them"}.
                  </p>
                  <Button size="sm" onClick={() => setOpen(UNASSIGNED)}>Assign them</Button>
                </div>
              )}
              {idleReps.length > 0 && (
                <p className="text-[13.5px] text-muted-foreground">
                  {idleReps.map((r) => r.name).join(", ")} {idleReps.length === 1 ? "has" : "have"} nothing assigned yet (dashed edge below).
                </p>
              )}
            </section>
          )}

          <Card>
            <CardContent className="flex flex-wrap items-center gap-x-8 gap-y-3 p-4 sm:p-5">
              {[
                ["Representatives", reps.length],
                ["Institutions", people.reduce((n, p) => n + p.institutions, 0)],
                ["Active", people.reduce((n, p) => n + p.activeInstitutions, 0)],
                ["Members", people.reduce((n, p) => n + p.members, 0)],
                ["Open leads", people.reduce((n, p) => n + p.openLeads, 0)],
                ["Leads won", people.reduce((n, p) => n + p.convertedLeads, 0)],
              ].map(([label, value]) => (
                <div key={label as string}>
                  <p className="text-[18px] font-bold tabular-nums leading-tight">{(value as number).toLocaleString()}</p>
                  <p className="text-[12px] text-muted-foreground">{label}</p>
                </div>
              ))}
            </CardContent>
          </Card>

          <div className="space-y-5">
            {isSuperAdmin && (
              <>
                <div className="mx-auto w-full max-w-[560px] border border-border bg-card p-4 text-center sm:p-5">
                  <div className="mx-auto flex size-12 items-center justify-center bg-primary/10 text-primary"><Sitemap size={22} /></div>
                  <h2 className="mt-2.5 text-[17px] font-bold leading-tight">Platform team</h2>
                  <p className="mt-0.5 text-[12.5px] text-muted-foreground">
                    {plural(supers.length, "SuperAdmin")} · {plural(reps.length, "representative")}. SuperAdmins see everything.
                  </p>
                </div>
                <div aria-hidden className="mx-auto h-5 w-0.5 bg-border" />
              </>
            )}

            <ul className="relative space-y-2.5 border-l-2 border-border pl-4 sm:pl-5">
              {tree.map((n) => <Branch key={n.id} node={n} myId={myId} onOpen={setOpen} />)}
              {isSuperAdmin && (unassignedInst > 0 || unassignedLeads > 0) && (
                <li className="relative before:absolute before:-left-4 before:top-6 before:h-0.5 before:w-4 before:bg-border sm:before:-left-5 sm:before:w-5">
                  <button
                    type="button"
                    onClick={() => setOpen(UNASSIGNED)}
                    className="w-full border border-dashed border-warning/70 bg-card px-3.5 py-3 text-left transition-colors hover:bg-muted/50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary"
                  >
                    <span className="block text-[14.5px] font-semibold">No owner</span>
                    <span className="block text-[12.5px] text-muted-foreground">
                      {plural(unassignedInst, "institution")} · {plural(unassignedLeads, "lead")} waiting to be handed to someone
                    </span>
                  </button>
                </li>
              )}
            </ul>
          </div>

          <Card className="border-border/40">
            <CardContent className="flex flex-wrap items-center gap-x-5 gap-y-1.5 p-3.5 text-[12.5px] text-muted-foreground">
              <span className="font-semibold text-foreground">Key</span>
              <span className="inline-flex items-center gap-1.5"><span className="inline-block h-3 w-5 border border-border" /> has work</span>
              <span className="inline-flex items-center gap-1.5"><span className="inline-block h-3 w-5 border border-dashed border-warning/70" /> nothing assigned yet</span>
              <span>Representatives see their own work and their team&apos;s. They can onboard institutions but not approve settlement details.</span>
            </CardContent>
          </Card>
        </>
      )}

      {open && data && (
        <ManageDialog
          personId={open}
          people={people}
          institutions={instPage?.results ?? []}
          leads={leads ?? []}
          canManage={isSuperAdmin}
          onClose={() => setOpen(null)}
        />
      )}
    </div>
  );
}
