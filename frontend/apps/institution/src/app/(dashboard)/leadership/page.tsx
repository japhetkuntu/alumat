"use client";

import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { useQuery } from "@tanstack/react-query";
import { Button, Card, ConfirmModal, EmptyState, FormSheet, LoadError, MoreHorizontal, Pencil, Plus, UserAvatar, DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@alumni/ui";
import { handleApiError } from "@/lib/api-client";
import { createLeadershipRole, deleteLeadershipRole, getLeadership, getLeadershipSettings, updateLeadershipSettings, updateLeadershipRole, type LeadershipRole, type LeadershipScopeType } from "@/lib/institution-api";
import { AppointWhere, LeadershipDialog, RoleFields, summarise, useLeadershipRoles, usePrivileges } from "@/components/institution/leadership";
import { DEFAULT_COHORT_LABEL, DEFAULT_COHORT_LABEL_PLURAL, useInstitutionNavTheme } from "@/components/institution/institution-layout";
import { useAuth } from "@/hooks/use-auth";

type Editing = LeadershipRole | "new" | null;

export default function LeadershipRolesPage() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const roles = useLeadershipRoles(user?.role === "SuperAdmin");
  const catalogue = usePrivileges(user?.role === "SuperAdmin");
  const [editing, setEditing] = useState<Editing>(null);
  const [name, setName] = useState("");
  const [privileges, setPrivileges] = useState<string[]>([]);
  const [deleting, setDeleting] = useState<LeadershipRole | null>(null);
  const [choosing, setChoosing] = useState(false);
  const [target, setTarget] = useState<{ scopeType: LeadershipScopeType; scopeId: string; scopeName: string; adding: boolean } | null>(null);
  const { data: navTheme } = useInstitutionNavTheme();
  const cohort = navTheme?.cohortLabel?.trim() || DEFAULT_COHORT_LABEL;
  const cohortPlural = navTheme?.cohortLabelPlural?.trim() || DEFAULT_COHORT_LABEL_PLURAL;
  const isAdmin = user?.role === "SuperAdmin";
  const posts = useQuery({ queryKey: ["leadership", "all"], queryFn: () => getLeadership(), enabled: isAdmin, staleTime: 30_000 });

  const settings = useQuery({ queryKey: ["leadership-settings"], queryFn: getLeadershipSettings, enabled: isAdmin, staleTime: 60_000 });
  const saveLimit = useMutation({
    mutationFn: (n: number) => updateLeadershipSettings({ groupMessagesPerDay: n }),
    onSuccess: (r) => { qc.setQueryData(["leadership-settings"], r); toast.success(`Leaders can send ${r.groupMessagesPerDay} ${r.groupMessagesPerDay === 1 ? "message" : "messages"} a day`); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const open = (r: Editing) => { setEditing(r); setName(r && r !== "new" ? r.name : ""); setPrivileges(r && r !== "new" ? r.privileges : []); };
  const refresh = () => { qc.invalidateQueries({ queryKey: ["leadership-roles"] }); qc.invalidateQueries({ queryKey: ["leadership"] }); };

  const save = useMutation({
    mutationFn: () => editing && editing !== "new"
      ? updateLeadershipRole(editing.id, { name: name.trim(), privileges })
      : createLeadershipRole({ name: name.trim(), privileges }),
    onSuccess: (r) => { refresh(); setEditing(null); toast.success(`${r.name} saved`); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const remove = useMutation({
    mutationFn: (r: LeadershipRole) => deleteLeadershipRole(r.id),
    onSuccess: () => { refresh(); setDeleting(null); toast.success("Role deleted"); },
    onError: (e) => { setDeleting(null); toast.error(handleApiError(e)); },
  });

  if (user && user.role !== "SuperAdmin") return <div className="p-8"><h1 className="text-2xl font-bold">Not available</h1><p className="mt-2 text-muted-foreground">Only a full administrator can manage leadership roles.</p></div>;

  const list = roles.data ?? [];

  return (
    <div className="mx-auto max-w-[900px] space-y-5 p-4 sm:p-[26px]">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="max-w-[60ch]">
          <h1 className="m-0 text-[20px] font-bold sm:text-[25px]">Leadership</h1>
          <p className="mt-1.5 text-[13px] text-muted-foreground">
            Make the roles your leaders hold, with your own names, and choose what each one can do. Then appoint people to them from a year group or a group.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => open("new")}><Plus size={16} />New role</Button>
          <Button onClick={() => setChoosing(true)}><Plus size={16} />Appoint a leader</Button>
        </div>
      </header>

      <Card className="overflow-hidden">
        <div className="flex items-center justify-between gap-3 border-b border-border px-4 py-3">
          <h2 className="m-0 text-[14px] font-semibold">Who leads what</h2>
          <span className="text-[12.5px] text-muted-foreground">{(posts.data ?? []).length} appointed</span>
        </div>
        {posts.isLoading ? <div className="h-20 animate-pulse bg-muted/50" /> : (posts.data ?? []).length === 0 ? (
          <EmptyState className="py-8" title="No leaders appointed yet"
            description={`Pick a ${cohort.toLowerCase()} or group and appoint a member to lead it. You can also do this from the ${cohortPlural} and Groups pages.`} />
        ) : (
          <ul className="divide-y divide-border">
            {(posts.data ?? []).map((p) => (
              <li key={p.id}>
                <button type="button" onClick={() => setTarget({ scopeType: p.scopeType, scopeId: p.scopeId, scopeName: p.scopeName, adding: false })}
                  className="flex w-full items-center gap-3 px-4 py-3 text-left transition hover:bg-muted/50">
                  <UserAvatar src={p.profilePictureUrl} name={p.memberName} size="sm" />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-[14px] font-medium leading-tight">{p.memberName}</span>
                    <span className="block truncate text-[12.5px] text-muted-foreground">{p.title} · {p.scopeName}</span>
                  </span>
                  <span className="shrink-0 text-[12.5px] text-muted-foreground">Manage</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card className="overflow-hidden">
        {roles.isError ? (
          <LoadError className="py-8" onRetry={() => roles.refetch()} />
        ) : roles.isLoading ? (
          <div className="space-y-px" aria-busy="true" aria-label="Loading roles">{[0, 1, 2].map((i) => <div key={i} className="h-[76px] animate-pulse bg-muted/50" />)}</div>
        ) : list.length === 0 ? (
          <EmptyState className="py-10" title="No roles yet"
            description="A role is a name you choose, like Treasurer or Chapter Convener, and what that person is allowed to do. You can also make one while appointing someone." />
        ) : (
          <ul className="divide-y divide-border">
            {list.map((r) => (
              <li key={r.id} className="flex items-start gap-3 p-4">
                <div className="min-w-0 flex-1">
                  <p className="text-[15px] font-semibold leading-tight">{r.name}</p>
                  <p className="mt-1 text-[13px] leading-snug text-muted-foreground">{summarise(r.privileges, catalogue.data ?? [])}</p>
                  <p className="mt-1 text-[12.5px] text-muted-foreground/80">{r.holderCount === 0 ? "Nobody holds it yet" : `${r.holderCount} ${r.holderCount === 1 ? "appointment" : "appointments"}`}</p>
                </div>
                <Button size="sm" variant="outline" onClick={() => open(r)}>Edit</Button>
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button size="sm" variant="outline" aria-label={`More actions for ${r.name}`} className="px-3"><MoreHorizontal size={15} /></Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end" className="min-w-[180px]">
                    <DropdownMenuItem onSelect={() => open(r)}><Pencil size={14} />Edit role</DropdownMenuItem>
                    <DropdownMenuItem destructive disabled={r.holderCount > 0} onSelect={() => setDeleting(r)}>{r.holderCount > 0 ? "Delete (in use)" : "Delete"}</DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card className="p-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="max-w-[48ch]">
            <h2 className="m-0 text-[14px] font-semibold">Messages to a group</h2>
            <p className="mt-1 text-[12.5px] leading-snug text-muted-foreground">
              How many messages one leader can send to the same group in a day. Messages can&apos;t be taken back, so a low number keeps groups from being flooded.
            </p>
          </div>
          <div className="flex items-center gap-2" role="group" aria-label="Messages a leader can send a group each day">
            {settings.isLoading ? <div className="h-10 w-48 animate-pulse bg-muted" /> : (
              Array.from({ length: settings.data?.maxGroupMessagesPerDay ?? 10 }, (_, i) => i + 1).filter((n) => n <= 5 || n === 10 || n === settings.data?.groupMessagesPerDay).map((n) => (
                <button key={n} type="button" aria-pressed={settings.data?.groupMessagesPerDay === n} disabled={saveLimit.isPending}
                  onClick={() => settings.data?.groupMessagesPerDay !== n && saveLimit.mutate(n)}
                  className={`grid size-10 place-items-center rounded-full text-[14px] font-semibold tabular-nums ring-1 transition ${settings.data?.groupMessagesPerDay === n ? "bg-primary text-primary-foreground ring-primary" : "ring-border text-muted-foreground hover:bg-muted/50"}`}>{n}</button>
              ))
            )}
            <span className="text-[12.5px] text-muted-foreground">a day</span>
          </div>
        </div>
      </Card>

      <FormSheet
        open={editing !== null}
        onClose={() => setEditing(null)}
        title={editing && editing !== "new" ? "Edit role" : "New role"}
        description={editing && editing !== "new" && editing.holderCount > 0 ? `Changes apply to everyone who holds it (${editing.holderCount}).` : undefined}
        formId="role-form"
        submitLabel="Save role"
        saving={save.isPending}
        submitDisabled={!name.trim()}
      >
        <form id="role-form" onSubmit={(e) => { e.preventDefault(); if (name.trim()) save.mutate(); }}>
          <RoleFields idPrefix="role" autoFocus name={name} onName={setName} privileges={privileges} onPrivileges={setPrivileges} />
        </form>
      </FormSheet>

      <AppointWhere open={choosing} onClose={() => setChoosing(false)}
        onChoose={(t) => { setChoosing(false); setTarget({ scopeType: t.scopeType, scopeId: t.scopeId, scopeName: t.scopeName, adding: true }); }} />

      {target && (
        <LeadershipDialog key={`${target.scopeType}-${target.scopeId}-${target.adding}`} scopeType={target.scopeType} scopeId={target.scopeId} scopeName={target.scopeName}
          noun={target.scopeType === "Batch" ? cohort.toLowerCase() : "group"} startAdding={target.adding} onClose={() => setTarget(null)} />
      )}

      <ConfirmModal
        open={!!deleting}
        title={deleting ? `Delete ${deleting.name}?` : ""}
        message="Nobody holds this role, so nothing changes for anyone. You can make it again later."
        confirmLabel="Delete"
        isLoading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting)}
        onCancel={() => setDeleting(null)}
      />
    </div>
  );
}
