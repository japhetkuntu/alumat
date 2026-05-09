"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { SearchModal } from "@/components/ui/search-modal";
import { YearGroupPicker } from "@/components/ui/year-group-picker";
import { FormSelect } from "@/components/ui/select";
import { getAdmins, createAdmin, updateAdmin } from "@/lib/admin-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";
import { formatDate } from "@/lib/utils";
import type { AdminUser, CreateAdminRequest, UpdateAdminRequest } from "@/types";
import { TableSkeleton } from "@/components/ui/skeleton";

const roles = ["SuperAdmin", "Admin"] as const;

function NewAdminForm({ onSave, onCancel, saving }: { onSave: (data: CreateAdminRequest) => void; onCancel: () => void; saving: boolean }) {
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState<typeof roles[number]>("Admin");
  const [yearGroup, setYearGroup] = useState<number | undefined>(undefined);

  return (
    <Card>
      <CardHeader><CardTitle className="text-base">Create admin</CardTitle></CardHeader>
      <CardContent>
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); onSave({ firstName, lastName, email, password, role, graduationYear: yearGroup }); }}>
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
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Role</Label>
              <FormSelect value={role} onValueChange={(v) => setRole(v as typeof roles[number])} options={roles.map((r) => ({ value: r, label: r }))} />
            </div>
            <div className="space-y-2">
              <Label>Year group (optional)</Label>
              <YearGroupPicker value={yearGroup ? [yearGroup] : []} onChange={(years) => setYearGroup(years[0])} />
            </div>
          </div>
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
  admin: AdminUser;
  onSave: (data: UpdateAdminRequest) => void;
  onCancel: () => void;
  saving: boolean;
}) {
  const [firstName, setFirstName] = useState(admin.firstName);
  const [lastName, setLastName] = useState(admin.lastName);
  const [role, setRole] = useState<typeof roles[number]>(admin.role as typeof roles[number]);
  const [yearGroup, setYearGroup] = useState<number | undefined>(admin.graduationYear);
  const [isDisabled, setIsDisabled] = useState(!!admin.isDisabled);

  return (
    <Card>
      <CardHeader><CardTitle className="text-base">Edit admin</CardTitle></CardHeader>
      <CardContent>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            onSave({ firstName, lastName, role, graduationYear: yearGroup, isDisabled });
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
            <div className="space-y-2">
              <Label>Role</Label>
              <FormSelect value={role} onValueChange={(v) => setRole(v as typeof roles[number])} options={roles.map((r) => ({ value: r, label: r }))} />
            </div>
            <div className="space-y-2">
              <Label>Year group (optional)</Label>
              <YearGroupPicker value={yearGroup ? [yearGroup] : []} onChange={(years) => setYearGroup(years[0])} />
            </div>
          </div>

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
  const [editingAdmin, setEditingAdmin] = useState<AdminUser | null>(null);
  const pageSize = 20;
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ["admin-admins", search, page],
    queryFn: () => getAdmins(page, pageSize, search || undefined),
    placeholderData: (prev) => prev,
  });

  const createMut = useMutation({
    mutationFn: (req: CreateAdminRequest) => createAdmin(req),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-admins"] }); setShowCreate(false); toast.success("Admin created"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: ([id, req]: [string, UpdateAdminRequest]) => updateAdmin(id, req),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-admins"] }); setEditingAdmin(null); toast.success("Admin updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const handleDisableToggle = async (admin: AdminUser) => {
    await updateMut.mutateAsync([admin.id, {
      firstName: admin.firstName,
      lastName: admin.lastName,
      role: admin.role,
      graduationYear: admin.graduationYear,
      isDisabled: !admin.isDisabled,
    }] as [string, UpdateAdminRequest]);
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

  return (
    <div className="p-8 lg:p-12 space-y-8 max-w-7xl mx-auto">
      <header className="flex items-end justify-between gap-4">
        <div className="space-y-1">
          <p className="text-[11px] font-black uppercase tracking-[0.2em] text-primary">Administration</p>
          <h1 className="text-4xl font-black tracking-tight text-foreground">Admin accounts</h1>
          <p className="text-muted-foreground font-medium">Create and manage admin users by year group.</p>
        </div>
        <Button onClick={() => setShowCreate(true)} className="shadow-lg shadow-primary/20 font-bold h-11 px-5">
          <Plus size={16} />New Admin
        </Button>
      </header>

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

      <Card>
        <CardContent className="p-0">
          <Table className="min-w-[720px]">
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Role</TableHead>
                <TableHead>Year group</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Created</TableHead>
                <TableHead>Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableSkeleton rows={6} cols={7} />
              ) : admins.length === 0 ? (
                <TableRow><TableCell colSpan={7} className="text-center py-8 text-muted-foreground">No admins found</TableCell></TableRow>
              ) : admins.map((a) => (
                <TableRow key={a.id}>
                  <TableCell>{a.firstName} {a.lastName}</TableCell>
                  <TableCell>{a.email}</TableCell>
                  <TableCell>{a.role}</TableCell>
                  <TableCell>{a.graduationYear ?? "All"}</TableCell>
                  <TableCell>
                    <Badge variant={a.isDisabled ? "secondary" : "secondary"} className="text-[10px] uppercase font-bold">
                      {a.isDisabled ? "Disabled" : "Active"}
                    </Badge>
                  </TableCell>
                  <TableCell>{formatDate(a.createdAt)}</TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2">
                      <Button size="sm" variant="outline" onClick={() => setEditingAdmin(a)}>
                        Edit
                      </Button>
                      <Button
                        size="sm"
                        variant={a.isDisabled ? "secondary" : "destructive"}
                        onClick={() => handleDisableToggle(a)}
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
    </div>
  );
}
