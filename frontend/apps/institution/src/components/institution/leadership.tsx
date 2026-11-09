"use client";

import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import Link from "next/link";
import { Button, CheckOption, ConfirmModal, FormSection, FormSheet, DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger, Input, Label, MoreHorizontal, Plus, Select, SelectContent, SelectItem, SelectTrigger, SelectValue, UserAvatar, cn } from "@alumni/ui";
import { handleApiError } from "@/lib/api-client";
import {
  appointLeader, changeLeaderRole, createLeadershipRole, getBatches, getCommunities, getLeadership, getLeadershipPrivileges, getLeadershipRoles, getMembers, removeLeader,
  type LeadershipPost, type LeadershipPrivilege, type LeadershipRole, type LeadershipScopeType,
} from "@/lib/institution-api";

import { DEFAULT_COHORT_LABEL, useInstitutionNavTheme } from "@/components/institution/institution-layout";

const errorText = (e: unknown): string | null => (e ? handleApiError(e) : null);

const key = (scopeType: LeadershipScopeType) => ["leadership", scopeType] as const;

/** Every post of one kind of group in a single request, so a whole table of groups costs one call. Pick a group's posts with `forScope`. */
export function useLeadership(scopeType: LeadershipScopeType, enabled = true) {
  const q = useQuery({ queryKey: key(scopeType), queryFn: () => getLeadership({ scopeType }), enabled, staleTime: 30_000 });
  return { ...q, forScope: (scopeId: string) => (q.data ?? []).filter((p) => p.scopeId === scopeId) };
}

/** What a role can be allowed to do, as the server lists it. */
export function usePrivileges(enabled = true) {
  return useQuery({ queryKey: ["leadership-privileges"], queryFn: getLeadershipPrivileges, enabled, staleTime: 10 * 60_000 });
}

export function useLeadershipRoles(enabled = true) {
  return useQuery({ queryKey: ["leadership-roles"], queryFn: getLeadershipRoles, enabled, staleTime: 30_000 });
}

/** "Can welcome and approve members and message the whole group", or "Recognition only". */
export function summarise(privileges: string[], catalogue: LeadershipPrivilege[]): string {
  const labels = catalogue.filter((p) => privileges.includes(p.key)).map((p) => p.label.charAt(0).toLowerCase() + p.label.slice(1));
  if (labels.length === 0) return "Recognition only";
  return `Can ${labels.length === 1 ? labels[0] : `${labels.slice(0, -1).join(", ")} and ${labels[labels.length - 1]}`}`;
}

/** The people who lead a group, as a short run of overlapping photos with the first title beside them. Nothing when nobody does. */
export function LeaderStack({ posts, onClick }: { posts: LeadershipPost[]; onClick?: () => void }) {
  if (posts.length === 0) return onClick
    ? (
      <button type="button" onClick={onClick}
        className="inline-flex min-h-9 items-center gap-1.5 rounded-full border border-dashed border-primary/50 px-3 text-[12.5px] font-medium text-primary transition hover:border-primary hover:bg-primary/5">
        <Plus size={12} />Appoint leader
      </button>
    )
    : <span className="text-[12.5px] text-muted-foreground">No leaders yet</span>;
  const shown = posts.slice(0, 3);
  const first = posts[0];
  const Wrapper = onClick ? "button" : "div";
  return (
    <div className="flex max-w-[260px] items-center gap-2">
      <Wrapper {...(onClick ? { type: "button" as const, onClick, "aria-label": `Manage leaders, ${posts.length} appointed` } : {})} className="flex min-w-0 items-center gap-2.5 text-left">
        <span className="flex shrink-0 -space-x-2">
          {shown.map((p) => <UserAvatar key={p.id} src={p.profilePictureUrl} name={p.memberName} size="sm" className="ring-2 ring-background" />)}
        </span>
        <span className="min-w-0 text-[12.5px] leading-tight">
          <span className="block truncate font-medium">{first.memberName}{posts.length > 1 && <span className="font-normal text-muted-foreground"> +{posts.length - 1}</span>}</span>
          <span className="block truncate text-muted-foreground">{first.title}</span>
        </span>
      </Wrapper>
      {onClick && (
        <button type="button" onClick={onClick} aria-label="Appoint another leader" title="Appoint another leader"
          className="grid size-7 shrink-0 place-items-center rounded-full border border-dashed border-primary/50 text-primary transition hover:border-primary hover:bg-primary/5"><Plus size={12} /></button>
      )}
    </div>
  );
}

