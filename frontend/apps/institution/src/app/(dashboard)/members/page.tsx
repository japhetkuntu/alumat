"use client";

import { useState } from "react";
import { useQuery, useQueries, useMutation, useQueryClient } from "@tanstack/react-query";
import { CheckCircle, XCircle, Download, ShieldBan, ShieldCheck, MailCheck, MailX, Eye, Upload, Loader2 } from "@alumni/ui";
import { Pagination } from "@alumni/ui";
import { toast } from "sonner";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { UserAvatar } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { PhoneInput } from "@alumni/ui";
import { formatDate, cn } from "@alumni/ui";
import { getMembers, approveMember, rejectMember, banMember, unbanMember, importMembers, type ImportMemberItem } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { TableSkeleton } from "@alumni/ui";
import type { MemberStatus } from "@/types";
import Link from "next/link";
import { useAuth } from "@/hooks/use-auth";

const CSV_HEADERS = ["firstName", "lastName", "email", "phone", "studentId", "graduationYear"] as const;
const CSV_TEMPLATE = [
  CSV_HEADERS.join(","),
  `Kwame,Mensah,kwame@example.com,+233241234567,ENG/20/0001,2020`,
].join("\n");

function downloadCsvTemplate() {
  const a = document.createElement("a");
  a.href = URL.createObjectURL(new Blob([CSV_TEMPLATE], { type: "text/csv" }));
  a.download = "member-upload-template.csv";
  a.click();
}

/** Minimal RFC4180 parser — handles quoted fields, embedded commas, and escaped quotes ("" inside a quoted field). */
function parseCsvText(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let field = "";
  let inQuotes = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (inQuotes) {
      if (c === '"') {
        if (text[i + 1] === '"') { field += '"'; i++; } else inQuotes = false;
      } else field += c;
    } else if (c === '"') inQuotes = true;
    else if (c === ",") { row.push(field); field = ""; }
    else if (c === "\n" || c === "\r") {
      if (c === "\r" && text[i + 1] === "\n") i++;
      row.push(field);
      field = "";
      if (row.some((f) => f.trim() !== "")) rows.push(row);
      row = [];
    } else field += c;
  }
  if (field !== "" || row.length > 0) { row.push(field); if (row.some((f) => f.trim() !== "")) rows.push(row); }
  return rows;
}

function normalizeHeader(h: string) {
  return h.trim().toLowerCase().replace(/[\s_-]+/g, "");
}

const HEADER_ALIASES: Record<string, keyof ImportMemberItem> = {
  firstname: "firstName",
  lastname: "lastName",
  email: "email",
  emailaddress: "email",
  phone: "phone",
  phonenumber: "phone",
  studentid: "studentId",
  graduationyear: "graduationYear",
  gradyear: "graduationYear",
  year: "graduationYear",
};

interface CsvParseResult {
  rows: ImportMemberItem[];
  errors: string[];
}

function parseMembersCsv(text: string): CsvParseResult {
  const table = parseCsvText(text);
  if (table.length === 0) return { rows: [], errors: ["The file is empty."] };

  const headerRow = table[0].map(normalizeHeader);
  const columnFor: Partial<Record<keyof ImportMemberItem, number>> = {};
  headerRow.forEach((h, i) => {
    const field = HEADER_ALIASES[h];
    if (field) columnFor[field] = i;
  });

  const missing = (["firstName", "lastName", "email", "graduationYear"] as const).filter((f) => columnFor[f] === undefined);
  if (missing.length > 0) {
    return { rows: [], errors: [`Missing required column(s): ${missing.join(", ")}. Download the template to see the expected format.`] };
  }

  const rows: ImportMemberItem[] = [];
  const errors: string[] = [];
  for (let r = 1; r < table.length; r++) {
    const cells = table[r];
    const get = (f: keyof ImportMemberItem) => (columnFor[f] !== undefined ? (cells[columnFor[f]!] ?? "").trim() : "");
    const firstName = get("firstName");
    const lastName = get("lastName");
    const email = get("email");
    const graduationYearRaw = get("graduationYear");
    const graduationYear = Number(graduationYearRaw);

    if (!firstName || !lastName || !email || !graduationYearRaw || !Number.isFinite(graduationYear)) {
      errors.push(`Row ${r + 1}: missing or invalid required field(s)`);
      continue;
    }
    rows.push({
      firstName, lastName, email, graduationYear,
      phone: get("phone") || undefined,
      studentId: get("studentId") || undefined,
    });
  }
  return { rows, errors };
}

