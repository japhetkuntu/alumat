"use client";

import { LoadError } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus } from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { toast } from "sonner";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { FormSheet, FormSection } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { SearchModal } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { YearGroupPicker } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { getInstitutionStaff, createInstitutionStaff, updateInstitutionStaff, resendStaffInvite, getCommunities, type CommunityListItem } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";
import { formatDate } from "@alumni/ui";
import type { InstitutionStaffUser, CreateInstitutionStaffRequest, UpdateInstitutionStaffRequest } from "@/types";
import { TableSkeleton } from "@alumni/ui";

const roles = ["SuperAdmin", "ScopedAdmin"] as const;

/** A scoped admin can be assigned at most one batch — cap the shared
 *  multi-select YearGroupPicker to its most recent selection rather than
 *  forking a single-select variant just for this form. */
function toSingleYear(years: number[]): number[] {
  return years.length > 0 ? [years[years.length - 1]] : [];
}

function CommunityCheckboxList({ communities, selected, onChange }: { communities: CommunityListItem[]; selected: string[]; onChange: (ids: string[]) => void }) {
  if (communities.length === 0) {
    return <p className="text-xs text-muted-foreground">No communities exist yet.</p>;
  }
  return (
    <div className="space-y-1.5 max-h-32 overflow-y-auto border border-border rounded-md p-2">
      {communities.map((c) => (
        <label key={c.id} className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={selected.includes(c.id)}
            onChange={(e) => onChange(e.target.checked ? [...selected, c.id] : selected.filter((id) => id !== c.id))}
          />
          {c.name}
        </label>
      ))}
    </div>
  );
}

function NewAdminForm({ id, onSave }: { id: string; onSave: (data: CreateInstitutionStaffRequest) => void }) {
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [role, setRole] = useState<typeof roles[number]>("SuperAdmin");
  const [yearGroups, setYearGroups] = useState<number[]>([]);
  const [communityIds, setCommunityIds] = useState<string[]>([]);

  const { data: communities = [] } = useQuery({
    queryKey: ["admin-communities-for-staff-form"],
    queryFn: getCommunities,
    enabled: role === "ScopedAdmin",
  });

  return (
    <form id={id} className="space-y-5" onSubmit={(e) => { e.preventDefault(); onSave({ firstName, lastName, email, phone: phone.trim() || undefined, role, yearGroups: role === "ScopedAdmin" ? yearGroups : undefined, communityIds: role === "ScopedAdmin" ? communityIds : undefined }); }}>
          <FormSection title="Who they are">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label>First name</Label>
                <Input value={firstName} onChange={(e) => setFirstName(e.target.value)} required />
              </div>
              <div className="space-y-2">
                <Label>Last name</Label>
                <Input value={lastName} onChange={(e) => setLastName(e.target.value)} required />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Email</Label>
              <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
            </div>
            <div className="space-y-2">
              <Label>Phone <span className="font-normal text-muted-foreground">(optional)</span></Label>
              <Input type="tel" value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="+233 24 000 0000" />
              <p className="text-[12.5px] text-muted-foreground">Needed only if platform staff should be able to reach this admin by SMS.</p>
            </div>
          </FormSection>

          <FormSection title="Sign-in">
            <p className="border border-border bg-muted/40 p-3 text-[13.5px] leading-relaxed">
              You do not set a password. We email {email.trim() || "them"} a link to choose their own, and they sign in with it. The link works for 24 hours; you can send a new one from this page.
            </p>
          </FormSection>
          <div className="space-y-2">
            <Label>Role</Label>
            <FormSelect value={role} onValueChange={(v) => setRole(v as typeof roles[number])} options={roles.map((r) => ({ value: r, label: r }))} />
            <p className="text-xs text-muted-foreground">
              {role === "SuperAdmin" && "Full access to everything, including managing other admins."}
              {role === "ScopedAdmin" && "Restricted to the one batch and the communities selected below."}
            </p>
          </div>
          {role === "ScopedAdmin" && (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Group or cohort</Label>
                <YearGroupPicker value={yearGroups} onChange={(years) => setYearGroups(toSingleYear(years))} />
              </div>
              <div className="space-y-2">
                <Label>Groups</Label>
                <CommunityCheckboxList communities={communities} selected={communityIds} onChange={setCommunityIds} />
              </div>
            </div>
          )}
          </form>
  );
}