/** A role's name and the things it may do, as the form used both when making a role on its own and when making one while appointing. */
export function RoleFields({ idPrefix, name, onName, privileges, onPrivileges, autoFocus }: {
  idPrefix: string; name: string; onName: (v: string) => void; privileges: string[]; onPrivileges: (v: string[]) => void; autoFocus?: boolean;
}) {
  const catalogue = usePrivileges();
  return (
    <div className="space-y-4">
      <div className="space-y-2">
        <Label htmlFor={`${idPrefix}-name`}>Role name</Label>
        <Input id={`${idPrefix}-name`} autoFocus={autoFocus} maxLength={60} placeholder="e.g. Class Coordinator, Treasurer, Chapter Convener" value={name} onChange={(e) => onName(e.target.value)} />
      </div>
      <fieldset className="space-y-2">
        <legend className="mb-1 text-[13px] font-semibold">What can they do?</legend>
        <p className="text-[12.5px] leading-relaxed text-muted-foreground">Only inside the group they are appointed to. Leave all unticked for a title that is shown but gives no access.</p>
        {catalogue.isLoading ? <div className="h-24 animate-pulse rounded-[12px] bg-muted" /> : (
          <div className="space-y-2">
            {(catalogue.data ?? []).map((p) => (
              <CheckOption key={p.key} id={`${idPrefix}-${p.key}`} label={p.label} hint={p.description} checked={privileges.includes(p.key)}
                onChange={(on) => onPrivileges(on ? [...privileges, p.key] : privileges.filter((k) => k !== p.key))} />
            ))}
          </div>
        )}
      </fieldset>
    </div>
  );
}

function useDebounced<T>(value: T, ms = 250) {
  const [v, setV] = useState(value);
  useEffect(() => { const t = setTimeout(() => setV(value), ms); return () => clearTimeout(t); }, [value, ms]);
  return v;
}

/**
 * Choosing a role: the institution's own roles, each with what it allows, or a new one made on the spot with its own name and privileges.
 * Used both when appointing someone and when changing the role they hold.
 */