const statusVariant: Record<MemberStatus, "success" | "warning" | "destructive" | "secondary"> = {
  Active: "success",
  Pending: "warning",
  Suspended: "destructive",
  Banned: "destructive",
  Blocked: "secondary",
};

const STATUS_FILTERS: { label: string; value: MemberStatus | "" }[] = [
  { label: "All", value: "" },
  { label: "Pending", value: "Pending" },
  { label: "Active", value: "Active" },
  { label: "Suspended", value: "Suspended" },
  { label: "Banned", value: "Banned" },
  { label: "Blocked", value: "Blocked" },
];

type ModalState =
  | { type: "approve"; memberId: string; name: string }
  | { type: "reject"; memberId: string; name: string }
  | { type: "ban"; memberId: string; name: string }
  | { type: "unban"; memberId: string; name: string }
  | null;

export default function AdminMembersPage() {
  const { user } = useAuth();
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<MemberStatus | "">("");
  const [page, setPage] = useState(1);
  const [modal, setModal] = useState<ModalState>(null);
  const [reasonText, setReasonText] = useState("");
  const [showImport, setShowImport] = useState(false);
  const [csvFileName, setCsvFileName] = useState<string | null>(null);
  const [csvParsed, setCsvParsed] = useState<CsvParseResult | null>(null);
  const [showAddMember, setShowAddMember] = useState(false);
  const [addMemberForm, setAddMemberForm] = useState({
    firstName: "", lastName: "", email: "", phone: "", studentId: "", graduationYear: "",
  });
  const pageSize = 25;
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ["admin-members", search, statusFilter, page],
    queryFn: () => getMembers({ search: search || undefined, status: statusFilter || undefined, page, pageSize }),
    placeholderData: (prev) => prev,
  });

  // Aggregate counts per status for the filter pills — matches the mockup's "All · 8,492" pattern.
  const countQueries = useQueries({
    queries: STATUS_FILTERS.map((f) => ({
      queryKey: ["admin-members-count", f.value],
      queryFn: () => getMembers({ status: f.value || undefined, pageSize: 1 }),
      staleTime: 60_000,
    })),
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
      setCsvFileName(null);
      setCsvParsed(null);
      toast.success(
        result.imported > 0
          ? `${result.imported} member(s) added — each was emailed a link to set their password.`
          : "No new members were added."
      );
      if (result.skipped > 0) toast(`${result.skipped} row(s) skipped (already registered or out of scope).`);
      if (result.errors.length > 0) {
        toast.error(`Errors: ${result.errors.slice(0, 3).join("; ")}`);
      }
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  function handleCsvFile(file: File) {
    setCsvFileName(file.name);
    setCsvParsed(null);
    const reader = new FileReader();
    reader.onload = () => setCsvParsed(parseMembersCsv(String(reader.result ?? "")));
    reader.onerror = () => setCsvParsed({ rows: [], errors: ["Could not read that file."] });
    reader.readAsText(file);
  }

  const addMemberMut = useMutation({
    mutationFn: (member: ImportMemberItem) => importMembers([member]),
    onSuccess: (result) => {
      if (result.imported > 0) {
        invalidate();
        setShowAddMember(false);
        setAddMemberForm({ firstName: "", lastName: "", email: "", phone: "", studentId: "", graduationYear: "" });
        toast.success("Member added — they've been emailed a link to set their password.");
      } else {
        toast.error(result.errors[0] ?? "Could not add member");
      }
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const members = data?.results ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data?.totalPages ?? 1;
  const densityCellClass = "py-2.5";
  const densityRowClass = "h-[52px]";
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
    <div className="p-4 sm:p-[26px] max-w-[1300px] mx-auto">
      <div className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Members</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">
            {totalCount.toLocaleString()} {totalCount === 1 ? "record" : "records"} &middot;{" "}
            {user?.role === "ScopedAdmin" && user.yearGroups?.length
              ? `scoped to your assigned year group${user.yearGroups.length > 1 ? "s" : ""} (${user.yearGroups.join(", ")})`
              : "scoped to this institution"}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => setShowImport(true)}>
            <Upload size={14} />Bulk upload
          </Button>
          <Button variant="outline" disabled={exportLoading} onClick={handleExportCsv}>
            {exportLoading ? <><Loader2 size={14} className="animate-spin" />Exporting…</> : <><Download size={14} />Export roster</>}
          </Button>
          <Button onClick={() => setShowAddMember(true)}>
            Add member
          </Button>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-2 mt-5 mb-3">
        <Input
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1); }}
          placeholder="Search name, email, member number"
          className="w-full sm:w-[260px] h-9"
        />
        {STATUS_FILTERS.map((f, i) => {
          const count = countQueries[i]?.data?.totalCount;
          const active = statusFilter === f.value;
          return (
            <button
              key={f.label}
              onClick={() => { setStatusFilter(f.value); setPage(1); }}
              className={cn(
                "px-3 py-2 border text-[12.5px] font-semibold transition-colors",
                active ? "bg-primary/10 text-primary border-blue-300" : "bg-white text-foreground border-border hover:bg-muted"
              )}
            >
              {f.label}{count != null ? ` · ${count.toLocaleString()}` : ""}
            </button>
          );
        })}
      </div>

      <Card className="overflow-hidden">
        <div className="px-4 py-3.5 border-b border-border flex items-center justify-between">
          <b className="text-[13.5px]">Member roster</b>
          <span className="text-[12.5px] text-muted-foreground">
            Showing {members.length ? (page - 1) * pageSize + 1 : 0}&ndash;{(page - 1) * pageSize + members.length} of {totalCount.toLocaleString()}
          </span>
        </div>
        <CardContent className="p-0">
          <div className="hidden md:block">
          <Table className="min-w-[760px] sm:min-w-[960px]">
            <TableHeader>
              <TableRow>
                <TableHead>Member</TableHead>
                <TableHead>Number</TableHead>
                <TableHead>Class year</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Joined</TableHead>
                <TableHead className="text-right sticky right-0 z-20 bg-muted/35 shadow-[-1px_0_0_0_var(--border)]">Action</TableHead>
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
                    <span className={cn("inline-flex items-center gap-1.5 text-[12px]", m.isEmailVerified ? "text-success" : "text-muted-foreground")}>
                      {m.isEmailVerified ? <MailCheck size={14} /> : <MailX size={14} />}
                      {m.isEmailVerified ? "Verified" : "Unverified"}
                    </span>
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
                          <Button size="icon" variant="ghost" className={`h-8 w-8 rounded-md text-success hover:text-success hover:bg-success/10 dark:hover:bg-success/100/10 ${actionFocusClass}`} title="Approve"
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
                        <Button size="icon" variant="ghost" className={`h-8 w-8 rounded-md text-success hover:text-success hover:bg-success/10 dark:hover:bg-success/100/10 ${actionFocusClass}`} title="Unban member"
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
          </div>

          {/* Mobile: stacked cards instead of a cramped table */}
          <div className="md:hidden divide-y divide-border">
            {isLoading ? (
              <div className="p-4 space-y-3">
                {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-16 rounded-md bg-muted animate-pulse" />)}
              </div>
            ) : members.length === 0 ? (
              <p className="text-center py-8 text-muted-foreground text-sm">No members found</p>
            ) : members.map((m) => (
              <div key={m.id} className="p-4 space-y-2.5">
                <div className="flex items-center gap-3">
                  <UserAvatar src={m.profilePictureUrl} name={`${m.firstName} ${m.lastName}`} size="sm" />
                  <div className="min-w-0 flex-1">
                    <p className="font-medium text-sm leading-tight truncate">{m.firstName} {m.lastName}</p>
                    <p className="text-xs text-muted-foreground truncate">{m.email}</p>
                  </div>
                  <Badge variant={statusVariant[m.status]}>{m.status}</Badge>
                </div>
                <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
                  <span>{m.memberNumber ?? "—"}</span>
                  <span>&middot;</span>
                  <span>{m.graduationYear ? `Class of ${m.graduationYear}` : "—"}</span>
                  <span>&middot;</span>
                  <span className={cn("inline-flex items-center gap-1", m.isEmailVerified ? "text-success" : "text-muted-foreground")}>
                    {m.isEmailVerified ? <MailCheck size={12} /> : <MailX size={12} />}
                    {m.isEmailVerified ? "Verified" : "Unverified"}
                  </span>
                  <span>&middot;</span>
                  <span>Joined {formatDate(m.createdAt)}</span>
                </div>
                <div className="flex items-center justify-end gap-1.5 pt-1">
                  <Link href={`/members/${m.id}`}>
                    <Button size="icon" variant="ghost" className={`h-8 w-8 rounded-md ${actionFocusClass}`} title="View details">
                      <Eye size={15} />
                    </Button>
                  </Link>
                  {m.status === "Pending" && (
                    <>
                      <Button size="icon" variant="ghost" className={`h-8 w-8 rounded-md text-success hover:text-success hover:bg-success/10 ${actionFocusClass}`} title="Approve"
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
                    <Button size="icon" variant="ghost" className={`h-8 w-8 rounded-md text-success hover:text-success hover:bg-success/10 ${actionFocusClass}`} title="Unban member"
                      onClick={() => setModal({ type: "unban", memberId: m.id, name: `${m.firstName} ${m.lastName}` })}>
                      <ShieldCheck size={15} />
                    </Button>
                  )}
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      <div className="mt-3">
        <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
      </div>

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
      <Dialog open={showImport} onOpenChange={(open) => { setShowImport(open); if (!open) { setCsvFileName(null); setCsvParsed(null); } }}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Bulk-upload members</DialogTitle>
            <DialogDescription>
              Upload a CSV of your roster. Anyone new gets an account created automatically and an email with a link to set their password and get into the member portal — no manual passwords to hand out.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <button type="button" onClick={downloadCsvTemplate} className="text-[12.5px] font-semibold text-accent hover:underline">
              <Download size={12} className="inline mr-1 -mt-0.5" />
              Download a CSV template
            </button>

            <label
              className={cn(
                "flex flex-col items-center justify-center gap-2 rounded-lg border-2 border-dashed p-6 text-center cursor-pointer transition-colors",
                csvFileName ? "border-accent/50 bg-accent/5" : "border-border hover:border-accent/40 hover:bg-muted/40"
              )}
            >
              <Upload size={20} className="text-muted-foreground" />
              <p className="text-[13px] font-semibold">{csvFileName ?? "Click to choose a CSV file"}</p>
              <p className="text-[11.5px] text-muted-foreground">Required columns: firstName, lastName, email, graduationYear. Optional: phone, studentId.</p>
              <input
                type="file"
                accept=".csv,text/csv"
                className="hidden"
                onChange={(e) => { const f = e.target.files?.[0]; if (f) handleCsvFile(f); e.target.value = ""; }}
              />
            </label>

            {csvParsed && (
              <div className={cn("rounded-lg border px-3 py-2.5 text-[12.5px]", csvParsed.rows.length > 0 ? "border-success/40 bg-success/5" : "border-destructive/40 bg-destructive/5")}>
                {csvParsed.rows.length > 0 ? (
                  <p className="font-semibold text-success">{csvParsed.rows.length} member{csvParsed.rows.length === 1 ? "" : "s"} ready to upload</p>
                ) : (
                  <p className="font-semibold text-destructive">No valid rows found</p>
                )}
                {csvParsed.errors.length > 0 && (
                  <ul className="mt-1.5 space-y-0.5 text-muted-foreground">
                    {csvParsed.errors.slice(0, 5).map((e, i) => <li key={i}>&bull; {e}</li>)}
                    {csvParsed.errors.length > 5 && <li>&bull; …and {csvParsed.errors.length - 5} more</li>}
                  </ul>
                )}
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setShowImport(false); setCsvFileName(null); setCsvParsed(null); }}>Cancel</Button>
            <Button
              disabled={!csvParsed || csvParsed.rows.length === 0 || importMut.isPending}
              onClick={() => csvParsed && importMut.mutate(csvParsed.rows)}
            >
              {importMut.isPending
                ? <><Loader2 size={14} className="animate-spin" />Uploading…</>
                : csvParsed?.rows.length ? `Upload ${csvParsed.rows.length} member${csvParsed.rows.length === 1 ? "" : "s"}` : "Upload members"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Add Member Dialog */}
      <Dialog open={showAddMember} onOpenChange={setShowAddMember}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Add Member</DialogTitle>
            <DialogDescription>
              Adds the member directly with an active status; they&apos;ll use &quot;Forgot password&quot; to set their own password on first login.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>First name</Label>
                <Input value={addMemberForm.firstName} onChange={(e) => setAddMemberForm((f) => ({ ...f, firstName: e.target.value }))} />
              </div>
              <div className="space-y-1.5">
                <Label>Last name</Label>
                <Input value={addMemberForm.lastName} onChange={(e) => setAddMemberForm((f) => ({ ...f, lastName: e.target.value }))} />
              </div>
            </div>
            <div className="space-y-1.5">
              <Label>Email</Label>
              <Input type="email" value={addMemberForm.email} onChange={(e) => setAddMemberForm((f) => ({ ...f, email: e.target.value }))} />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Graduation year</Label>
                <Input type="number" placeholder="2024" value={addMemberForm.graduationYear} onChange={(e) => setAddMemberForm((f) => ({ ...f, graduationYear: e.target.value }))} />
              </div>
              <div className="space-y-1.5">
                <Label>Mobile number</Label>
                <PhoneInput value={addMemberForm.phone} onChange={(val) => setAddMemberForm((f) => ({ ...f, phone: val }))} />
              </div>
            </div>
            <div className="space-y-1.5">
              <Label>Student ID (optional)</Label>
              <Input value={addMemberForm.studentId} onChange={(e) => setAddMemberForm((f) => ({ ...f, studentId: e.target.value }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowAddMember(false)}>Cancel</Button>
            <Button
              disabled={
                addMemberMut.isPending ||
                !addMemberForm.firstName.trim() ||
                !addMemberForm.lastName.trim() ||
                !addMemberForm.email.trim() ||
                !addMemberForm.graduationYear ||
                !addMemberForm.phone.trim()
              }
              onClick={() =>
                addMemberMut.mutate({
                  firstName: addMemberForm.firstName.trim(),
                  lastName: addMemberForm.lastName.trim(),
                  email: addMemberForm.email.trim(),
                  graduationYear: Number(addMemberForm.graduationYear),
                  phone: addMemberForm.phone.trim(),
                  studentId: addMemberForm.studentId.trim() || undefined,
                })
              }
            >
              {addMemberMut.isPending ? <><Loader2 size={14} className="animate-spin" />Adding...</> : "Add member"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