function EditAdminForm({
  id,
  admin,
  onSave,
}: {
  id: string;
  admin: InstitutionStaffUser;
  onSave: (data: UpdateInstitutionStaffRequest) => void;
}) {
  const [firstName, setFirstName] = useState(admin.firstName);
  const [lastName, setLastName] = useState(admin.lastName);
  const [phone, setPhone] = useState(admin.phone ?? "");
  const [role, setRole] = useState<typeof roles[number]>(admin.role as typeof roles[number]);
  const [yearGroups, setYearGroups] = useState<number[]>(admin.yearGroups ?? []);
  const [communityIds, setCommunityIds] = useState<string[]>(admin.communityIds ?? []);
  const [isDisabled, setIsDisabled] = useState(!!admin.isDisabled);

  const { data: communities = [] } = useQuery({
    queryKey: ["admin-communities-for-staff-form"],
    queryFn: getCommunities,
    enabled: role === "ScopedAdmin",
  });

  return (
    <form
          id={id}
          className="space-y-5"
          onSubmit={(e) => {
            e.preventDefault();
            onSave({ firstName, lastName, phone: phone.trim() || undefined, role, yearGroups: role === "ScopedAdmin" ? yearGroups : undefined, communityIds: role === "ScopedAdmin" ? communityIds : undefined, isDisabled });
          }}
        >
          <FormSection title="Who they are">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label>First name</Label>
                <Input value={firstName} onChange={(e) => setFirstName(e.target.value)} required />
              </div>
              <div className="space-y-2">
                <Label>Last name</Label>
                <Input value={lastName} onChange={(e) => setLastName(e.target.value)} required />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Phone <span className="font-normal text-muted-foreground">(optional)</span></Label>
              <Input type="tel" value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="+233 24 000 0000" />
              <p className="text-[12.5px] text-muted-foreground">Needed only if platform staff should be able to reach this admin by SMS.</p>
            </div>
          </FormSection>
          <div className="space-y-2">
            <Label>Role</Label>
            <FormSelect value={role} onValueChange={(v) => setRole(v as typeof roles[number])} options={roles.map((r) => ({ value: r, label: r }))} />
          </div>
          {role === "ScopedAdmin" && (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Group or cohort</Label>
                <YearGroupPicker value={yearGroups} onChange={(years) => setYearGroups(toSingleYear(years))} />
              </div>
              <div className="space-y-2">
                <Label>Groups</Label>
                <CommunityCheckboxList communities={communities} selected={communityIds} onChange={setCommunityIds} />
              </div>
            </div>
          )}

          <div className="flex items-center gap-3">
            <label className="flex items-center gap-2">
              <input type="checkbox" checked={isDisabled} onChange={(e) => setIsDisabled(e.target.checked)} />
              <span className="text-sm">Disabled</span>
            </label>
          </div>

          </form>
  );
}