function RolePicker({ roles, selected, making, onSelect, onMake, name, onName, privileges, onPrivileges, label = "Their role" }: {
  roles: LeadershipRole[]; selected: string | null; making: boolean; onSelect: (id: string) => void; onMake: () => void;
  name: string; onName: (v: string) => void; privileges: string[]; onPrivileges: (v: string[]) => void; label?: string;
}) {
  const catalogue = usePrivileges();
  const noRoles = roles.length === 0;
  return (
    <div className="min-w-0 space-y-4">
      {!noRoles && (
        <div className="space-y-2">
          <Label className="sr-only">{label}</Label>
          <div role="radiogroup" aria-label={label} className="space-y-2">
            {roles.map((r) => {
              const on = !making && selected === r.id;
              return (
                <button key={r.id} type="button" role="radio" aria-checked={on} onClick={() => onSelect(r.id)}
                  className={cn("flex w-full items-start gap-3 rounded-[14px] p-3 text-left ring-1 transition", on ? "bg-primary/5 ring-primary/60" : "ring-border hover:bg-muted/50")}>
                  <span className={cn("mt-0.5 grid size-4 shrink-0 place-items-center rounded-full ring-1", on ? "bg-primary ring-primary" : "ring-border")}>{on && <span className="size-1.5 rounded-full bg-primary-foreground" />}</span>
                  <span className="min-w-0 flex-1">
                    <span className="block text-[14px] font-semibold leading-tight">{r.name}</span>
                    <span className="mt-0.5 block text-[12.5px] leading-snug text-muted-foreground">{summarise(r.privileges, catalogue.data ?? [])}</span>
                  </span>
                </button>
              );
            })}
            <button type="button" role="radio" aria-checked={making} onClick={onMake}
              className={cn("flex w-full items-center gap-3 rounded-[14px] p-3 text-left text-[14px] font-medium ring-1 transition", making ? "bg-primary/5 ring-primary/60" : "text-muted-foreground ring-border hover:bg-muted/50 hover:text-foreground")}>
              <span className="grid size-4 shrink-0 place-items-center text-[16px] leading-none">+</span> A new role
            </button>
          </div>
        </div>
      )}
      {(making || noRoles) && <RoleFields idPrefix="picker-role" autoFocus={!noRoles} name={name} onName={onName} privileges={privileges} onPrivileges={onPrivileges} />}
      <p className="text-[12.5px] text-muted-foreground">
        To change what a role can do, <Link href="/leadership" className="font-medium text-foreground underline underline-offset-2">edit it under Leadership</Link>. It updates for everyone who holds it.
      </p>
    </div>
  );
}

export type AppointTarget = { scopeType: LeadershipScopeType; scopeId: string; scopeName: string };

/**
 * The first step of appointing from somewhere that is not a year group or group: choose where they will lead. Hands back the choice;
 * the caller opens the appointment flow for it.
 */
