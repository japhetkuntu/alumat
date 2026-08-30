"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import { Table, TableBody, TableCell, TableEmpty, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@alumni/ui";
import { FormError } from "@alumni/ui";
import { createPlatformStaff, getPlatformStaff, updatePlatformStaff, type PlatformStaffItem } from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";

export default function PlatformStaffPage() {
  const { isSuperAdmin } = useAuth();
  const queryClient = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: ["platform-staff"],
    queryFn: () => getPlatformStaff({ pageSize: 100 }),
  });
  const staff = data?.results ?? [];

  const [inviteOpen, setInviteOpen] = useState(false);
  const [inviteForm, setInviteForm] = useState({ name: "", email: "", password: "", role: "Support", team: "" });
  const [inviteError, setInviteError] = useState<string | null>(null);

  const [manageTarget, setManageTarget] = useState<PlatformStaffItem | null>(null);

  const createMutation = useMutation({
    mutationFn: () => createPlatformStaff(inviteForm),
    onSuccess: () => {
      toast.success("Staff member invited");
      queryClient.invalidateQueries({ queryKey: ["platform-staff"] });
      setInviteOpen(false);
      setInviteForm({ name: "", email: "", password: "", role: "Support", team: "" });
      setInviteError(null);
    },
    onError: (e) => {
      const msg = handleApiError(e);
      setInviteError(msg);
      toast.error(msg);
    },
  });

  const updateMutation = useMutation({
    mutationFn: (vars: { id: string; isDisabled: boolean }) =>
      updatePlatformStaff(vars.id, {
        name: manageTarget!.name,
        role: manageTarget!.role,
        team: manageTarget!.team ?? undefined,
        isDisabled: vars.isDisabled,
      }),
    onSuccess: () => {
      toast.success("Staff member updated");
      queryClient.invalidateQueries({ queryKey: ["platform-staff"] });
      setManageTarget(null);
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const activeCount = staff.filter((s) => !s.isDisabled).length;
  const disabledCount = staff.filter((s) => s.isDisabled).length;

  if (!isSuperAdmin) {
    return (
      <div className="p-8 lg:p-12">
        <h1 className="text-2xl font-bold">Unauthorized</h1>
        <p className="text-muted-foreground mt-2">Only SuperAdmin users can access platform staff management.</p>
      </div>
    );
  }

  return (
    <div className="p-7 max-w-[1500px]">
      <div className="flex items-end justify-between mb-6">
        <div>
          <h1 className="text-[24px] font-bold">Platform staff</h1>
          <p className="text-muted-foreground text-[13px] mt-1">Manage internal access, roles, and accountability.</p>
        </div>
        <Button onClick={() => setInviteOpen(true)}>Invite staff member</Button>
      </div>

      <Card>
        <div className="px-5 py-4 border-b border-border">
          <p className="text-[14px] font-semibold">Staff access</p>
          <p className="text-[12px] text-muted-foreground">
            {activeCount} active &middot; {disabledCount} disabled
          </p>
        </div>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Staff member</TableHead>
              <TableHead>Team</TableHead>
              <TableHead>Role</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>MFA</TableHead>
              <TableHead>Last active</TableHead>
              <TableHead />
            </TableRow>
          </TableHeader>
          <TableBody>
            {!isLoading && staff.length === 0 && <TableEmpty title="No platform staff yet" colSpan={7} />}
            {staff.map((s) => (
              <TableRow key={s.id}>
                <TableCell>
                  <div className="flex items-center gap-3">
                    <UserAvatar name={s.name} size="sm" />
                    <div>
                      <p className="font-semibold">{s.name}</p>
                      <p className="text-[12px] text-muted-foreground">{s.email}</p>
                    </div>
                  </div>
                </TableCell>
                <TableCell>{s.team ?? "—"}</TableCell>
                <TableCell>{s.role}</TableCell>
                <TableCell><Badge variant={s.isDisabled ? "neutral" : "success"}>{s.isDisabled ? "Disabled" : "Active"}</Badge></TableCell>
                <TableCell>{s.mfa ? "Enforced" : "Not enrolled"}</TableCell>
                <TableCell>{s.lastActiveAt ? new Date(s.lastActiveAt).toLocaleString() : "Never"}</TableCell>
                <TableCell>
                  <Button size="sm" variant="outline" onClick={() => setManageTarget(s)}>Manage</Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>

      <Dialog open={inviteOpen} onOpenChange={(open) => { setInviteOpen(open); if (!open) setInviteError(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Invite staff member</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 mt-2">
            <div className="space-y-1.5">
              <Label>Name</Label>
              <Input value={inviteForm.name} onChange={(e) => setInviteForm((f) => ({ ...f, name: e.target.value }))} />
            </div>
            <div className="space-y-1.5">
              <Label>Email</Label>
              <Input type="email" value={inviteForm.email} onChange={(e) => setInviteForm((f) => ({ ...f, email: e.target.value }))} />
            </div>
            <div className="space-y-1.5">
              <Label>Temporary password</Label>
              <Input type="password" value={inviteForm.password} onChange={(e) => setInviteForm((f) => ({ ...f, password: e.target.value }))} />
            </div>
            <div className="space-y-1.5">
              <Label>Role</Label>
              <select
                className="w-full h-9 rounded-md border border-border bg-background px-3 text-[13px]"
                value={inviteForm.role}
                onChange={(e) => setInviteForm((f) => ({ ...f, role: e.target.value }))}
              >
                <option value="SuperAdmin">SuperAdmin</option>
                <option value="Support">Support</option>
                <option value="Billing">Billing</option>
                <option value="Sales">Sales</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <Label>Team</Label>
              <Input value={inviteForm.team} onChange={(e) => setInviteForm((f) => ({ ...f, team: e.target.value }))} />
            </div>
            <FormError message={inviteError} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setInviteOpen(false)}>Cancel</Button>
            <Button
              onClick={() => createMutation.mutate()}
              disabled={createMutation.isPending || !inviteForm.name || !inviteForm.email || inviteForm.password.length < 8}
            >
              {createMutation.isPending ? "Inviting…" : "Invite"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!manageTarget} onOpenChange={(open) => !open && setManageTarget(null)}>
        <DialogContent>
          {manageTarget && (
            <>
              <DialogHeader>
                <DialogTitle>{manageTarget.name}</DialogTitle>
              </DialogHeader>
              <p className="text-[13px] text-muted-foreground mt-2">{manageTarget.email} &middot; {manageTarget.role}</p>
              <DialogFooter>
                <Button
                  variant={manageTarget.isDisabled ? "default" : "destructive"}
                  onClick={() => updateMutation.mutate({ id: manageTarget.id, isDisabled: !manageTarget.isDisabled })}
                  disabled={updateMutation.isPending}
                >
                  {manageTarget.isDisabled ? "Re-enable access" : "Disable access"}
                </Button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