export default function AdminsPage() {
  const { user } = useAuth();
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [showCreate, setShowCreate] = useState(false);
  const [editingAdmin, setEditingAdmin] = useState<InstitutionStaffUser | null>(null);
  const [disableTarget, setDisableTarget] = useState<InstitutionStaffUser | null>(null);
  const pageSize = 20;
  const qc = useQueryClient();

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ["admin-admins", search, page],
    queryFn: () => getInstitutionStaff(page, pageSize, search || undefined),
    placeholderData: (prev) => prev,
  });

  const createMut = useMutation({
    mutationFn: (req: CreateInstitutionStaffRequest) => createInstitutionStaff(req),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-admins"] }); setShowCreate(false); toast.success("Admin added. We emailed them a link to choose their password."); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const resendMut = useMutation({
    mutationFn: (id: string) => resendStaffInvite(id),
    onSuccess: () => toast.success("A new link has been emailed."),
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: ([id, req]: [string, UpdateInstitutionStaffRequest]) => updateInstitutionStaff(id, req),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-admins"] }); setEditingAdmin(null); setDisableTarget(null); toast.success("Institution admin updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const handleDisableToggle = async (admin: InstitutionStaffUser) => {
    await updateMut.mutateAsync([admin.id, {
      firstName: admin.firstName,
      lastName: admin.lastName,
      role: admin.role,
      yearGroups: admin.yearGroups,
      communityIds: admin.communityIds,
      isDisabled: !admin.isDisabled,
    }] as [string, UpdateInstitutionStaffRequest]);
  };

  const admins = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;

  if (!user || user.role !== "SuperAdmin") {
    return (
      <div className="p-4 sm:p-8 lg:p-12">
        <h1 className="text-2xl font-bold">Unauthorized</h1>
        <p className="text-muted-foreground mt-2">Only SuperAdmin users can access admin user management.</p>
      </div>
    );
  }

  const activeCount = admins.filter((a) => !a.isDisabled).length;
  const disabledCount = admins.filter((a) => a.isDisabled).length;

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      <header className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Institution Admins</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">Maintain staff access for this institution.</p>
        </div>
        <Button onClick={() => setShowCreate(true)}>
          <Plus size={16} />Add institution admin
        </Button>
      </header>

      <div className="p-3 rounded-none text-[13px]" style={{ background: "var(--brand-primary-light)", color: "var(--brand-primary-700, var(--color-text-info))" }}>
        <b>SuperAdmin-only area.</b> Administrators can view staff access but cannot create, edit, or disable accounts.
      </div>

      <FormSheet
        open={showCreate || editingAdmin !== null}
        onClose={() => { setShowCreate(false); setEditingAdmin(null); }}
        title={editingAdmin ? "Edit admin" : "Add institution admin"}
        description={editingAdmin ? `${editingAdmin.firstName} ${editingAdmin.lastName}` : "We email them a link to choose their own password."}
        formId="admin-form"
        submitLabel={editingAdmin ? "Save changes" : "Add and send invite"}
        savingLabel={editingAdmin ? "Saving" : "Creating"}
        saving={createMut.isPending || updateMut.isPending}
      >
        {editingAdmin ? (
          <EditAdminForm key={editingAdmin.id} id="admin-form" admin={editingAdmin} onSave={(d) => updateMut.mutate([editingAdmin.id, d])} />
        ) : showCreate ? (
          <NewAdminForm id="admin-form" onSave={(d) => createMut.mutate(d)} />
        ) : null}
      </FormSheet>

      <Card>
        <CardContent className="p-3 sm:p-4 flex flex-col sm:flex-row gap-2.5">
          <div className="flex-1 min-w-0">
            <SearchModal
              title="Search admins"
              value={search}
              onChange={(value) => { setSearch(value); setPage(1); }}
              placeholder="Search by name or email..."
            >
              {isLoading ? (
                <p className="text-sm text-muted-foreground">Searching…</p>
              ) : admins.length === 0 ? (
                <p className="text-sm text-muted-foreground">No admins match your search.</p>
              ) : (
                <div className="space-y-2 max-h-72 overflow-y-auto">
                  {admins.slice(0, 5).map((a) => (
                    <div key={a.id} className="flex items-center justify-between gap-3 rounded-lg border border-border/40 bg-muted/30 px-3 py-2">
                      <div className="min-w-0">
                        <p className="text-sm font-semibold truncate">{a.firstName} {a.lastName}</p>
                        <p className="text-xs text-muted-foreground truncate">{a.email}</p>
                      </div>
                      <Badge variant="secondary" className="text-[12px] uppercase font-bold">{a.role}</Badge>
                    </div>
                  ))}
                </div>
              )}
            </SearchModal>
          </div>
        </CardContent>
      </Card>

      <Card className="overflow-hidden">
        <div className="px-4 py-3.5 border-b border-border flex items-center justify-between">
          <b className="text-[13.5px]">Staff access</b>
          <span className="text-[12.5px] text-muted-foreground">{activeCount} active &middot; {disabledCount} disabled</span>
        </div>
        <CardContent className="p-0">
          <Table stackOnMobile className="min-w-[720px]">
            <TableHeader>
              <TableRow>
                <TableHead>Staff account</TableHead>
                <TableHead>Role</TableHead>
                <TableHead>Scope</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Created</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {isError ? (
                <TableRow><TableCell colSpan={6}><LoadError className="py-8" onRetry={() => refetch()} /></TableCell></TableRow>
              ) : isLoading ? (
                <TableSkeleton rows={4} cols={6} />
              ) : admins.length === 0 ? (
                <TableRow><TableCell colSpan={6}><EmptyState className="py-8" title="Admins help you run the community" description="Nothing matches. Invite a colleague and choose what they can manage, so the work is shared." /></TableCell></TableRow>
              ) : admins.map((a) => (
                <TableRow key={a.id}>
                  <TableCell>
                    <p className="font-medium">{a.firstName} {a.lastName}</p>
                    <p className="text-[12px] text-muted-foreground">{a.email}</p>
                  </TableCell>
                  <TableCell>{a.role}</TableCell>
                  <TableCell>
                    {a.role !== "ScopedAdmin" ? (
                      "All institution records"
                    ) : (a.yearGroups?.length || a.communityIds?.length) ? (
                      [
                        a.yearGroups?.length ? `Batch: ${a.yearGroups[0]}` : null,
                        a.communityIds?.length ? `${a.communityIds.length} ${a.communityIds.length === 1 ? "community" : "communities"}` : null,
                      ].filter(Boolean).join(" · ")
                    ) : (
                      <span className="text-destructive">No scope configured</span>
                    )}
                  </TableCell>
                  <TableCell>
                    <Badge variant={a.isDisabled ? "neutral" : a.invitePending ? "warning" : "success"}>
                      {a.isDisabled ? "Disabled" : a.invitePending ? "Invited" : "Active"}
                    </Badge>
                  </TableCell>
                  <TableCell>{formatDate(a.createdAt)}</TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2 justify-end">
                      {a.invitePending && !a.isDisabled && (
                        <Button size="sm" variant="outline" onClick={() => resendMut.mutate(a.id)} isLoading={resendMut.isPending && resendMut.variables === a.id}>
                          Resend invite
                        </Button>
                      )}
                      <Button size="sm" variant="outline" onClick={() => setEditingAdmin(a)}>
                        Edit
                      </Button>
                      <Button
                        size="sm"
                        variant={a.isDisabled ? "secondary" : "destructive"}
                        onClick={() => a.isDisabled ? handleDisableToggle(a) : setDisableTarget(a)}
                        isLoading={updateMut.isPending}
                      >
                        {a.isDisabled ? "Enable" : "Disable"}
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />

      <ConfirmModal
        open={!!disableTarget}
        title="Disable this admin?"
        message={`${disableTarget ? `${disableTarget.firstName} ${disableTarget.lastName}` : "This admin"} will lose access to the Institution Portal immediately.`}
        confirmLabel="Disable"
        variant="destructive"
        isLoading={updateMut.isPending}
        onConfirm={() => { if (disableTarget) handleDisableToggle(disableTarget); }}
        onCancel={() => setDisableTarget(null)}
      />
    </div>
  );
}