export function AppointWhere({ open, onClose, onChoose, title = "Appoint a leader", description = "Choose where they will lead. You pick the person and their role next." }: {
  open: boolean; onClose: () => void; onChoose: (t: AppointTarget & { noun: string }) => void; title?: string; description?: string;
}) {
  const { data: navTheme } = useInstitutionNavTheme();
  const cohort = navTheme?.cohortLabel?.trim() || DEFAULT_COHORT_LABEL;
  const [type, setType] = useState<"Batch" | "Community">("Batch");
  const [id, setId] = useState("");
  const batches = useQuery({ queryKey: ["batches"], queryFn: getBatches, enabled: open, staleTime: 60_000 });
  const groups = useQuery({ queryKey: ["communities"], queryFn: getCommunities, enabled: open, staleTime: 60_000 });
  const options = type === "Batch" ? (batches.data ?? []).map((b) => ({ id: b.id, name: b.name })) : (groups.data ?? []).map((c) => ({ id: c.id, name: c.name }));
  const loading = (type === "Batch" ? batches : groups).isLoading;
  const go = () => {
    const o = options.find((x) => x.id === id);
    if (!o) return;
    onChoose({ scopeType: type, scopeId: o.id, scopeName: o.name, noun: type === "Batch" ? cohort.toLowerCase() : "group" });
    setId("");
  };
  return (
    <FormSheet open={open} onClose={onClose} title={title} description={description} formId="appoint-where" submitLabel="Continue" submitDisabled={!id}>
      <form id="appoint-where" className="space-y-4" onSubmit={(e) => { e.preventDefault(); go(); }}>
        <div role="radiogroup" aria-label="Where" className="grid grid-cols-2 gap-2">
          {([["Batch", cohort], ["Community", "Group"]] as const).map(([t, label]) => (
            <button key={t} type="button" role="radio" aria-checked={type === t} onClick={() => { setType(t); setId(""); }}
              className={cn("min-h-11 rounded-[12px] px-3 text-[14px] font-medium ring-1 transition", type === t ? "bg-primary/5 text-foreground ring-primary/60" : "text-muted-foreground ring-border hover:bg-muted/50")}>{label}</button>
          ))}
        </div>
        <div className="space-y-2">
          <Label>{type === "Batch" ? cohort : "Group"}</Label>
          <Select value={id} onValueChange={setId}>
            <SelectTrigger aria-label="Where they will lead" className="w-full"><SelectValue placeholder={type === "Batch" ? `Choose a ${cohort.toLowerCase()}` : "Choose a group"} /></SelectTrigger>
            <SelectContent>
              {options.length === 0
                ? <div className="px-3 py-2 text-[13px] text-muted-foreground">{loading ? "Loading…" : "Nothing to choose yet"}</div>
                : options.map((o) => <SelectItem key={o.id} value={o.id}>{o.name}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
      </form>
    </FormSheet>
  );
}

type Mode = { kind: "list" } | { kind: "add" } | { kind: "change"; post: LeadershipPost };

/**
 * Who leads one group, and the way to change it. Appointing is one short flow in one view: find the person, choose their role (or make a
 * new one on the spot). A leader's role can be changed in place, and they can be removed. The institution chooses every word and privilege.
 */
export function LeadershipDialog({ scopeType, scopeId, scopeName, noun, onClose, startAdding, presetMember }: {
  scopeType: LeadershipScopeType; scopeId: string; scopeName: string; noun: string; onClose: () => void;
  /** Opens straight on "Appoint someone", for when the person came here to appoint. */
  startAdding?: boolean;
  /** Appointing someone already chosen, such as from their own profile: skips the search and goes to their role. */
  presetMember?: { id: string; name: string; photo?: string | null };
}) {
  const qc = useQueryClient();
  const posts = useLeadership(scopeType);
  const current = posts.forScope(scopeId);
  const catalogue = usePrivileges();
  const roles = useLeadershipRoles();

  const [mode, setMode] = useState<Mode>(startAdding ? { kind: "add" } : { kind: "list" });
  const [search, setSearch] = useState("");
  const [picked, setPicked] = useState<{ id: string; name: string; photo?: string | null } | null>(presetMember ?? null);
  const [roleId, setRoleId] = useState<string | null>(null);
  const [making, setMaking] = useState(false);
  const [newName, setNewName] = useState("");
  const [newPrivileges, setNewPrivileges] = useState<string[]>([]);
  const [removing, setRemoving] = useState<LeadershipPost | null>(null);
  const roleStepRef = useRef<HTMLDivElement>(null);

  const adding = mode.kind === "add";
  const changing = mode.kind === "change" ? mode.post : null;
  const q = useDebounced(search.trim());
  const people = useQuery({
    queryKey: ["leadership-member-search", q],
    queryFn: () => getMembers({ search: q, status: "Active", pageSize: 6 }),
    enabled: adding && !picked && q.length >= 2,
  });

  const roleList = roles.data ?? [];
  const noRoles = !roles.isLoading && roleList.length === 0;
  const makingNew = making || noRoles;
  const reset = () => { setMode({ kind: "list" }); setSearch(""); setPicked(null); setRoleId(null); setMaking(false); setNewName(""); setNewPrivileges([]); };
  const refresh = () => {
    qc.invalidateQueries({ queryKey: key(scopeType) });
    qc.invalidateQueries({ queryKey: ["leadership-roles"] });
    qc.invalidateQueries({ queryKey: ["leadership", "member"] });
  };
  const resolveRole = async () => (makingNew ? (await createLeadershipRole({ name: newName.trim(), privileges: newPrivileges })).id : roleId!);

  const appoint = useMutation({
    mutationFn: async () => appointLeader({ memberId: picked!.id, roleId: await resolveRole(), scopeType, scopeId: scopeType === "Institution" ? undefined : scopeId }),
    onSuccess: (p) => { refresh(); toast.success(`${p.memberName} is now ${p.title}`); reset(); },
    onError: (e) => { refresh(); toast.error(handleApiError(e)); },
  });

  const change = useMutation({
    mutationFn: async () => changeLeaderRole(changing!.id, await resolveRole()),
    onSuccess: (p) => { refresh(); toast.success(`${p.memberName} is now ${p.title}`); reset(); },
    onError: (e) => { refresh(); toast.error(handleApiError(e)); },
  });

  const remove = useMutation({
    mutationFn: (p: LeadershipPost) => removeLeader(p.id),
    onSuccess: () => { refresh(); setRemoving(null); toast.success("Removed"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const picker = (
    <RolePicker roles={roleList} selected={roleId} making={making} label={changing ? "New role" : "Their role"}
      onSelect={(id) => { setRoleId(id); setMaking(false); }} onMake={() => { setMaking(true); setRoleId(null); }}
      name={newName} onName={setNewName} privileges={newPrivileges} onPrivileges={setNewPrivileges} />
  );
  const ready = makingNew ? newName.trim().length > 0 : !!roleId && roleId !== changing?.roleId;

  const sheet = mode.kind === "list"
    ? { title: "Leadership", description: scopeName, formId: "leadership-list", hideSubmit: true, submitLabel: "", saving: false, error: null as string | null, disabled: false }
    : adding
      ? { title: "Appoint someone", description: scopeName, formId: "leadership-add", hideSubmit: false, submitLabel: "Appoint", saving: appoint.isPending, error: errorText(appoint.error), disabled: !picked || !ready }
      : { title: "Change role", description: `${changing!.memberName} · ${scopeName}`, formId: "leadership-change", hideSubmit: false, submitLabel: "Save role", saving: change.isPending, error: errorText(change.error), disabled: !ready };
  const back = (
    <button type="button" onClick={reset} className="mb-4 inline-flex min-h-10 items-center text-[13px] text-muted-foreground hover:text-foreground">← Back to leadership</button>
  );

  return (
    <>
      <FormSheet open onClose={onClose} title={sheet.title} description={sheet.description} formId={sheet.formId} hideSubmit={sheet.hideSubmit}
        cancelLabel={mode.kind === "list" ? "Close" : "Cancel"} submitLabel={sheet.submitLabel} saving={sheet.saving} submitDisabled={sheet.disabled} error={sheet.error}>
        {mode.kind === "list" && (
          <form id="leadership-list" onSubmit={(e) => e.preventDefault()} className="min-w-0 space-y-4">
            {posts.isLoading ? (
              <div className="h-14 animate-pulse bg-muted" />
            ) : current.length === 0 ? (
              <p className="bg-muted/50 px-4 py-5 text-[13.5px] leading-relaxed text-muted-foreground">
                Nobody leads this {noun} yet. Members see the people you appoint here, with a button to message them.
              </p>
            ) : (
              <ul className="divide-y divide-border">
                {current.map((p) => (
                  <li key={p.id} className="flex items-start gap-3 py-3">
                    <UserAvatar src={p.profilePictureUrl} name={p.memberName} size="default" />
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-[14px] font-semibold leading-tight">{p.memberName}</p>
                      <p className="truncate text-[12.5px] font-medium text-primary">{p.title}</p>
                      <p className="mt-0.5 text-[12px] leading-snug text-muted-foreground">{summarise(p.privileges, catalogue.data ?? [])}</p>
                    </div>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <button type="button" aria-label={`Options for ${p.memberName}`}
                          className="grid size-10 shrink-0 place-items-center rounded-full text-muted-foreground transition hover:bg-muted hover:text-foreground sm:size-9"><MoreHorizontal size={16} /></button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end" className="min-w-[170px]">
                        <DropdownMenuItem onSelect={() => { setMode({ kind: "change", post: p }); setRoleId(null); }}>Change role</DropdownMenuItem>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem destructive onSelect={() => setRemoving(p)}>Remove</DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </li>
                ))}
              </ul>
            )}
            <Button type="button" onClick={() => setMode({ kind: "add" })} className="w-full">{current.length === 0 ? "Appoint the first leader" : "Appoint someone"}</Button>
          </form>
        )}

        {adding && (
          <form id="leadership-add" className="min-w-0 space-y-5" onSubmit={(e) => { e.preventDefault(); if (picked && ready) appoint.mutate(); }}>
            {back}
            {!picked ? (
              <FormSection title="Who">
                <div className="space-y-2">
                  <Label htmlFor="lead-search">Member</Label>
                  <Input id="lead-search" autoFocus autoComplete="off" placeholder="Search members by name" value={search} onChange={(e) => setSearch(e.target.value)} />
                  <div className="min-h-[3rem]">
                    {q.length < 2 ? (
                      <p className="px-1 pt-1 text-[12.5px] text-muted-foreground">Type at least two letters.</p>
                    ) : people.isFetching && !people.data ? (
                      <div className="h-12 animate-pulse bg-muted" />
                    ) : (people.data?.results ?? []).length === 0 ? (
                      <p className="px-1 pt-1 text-[12.5px] text-muted-foreground">No active member matches &ldquo;{q}&rdquo;.</p>
                    ) : (
                      <ul className="-mx-1 max-h-[320px] overflow-y-auto">
                        {people.data!.results.map((m) => (
                          <li key={m.id}>
                            <button type="button" onClick={() => setPicked({ id: m.id, name: `${m.firstName} ${m.lastName}`.trim(), photo: m.profilePictureUrl })}
                              className="flex min-h-12 w-full items-center gap-3 px-2 py-2 text-left transition hover:bg-muted">
                              <UserAvatar src={m.profilePictureUrl} name={`${m.firstName} ${m.lastName}`} size="sm" />
                              <span className="min-w-0">
                                <span className="block truncate text-[14px] font-medium leading-tight">{m.firstName} {m.lastName}</span>
                                <span className="block truncate text-[12px] text-muted-foreground">{[m.graduationYear > 0 ? m.graduationYear : null, m.email].filter(Boolean).join(" · ")}</span>
                              </span>
                            </button>
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                </div>
              </FormSection>
            ) : (
              <>
                <FormSection title="Who">
                  <div className="flex items-center gap-3 bg-muted/50 p-3">
                    <UserAvatar src={picked.photo} name={picked.name} size="default" />
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-[14px] font-semibold leading-tight">{picked.name}</p>
                      <p className="truncate text-[12.5px] text-muted-foreground">{scopeName}</p>
                    </div>
                    <button type="button" onClick={() => { setPicked(null); setRoleId(null); }} className="shrink-0 text-[12.5px] text-muted-foreground underline-offset-2 hover:text-foreground hover:underline">Change</button>
                  </div>
                </FormSection>
                <FormSection title="Their role" hint="Choose one of your roles, or make a new one.">{picker}</FormSection>
              </>
            )}
          </form>
        )}

        {changing && (
          <form id="leadership-change" className="min-w-0 space-y-5" onSubmit={(e) => { e.preventDefault(); if (ready) change.mutate(); }}>
            {back}
            <FormSection title="Now">
              <p className="bg-muted/50 px-3.5 py-3 text-[13px] leading-snug text-muted-foreground">
                <span className="font-semibold text-foreground">{changing.title}</span>. {summarise(changing.privileges, catalogue.data ?? [])}. They stay in the {noun} and are told about the change.
              </p>
            </FormSection>
            <FormSection title="New role">{picker}</FormSection>
          </form>
        )}
      </FormSheet>

      <ConfirmModal
        open={!!removing}
        title={removing ? `Remove ${removing.memberName} as ${removing.title}?` : ""}
        message={removing && removing.privileges.length > 0
          ? "They lose what this role allowed, and the title disappears from their profile. They stay a member, and you can appoint them again at any time."
          : "The title disappears from their profile and from this page. They stay a member, and you can appoint them again at any time."}
        confirmLabel="Remove"
        isLoading={remove.isPending}
        onConfirm={() => removing && remove.mutate(removing)}
        onCancel={() => setRemoving(null)}
      />
    </>
  );
}
