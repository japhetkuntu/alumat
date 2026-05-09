"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { CheckCircle, XCircle, Download, ShieldBan, ShieldCheck, MailCheck, MailX, Eye, Upload, Loader2 } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { FormSelect } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { UserAvatar } from "@/components/ui/user-avatar";
import { Card, CardContent } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SearchModal } from "@/components/ui/search-modal";
import { formatDate } from "@/lib/utils";
import { getMembers, approveMember, rejectMember, banMember, unbanMember, importMembers, type ImportMemberItem } from "@/lib/admin-api";
import { handleApiError } from "@/lib/api-client";
import { TableSkeleton } from "@/components/ui/skeleton";
import type { MemberStatus } from "@/types";
import Link from "next/link";

const statusVariant: Record<MemberStatus, "success" | "warning" | "destructive" | "secondary"> = {
  Active: "success",
  Pending: "warning",
  Suspended: "destructive",
  Banned: "destructive",
  Blocked: "secondary",
};

type ModalState =
  | { type: "approve"; memberId: string; name: string }
  | { type: "reject"; memberId: string; name: string }
  | { type: "ban"; memberId: string; name: string }
  | { type: "unban"; memberId: string; name: string }
  | null;

export default function AdminMembersPage() {
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [compactRows, setCompactRows] = useState(false);
  const [page, setPage] = useState(1);
  const [modal, setModal] = useState<ModalState>(null);
  const [reasonText, setReasonText] = useState("");
  const [showImport, setShowImport] = useState(false);
  const [importData, setImportData] = useState("");
  const pageSize = 20;
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ["admin-members", search, statusFilter, page],
    queryFn: () => getMembers({ search: search || undefined, status: statusFilter || undefined, page, pageSize }),
    placeholderData: (prev) => prev,
  });

  const invalidate = () => qc.invalidateQueries({ queryKey: ["admin-members"] });

  const approveMut = useMutation({
    mutationFn: (id: string) => approveMember(id),
    onSuccess: () => { invalidate(); setModal(null); toast.success("Member approved"); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const rejectMut = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason?: string }) => rejectMember(id, reason),
    onSuccess: () => { invalidate(); setModal(null); setReasonText(""); toast.success("Member rejected"); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const banMut = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason?: string }) => banMember(id, reason),
    onSuccess: () => { invalidate(); setModal(null); setReasonText(""); toast.success("Member banned"); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const unbanMut = useMutation({
    mutationFn: (id: string) => unbanMember(id),
    onSuccess: () => { invalidate(); setModal(null); toast.success("Member unbanned"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const importMut = useMutation({
    mutationFn: (members: ImportMemberItem[]) => importMembers(members),
    onSuccess: (result) => {
      invalidate();
      setShowImport(false);
      setImportData("");
      toast.success(`${result.imported} member(s) imported, ${result.skipped} skipped`);
      if (result.errors.length > 0) {
        toast.error(`Errors: ${result.errors.slice(0, 3).join("; ")}`);
      }
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const members = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;
  const densityCellClass = compactRows ? "py-2" : "py-2.5";
  const densityRowClass = compactRows ? "h-[46px]" : "h-[52px]";
  const actionFocusClass = "focus-visible:ring-2 focus-visible:ring-ring/50 focus-visible:ring-offset-1";
  const [exportLoading, setExportLoading] = useState(false);

  const closeModal = () => { setModal(null); setReasonText(""); };

  const handleConfirm = () => {
    if (!modal) return;
    if (modal.type === "approve") approveMut.mutate(modal.memberId);
    else if (modal.type === "reject") rejectMut.mutate({ id: modal.memberId, reason: reasonText || undefined });
    else if (modal.type === "ban") banMut.mutate({ id: modal.memberId, reason: reasonText || undefined });
    else if (modal.type === "unban") unbanMut.mutate(modal.memberId);
  };

  const handleExportCsv = async () => {
    setExportLoading(true);
    try {
      const result = await getMembers({ search: search || undefined, status: statusFilter || undefined, page: 1, pageSize: 5000 });
      const rows = result.results ?? [];
      if (!rows.length) { toast.error("No members to export"); return; }
      const headers = ["id", "firstName", "lastName", "email", "phone", "graduationYear", "department", "status", "memberNumber"];
      const csv = [headers.join(","), ...rows.map((m) =>
        headers.map((h) => `"${String(m[h as keyof typeof m] ?? "").replace(/"/g, '""')}"`).join(",")
      )].join("\n");
      const a = document.createElement("a");
      a.href = URL.createObjectURL(new Blob([csv], { type: "text/csv" }));
      a.download = `members-${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      toast.success(`Exported ${rows.length} members`);
    } catch (e) {
      toast.error(handleApiError(e));
    } finally {
      setExportLoading(false);
    }
  };

  return (
    <div className="p-6 lg:p-8 space-y-6 page-enter">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-[22px] font-bold tracking-tight">Members</h1>
          <p className="text-muted-foreground text-[13px] mt-1">Manage alumni accounts and approvals</p>
        </div>
        <div className="flex flex-wrap justify-end gap-2">
          <Button size="sm" variant="outline" className="h-9 px-3.5" onClick={() => setShowImport(true)}>
            <Upload size={14} />Import Members
          </Button>
          <Button size="sm" variant="outline" className="h-9 px-3.5" onClick={() => setCompactRows((v) => !v)}>
            {compactRows ? "Comfortable Rows" : "Compact Rows"}
          </Button>
          <Button size="sm" variant="outline" className="h-9 px-3.5" disabled={exportLoading} onClick={handleExportCsv}>
            {exportLoading ? <><Loader2 size={14} className="animate-spin" />Exporting…</> : <><Download size={14} />Export CSV</>}
          </Button>
        </div>
      </div>

      <Card>
        <CardContent className="p-3 sm:p-4 flex flex-col sm:flex-row gap-2.5">
          <div className="flex-1 min-w-0">
            <SearchModal
              title="Search members"
              value={search}
              onChange={(value) => { setSearch(value); setPage(1); }}
              placeholder="Search by name or email..."
            >
              {isLoading ? (
                <p className="text-sm text-muted-foreground">Searching...</p>
              ) : members.length === 0 ? (
                <p className="text-sm text-muted-foreground">No members match your search.</p>
              ) : (
                <div className="space-y-2 max-h-72 overflow-y-auto">
                  {members.slice(0, 5).map((m) => (
                    <div key={m.id} className="flex items-center justify-between gap-2 rounded-lg border border-border/40 bg-muted/30 px-3 py-2">
                      <div className="min-w-0">
                        <p className="text-sm font-semibold truncate">{m.firstName} {m.lastName}</p>
                        <p className="text-xs text-muted-foreground truncate">{m.email}</p>
                      </div>
                      <Badge variant={statusVariant[m.status]} className="text-[10px] font-bold uppercase tracking-widest">
                        {m.status}
                      </Badge>
                    </div>
                  ))}
                  {members.length > 5 && (
                    <p className="text-xs text-muted-foreground">Showing {Math.min(5, members.length)} of {members.length} results. Close to view the full list.</p>
                  )}
                </div>
              )}
            </SearchModal>
          </div>
          <FormSelect className="w-40 min-w-[10rem]" value={statusFilter || "__all__"} onValueChange={(v) => { setStatusFilter(v === "__all__" ? "" : v); setPage(1); }} placeholder="All statuses" options={[
            { value: "__all__", label: "All statuses" },            { value: "Active", label: "Active" },
            { value: "Pending", label: "Pending" },
            { value: "Suspended", label: "Suspended" },
            { value: "Banned", label: "Banned" },
            { value: "Blocked", label: "Blocked" },
          ]} />
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          <Table className="min-w-[760px] sm:min-w-[960px]">
            <TableHeader>
              <TableRow>
                <TableHead>Member</TableHead>
                <TableHead>Member No.</TableHead>
                <TableHead>Year</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Verified</TableHead>
                <TableHead>Joined</TableHead>
                <TableHead className="text-right sticky right-0 z-20 bg-muted/35 shadow-[-1px_0_0_0_var(--border)]">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableSkeleton rows={8} cols={7} />
              ) : members.length === 0 ? (
                <TableRow><TableCell colSpan={7} className="text-center py-8 text-muted-foreground">No members found</TableCell></TableRow>
              ) : members.map((m) => (
                <TableRow key={m.id} className={densityRowClass}>
                  <TableCell className={densityCellClass}>
                    <div className="flex items-center gap-3">
                      <UserAvatar
                        src={m.profilePictureUrl}
                        name={`${m.firstName} ${m.lastName}`}
                        size="sm"
                      />
                      <div className="min-w-0">
                        <p className="font-medium text-sm leading-tight truncate max-w-[220px]">{m.firstName} {m.lastName}</p>
                        <p className="text-xs text-muted-foreground truncate max-w-[220px]">{m.email}</p>
                        {m.rejectionCount != null && m.rejectionCount > 0 && (
                          <p className="text-[10px] text-orange-500 leading-tight">Rejected {m.rejectionCount}×</p>
                        )}
                      </div>
                    </div>
                  </TableCell>
                  <TableCell className={`text-sm font-mono text-muted-foreground whitespace-nowrap ${densityCellClass}`}>{m.memberNumber ?? "—"}</TableCell>
                  <TableCell className={`text-sm whitespace-nowrap ${densityCellClass}`}>{m.graduationYear}</TableCell>
                  <TableCell className={`whitespace-nowrap ${densityCellClass}`}><Badge variant={statusVariant[m.status]}>{m.status}</Badge></TableCell>
                  <TableCell className={densityCellClass}>
                    {m.isEmailVerified
                      ? <MailCheck size={14} className="text-green-600" />
                      : <MailX size={14} className="text-muted-foreground" />}
                  </TableCell>
                  <TableCell className={`text-sm text-muted-foreground whitespace-nowrap ${densityCellClass}`}>{formatDate(m.createdAt)}</TableCell>
                  <TableCell className={`text-right sticky right-0 z-10 bg-card/95 backdrop-blur-sm shadow-[-1px_0_0_0_var(--border)] ${densityCellClass}`}>
                    <div className="flex items-center justify-end gap-1.5">
                      <Link href={`/members/${m.id}`}>
                        <Button size="icon" variant="ghost" className={`h-8 w-8 rounded-md ${actionFocusClass}`} title="View details">
                          <Eye size={15} />
                        </Button>
                      </Link>
                      {m.status === "Pending" && (
                        <>
                          <Button size="icon" variant="ghost" className={`h-8 w-8 rounded-md text-green-600 hover:text-green-700 hover:bg-green-50 dark:hover:bg-green-500/10 ${actionFocusClass}`} title="Approve"
                            onClick={() => setModal({ type: "approve", memberId: m.id, name: `${m.firstName} ${m.lastName}` })}>
                            <CheckCircle size={15} />
                          </Button>
                          <Button size="icon" variant="ghost" className={`h-8 w-8 rounded-md text-destructive hover:text-destructive/90 hover:bg-destructive/10 ${actionFocusClass}`} title="Reject"
                            onClick={() => { setReasonText(""); setModal({ type: "reject", memberId: m.id, name: `${m.firstName} ${m.lastName}` }); }}>
                            <XCircle size={15} />
                          </Button>
                        </>
                      )}
                      {(m.status === "Active" || m.status === "Suspended") && (
                        <Button size="icon" variant="ghost" className={`h-8 w-8 rounded-md text-destructive hover:text-destructive/90 hover:bg-destructive/10 ${actionFocusClass}`} title="Ban member"
                          onClick={() => { setReasonText(""); setModal({ type: "ban", memberId: m.id, name: `${m.firstName} ${m.lastName}` }); }}>
                          <ShieldBan size={15} />
                        </Button>
                      )}
                      {m.status === "Banned" && (
                        <Button size="icon" variant="ghost" className={`h-8 w-8 rounded-md text-green-600 hover:text-green-700 hover:bg-green-50 dark:hover:bg-green-500/10 ${actionFocusClass}`} title="Unban member"
                          onClick={() => setModal({ type: "unban", memberId: m.id, name: `${m.firstName} ${m.lastName}` })}>
                          <ShieldCheck size={15} />
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />

      {/* Confirm modals */}
      <ConfirmModal
        open={modal?.type === "approve"}
        title="Approve Member"
        message={`Approve ${modal?.type === "approve" ? modal.name : ""}? They will be granted active access and assigned a member number.`}
        confirmLabel="Approve"
        variant="default"
        isLoading={approveMut.isPending}
        onConfirm={handleConfirm}
        onCancel={closeModal}
      />
      <ConfirmModal
        open={modal?.type === "reject"}
        title="Reject Registration"
        message={`Reject ${modal?.type === "reject" ? modal.name : ""}? After 3 rejections their account will be permanently blocked.`}
        confirmLabel="Reject"
        variant="destructive"
        isLoading={rejectMut.isPending}
        onConfirm={handleConfirm}
        onCancel={closeModal}
      >
        <Textarea placeholder="Reason for rejection (optional)" rows={2} value={reasonText} onChange={(e) => setReasonText(e.target.value)} />
      </ConfirmModal>
      <ConfirmModal
        open={modal?.type === "ban"}
        title="Ban Member"
        message={`Ban ${modal?.type === "ban" ? modal.name : ""}? They will be prevented from logging in.`}
        confirmLabel="Ban Member"
        variant="destructive"
        isLoading={banMut.isPending}
        onConfirm={handleConfirm}
        onCancel={closeModal}
      >
        <Textarea placeholder="Reason for ban (optional)" rows={2} value={reasonText} onChange={(e) => setReasonText(e.target.value)} />
      </ConfirmModal>
      <ConfirmModal
        open={modal?.type === "unban"}
        title="Unban Member"
        message={`Restore access for ${modal?.type === "unban" ? modal.name : ""}? Their status will be set to Active.`}
        confirmLabel="Unban"
        variant="default"
        isLoading={unbanMut.isPending}
        onConfirm={handleConfirm}
        onCancel={closeModal}
      />

      {/* Import Members Dialog */}
      <Dialog open={showImport} onOpenChange={setShowImport}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Import Members</DialogTitle>
            <DialogDescription>
              Paste a JSON array of members to import. Each member needs: firstName, lastName, email, graduationYear. Optional: phone, studentId, departmentId, paidMembershipYears (array of years).
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1.5">
              <Label>JSON data</Label>
              <Textarea
                rows={8}
                placeholder={`[
  {
    "firstName": "Kwame",
    "lastName": "Mensah",
    "email": "kwame@example.com",
    "graduationYear": 2020,
    "studentId": "UMaT/ENG/20/0001",
    "paidMembershipYears": [2023, 2024]
  }
]`}
                value={importData}
                onChange={(e) => setImportData(e.target.value)}
                className="font-mono text-xs"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setShowImport(false); setImportData(""); }}>Cancel</Button>
            <Button
              disabled={!importData.trim() || importMut.isPending}
              onClick={() => {
                try {
                  const parsed = JSON.parse(importData);
                  if (!Array.isArray(parsed)) { toast.error("Must be a JSON array"); return; }
                  importMut.mutate(parsed);
                } catch {
                  toast.error("Invalid JSON format");
                }
              }}
            >
              {importMut.isPending ? <><Loader2 size={14} className="animate-spin" />Importing...</> : `Import Members`}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
