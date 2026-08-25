"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { Pagination } from "@alumni/ui";
import { toast } from "sonner";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { SearchModal } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { YearGroupPicker } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { getInstitutionStaff, createInstitutionStaff, updateInstitutionStaff, getCommunities, type CommunityListItem } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";
import { formatDate } from "@alumni/ui";
import type { InstitutionStaffUser, CreateInstitutionStaffRequest, UpdateInstitutionStaffRequest } from "@/types";
import { TableSkeleton } from "@alumni/ui";

const roles = ["SuperAdmin", "Admin", "ScopedAdmin"] as const;

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

function NewAdminForm({ onSave, onCancel, saving }: { onSave: (data: CreateInstitutionStaffRequest) => void; onCancel: () => void; saving: boolean }) {
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState<typeof roles[number]>("Admin");
  const [yearGroups, setYearGroups] = useState<number[]>([]);
  const [communityIds, setCommunityIds] = useState<string[]>([]);

  const { data: communities = [] } = useQuery({
    queryKey: ["admin-communities-for-staff-form"],
    queryFn: getCommunities,
    enabled: role === "ScopedAdmin",
  });

  return (
    <Card>
      <CardHeader><CardTitle className="text-base">Add institution admin</CardTitle></CardHeader>
      <CardContent>
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); onSave({ firstName, lastName, email, password, role, yearGroups: role === "ScopedAdmin" ? yearGroups : undefined, communityIds: role === "ScopedAdmin" ? communityIds : undefined }); }}>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>First name</Label>
              <Input value={firstName} onChange={(e) => setFirstName(e.target.value)} required />
            </div>
            <div className="space-y-2">
              <Label>Last name</Label>
              <Input value={lastName} onChange={(e) => setLastName(e.target.value)} required />
            </div>
            <div className="space-y-2">
              <Label>Email</Label>
              <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
            </div>
            <div className="space-y-2">
              <Label>Password</Label>
              <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
            </div>
          </div>
          <div className="space-y-2">
            <Label>Role</Label>
            <FormSelect value={role} onValueChange={(v) => setRole(v as typeof roles[number])} options={roles.map((r) => ({ value: r, label: r }))} />
            <p className="text-xs text-muted-foreground">
              {role === "SuperAdmin" && "Full access, including managing other admins."}
              {role === "Admin" && "Full access to institution data, except managing other admins."}
              {role === "ScopedAdmin" && "Restricted to the year-groups/batches and communities selected below."}
            </p>
          </div>
          {role === "ScopedAdmin" && (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Year groups</Label>
                <YearGroupPicker value={yearGroups} onChange={setYearGroups} />
              </div>
              <div className="space-y-2">
                <Label>Communities</Label>
                <CommunityCheckboxList communities={communities} selected={communityIds} onChange={setCommunityIds} />
              </div>
            </div>
          )}
          <div className="flex gap-3">
            <Button type="submit" size="sm" isLoading={saving} loadingText="Creating">Create admin</Button>
            <Button type="button" size="sm" variant="outline" onClick={onCancel}>Cancel</Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

function EditAdminForm({
  admin,
  onSave,
  onCancel,
  saving,
}: {
  admin: InstitutionStaffUser;
  onSave: (data: UpdateInstitutionStaffRequest) => void;
  onCancel: () => void;
  saving: boolean;
}) {
  const [firstName, setFirstName] = useState(admin.firstName);
  const [lastName, setLastName] = useState(admin.lastName);
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
    <Card>
      <CardHeader><CardTitle className="text-base">Edit admin</CardTitle></CardHeader>
      <CardContent>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            onSave({ firstName, lastName, role, yearGroups: role === "ScopedAdmin" ? yearGroups : undefined, communityIds: role === "ScopedAdmin" ? communityIds : undefined, isDisabled });
          }}
        >
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
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
            <Label>Role</Label>
            <FormSelect value={role} onValueChange={(v) => setRole(v as typeof roles[number])} options={roles.map((r) => ({ value: r, label: r }))} />
          </div>
          {role === "ScopedAdmin" && (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Year groups</Label>
                <YearGroupPicker value={yearGroups} onChange={setYearGroups} />
              </div>
              <div className="space-y-2">
                <Label>Communities</Label>
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

          <div className="flex gap-3">
            <Button type="submit" size="sm" isLoading={saving} loadingText="Saving">
              Save changes
            </Button>
            <Button type="button" size="sm" variant="outline" onClick={onCancel}>
              Cancel
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
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

  const { data, isLoading } = useQuery({
    queryKey: ["admin-admins", search, page],
    queryFn: () => getInstitutionStaff(page, pageSize, search || undefined),
    placeholderData: (prev) => prev,
  });

  const createMut = useMutation({
    mutationFn: (req: CreateInstitutionStaffRequest) => createInstitutionStaff(req),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-admins"] }); setShowCreate(false); toast.success("Institution admin added"); },
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
      <div className="p-8 lg:p-12">
        <h1 className="text-2xl font-bold">Unauthorized</h1>
        <p className="text-muted-foreground mt-2">Only SuperAdmin users can access admin user management.</p>
      </div>
    );
  }

  const activeCount = admins.filter((a) => !a.isDisabled).length;
  const disabledCount = admins.filter((a) => a.isDisabled).length;

  return (
    <div className="p-[26px] max-w-[1100px] mx-auto space-y-5">
      <header className="flex items-end justify-between gap-4">
        <div>
          <h1 className="text-[25px] font-bold m-0">Institution Admins</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">Maintain staff access for this institution.</p>
        </div>
        <Button onClick={() => setShowCreate(true)}>
          <Plus size={16} />Add institution admin
        </Button>
      </header>

      <div className="p-3 rounded-[6px] text-[13px]" style={{ background: "var(--brand-primary-light)", color: "var(--color-text-info)" }}>
        <b>SuperAdmin-only area.</b> Administrators can view staff access but cannot create, edit, or disable accounts.
      </div>

      {showCreate && (
        <NewAdminForm
          onSave={(d) => createMut.mutate(d)}
          onCancel={() => setShowCreate(false)}
          saving={createMut.isPending}
        />
      )}

      {editingAdmin && (
        <EditAdminForm
          admin={editingAdmin}
          onSave={(d) => updateMut.mutate([editingAdmin.id, d])}
          onCancel={() => setEditingAdmin(null)}
          saving={updateMut.isPending}
        />
      )}

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
                      <Badge variant="secondary" className="text-[10px] uppercase font-bold">{a.role}</Badge>
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
          <Table className="min-w-[720px]">
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
              {isLoading ? (
                <TableSkeleton rows={6} cols={6} />
              ) : admins.length === 0 ? (
                <TableRow><TableCell colSpan={6} className="text-center py-8 text-muted-foreground">No admins found</TableCell></TableRow>
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
                        a.yearGroups?.length ? `Years: ${a.yearGroups.join(", ")}` : null,
                        a.communityIds?.length ? `${a.communityIds.length} ${a.communityIds.length === 1 ? "community" : "communities"}` : null,
                      ].filter(Boolean).join(" · ")
                    ) : (
                      <span className="text-destructive">No scope configured</span>
                    )}
                  </TableCell>
                  <TableCell>
                    <Badge variant={a.isDisabled ? "neutral" : "success"}>
                      {a.isDisabled ? "Disabled" : "Active"}
                    </Badge>
                  </TableCell>
                  <TableCell>{formatDate(a.createdAt)}</TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2 justify-end">
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
