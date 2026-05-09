"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Download, Loader2 } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { toast } from "sonner";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { FormSelect } from "@/components/ui/select";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { SearchModal } from "@/components/ui/search-modal";
import { formatCurrency, formatDate } from "@/lib/utils";
import { getContributions, getCampaigns, recordManualContribution } from "@/lib/admin-api";
import { handleApiError } from "@/lib/api-client";
import { TableSkeleton } from "@/components/ui/skeleton";
import type { ContributionStatus } from "@/types";

const statusVariant: Record<ContributionStatus, "success" | "warning" | "destructive"> = {
  Confirmed: "success",
  Pending: "warning",
  Rejected: "destructive",
};

export default function AdminContributionsPage() {
  const [showManualForm, setShowManualForm] = useState(false);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [compactRows, setCompactRows] = useState(false);
  const [form, setForm] = useState({ campaignId: "", memberNumber: "", memberName: "", memberEmail: "", amount: "", paymentMethod: "Manual", transactionRef: "", notes: "", confirmed: true, paidAt: new Date().toISOString().slice(0, 16) });
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ["admin-contributions", search, statusFilter, page],
    queryFn: () => getContributions({ page, pageSize, status: statusFilter || undefined, search: search || undefined }),
    placeholderData: (prev) => prev,
  });

  const { data: campaignsData } = useQuery({
    queryKey: ["admin-campaigns-list"],
    queryFn: () => getCampaigns(1, 100),
  });

  const recordMut = useMutation({
    mutationFn: () => recordManualContribution({
      campaignId: form.campaignId,
      memberNumber: form.memberNumber,
      memberName: form.memberName || undefined,
      memberEmail: form.memberEmail || undefined,
      amount: Number(form.amount),
      paymentMethod: form.paymentMethod,
      transactionRef: form.transactionRef || undefined,
      notes: form.notes || undefined,
      paidAt: form.paidAt || undefined,
      confirmed: form.confirmed,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin-contributions"] });
      setShowManualForm(false);
      setForm({
        campaignId: "",
        memberNumber: "",
        memberName: "",
        memberEmail: "",
        amount: "",
        paymentMethod: "Manual",
        transactionRef: "",
        notes: "",
        confirmed: true,
        paidAt: new Date().toISOString().slice(0, 16),
      });
      toast.success("Payment recorded");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });


  const items = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;
  const densityCellClass = compactRows ? "py-2" : "py-2.5";
  const densityRowClass = compactRows ? "h-[46px]" : "h-[52px]";

  const [exportLoading, setExportLoading] = useState(false);

  const handleExportCsv = async () => {
    setExportLoading(true);
    try {
      const res = await getContributions({ page: 1, pageSize: 5000, status: statusFilter || undefined, search: search || undefined });
      const rows = res.results ?? [];
      if (!rows.length) { toast.error("No data to export."); return; }
      const headers = ["id", "memberName", "memberNumber", "memberEmail", "campaignName", "amount", "status", "paymentMethod", "transactionRef", "paidAt", "createdAt"];
      const csv = [headers.join(","), ...rows.map((r) => headers.map((h) => JSON.stringify((r as unknown as Record<string, unknown>)[h] ?? "")).join(","))].join("\n");
      const url = URL.createObjectURL(new Blob([csv], { type: "text/csv" }));
      const a = document.createElement("a");
      a.href = url; a.download = "contributions.csv"; a.click();
      URL.revokeObjectURL(url);
    } catch (e) { toast.error(handleApiError(e)); } finally { setExportLoading(false); }
  };

  return (
    <div className="p-6 lg:p-8 space-y-6 page-enter">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-[22px] font-bold tracking-tight">Contributions</h1>
          <p className="text-muted-foreground text-[13px] mt-1">Track and manage all contribution records</p>
        </div>
        <div className="flex flex-wrap justify-end gap-2">
          <Button size="sm" variant="outline" className="h-9 px-3.5" onClick={() => setCompactRows((v) => !v)}>
            {compactRows ? "Comfortable Rows" : "Compact Rows"}
          </Button>
          <Button size="sm" variant="outline" className="h-9 px-3.5" onClick={() => setShowManualForm(!showManualForm)}><Plus size={14} />Record Payment</Button>
          <Button size="sm" variant="outline" className="h-9 px-3.5" disabled={exportLoading} onClick={handleExportCsv}>
            {exportLoading ? <><Loader2 size={14} className="animate-spin" />Exporting…</> : <><Download size={14} />Export</>}
          </Button>
        </div>
      </div>

      {showManualForm && (
        <Card>
          <CardHeader><CardTitle className="text-base">Record Manual Payment</CardTitle></CardHeader>
          <CardContent>
            <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); recordMut.mutate(); }}>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Campaign</Label>
                  <FormSelect placeholder="Select campaign" value={form.campaignId} onValueChange={(v) => setForm({ ...form, campaignId: v })} options={(campaignsData?.results ?? []).filter((c) => c.status === "Active").map((c) => ({ value: c.id, label: c.title }))} />
                </div>
                <div className="space-y-2">
                  <Label>Member Number</Label>
                  <Input placeholder="UMaT/CS/2021/0001" value={form.memberNumber} onChange={(e) => setForm({ ...form, memberNumber: e.target.value })} required />
                </div>
                <div className="space-y-2">
                  <Label>Member Name (optional)</Label>
                  <Input placeholder="John Doe" value={form.memberName} onChange={(e) => setForm({ ...form, memberName: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label>Member Email (optional)</Label>
                  <Input type="email" placeholder="john@example.com" value={form.memberEmail} onChange={(e) => setForm({ ...form, memberEmail: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label>Amount (GHS)</Label>
                  <Input type="number" placeholder="200" value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} required />
                </div>
                <div className="space-y-2">
                  <Label>Payment Method</Label>
                  <FormSelect value={form.paymentMethod} onValueChange={(v) => setForm({ ...form, paymentMethod: v })} options={[
                    { value: "Manual", label: "Manual" },
                    { value: "BankTransfer", label: "Bank Transfer" },
                    { value: "MobileMoney", label: "Mobile Money" },
                  ]} />
                </div>
                <div className="space-y-2">
                  <Label>Transaction Ref (optional)</Label>
                  <Input placeholder="e.g. TXN123456" value={form.transactionRef} onChange={(e) => setForm({ ...form, transactionRef: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label>Notes (optional)</Label>
                  <Input placeholder="Any notes..." value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label className="flex items-center gap-2">
                    <input
                      type="checkbox"
                      checked={form.confirmed}
                      onChange={(e) => setForm({ ...form, confirmed: e.target.checked })}
                      className="h-4 w-4 rounded border border-muted-foreground"
                    />
                    Mark as confirmed
                  </Label>
                  {form.confirmed && (
                    <Input
                      type="datetime-local"
                      value={form.paidAt}
                      onChange={(e) => setForm({ ...form, paidAt: e.target.value })}
                      placeholder="Paid at"
                    />
                  )}
                </div>
              </div>
              <div className="flex gap-3">
                <Button type="submit" size="sm" isLoading={recordMut.isPending} loadingText="Saving">Record Payment</Button>
                <Button type="button" size="sm" variant="outline" onClick={() => setShowManualForm(false)}>Cancel</Button>
              </div>
            </form>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-3 sm:p-4 flex flex-col sm:flex-row gap-2.5">
          <div className="flex-1 min-w-0">
            <SearchModal
              title="Search contributions"
              value={search}
              onChange={(value) => { setSearch(value); setPage(1); }}
              placeholder="Search by member number, ref, or notes..."
            >
              {isLoading ? (
                <p className="text-sm text-muted-foreground">Loading results…</p>
              ) : items.length === 0 ? (
                <p className="text-sm text-muted-foreground">No contributions match your search.</p>
              ) : (
                <div className="space-y-2 max-h-72 overflow-y-auto">
                  {items.slice(0, 5).map((c) => (
                    <div key={c.id} className="flex items-center justify-between gap-3 rounded-lg border border-border/40 bg-muted/30 px-3 py-2">
                      <div className="min-w-0">
                        <p className="text-sm font-semibold truncate">
                          {c.memberName ?? "Unknown"}
                        </p>
                        <p className="text-xs text-muted-foreground truncate">
                          {c.transactionRef ?? "No ref"} • {formatCurrency(c.amount)}
                        </p>
                      </div>
                      <Badge variant={statusVariant[c.status]} className="text-[10px] font-bold uppercase">
                        {c.status}
                      </Badge>
                    </div>
                  ))}
                  {items.length > 5 && (
                    <p className="text-xs text-muted-foreground">Showing {Math.min(5, items.length)} of {items.length} results. Close to view the full list.</p>
                  )}
                </div>
              )}
            </SearchModal>
          </div>
          <FormSelect value={statusFilter || "__all__"} onValueChange={(v) => { setStatusFilter(v === "__all__" ? "" : v); setPage(1); }} placeholder="All statuses" options={[
            { value: "__all__", label: "All statuses" },
            { value: "Pending", label: "Pending" },
            { value: "Confirmed", label: "Confirmed" },
            { value: "Rejected", label: "Rejected" },
          ]} />
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          <Table className="min-w-[820px] md:min-w-[1080px]">
            <TableHeader>
              <TableRow>
                <TableHead>Member</TableHead>
                <TableHead>Campaign</TableHead>
                <TableHead>Amount</TableHead>
                <TableHead>Ref</TableHead>
                <TableHead>Method</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Date</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableSkeleton rows={8} cols={7} />
              ) : items.length === 0 ? (
                <TableRow><TableCell colSpan={8} className="text-center py-8 text-muted-foreground">No contributions found</TableCell></TableRow>
              ) : items.map((c) => (
                <TableRow key={c.id} className={densityRowClass}>
                  <TableCell className={densityCellClass}>
                    <div className="flex items-center gap-3 max-w-[260px]">
                      <Avatar className="h-8 w-8">
                        {c.memberProfilePictureUrl ? (
                          <AvatarImage src={c.memberProfilePictureUrl} alt={c.memberName ?? "Member"} />
                        ) : (
                          <AvatarFallback>{(c.memberName ?? "?").slice(0, 1).toUpperCase()}</AvatarFallback>
                        )}
                      </Avatar>
                      <div className="min-w-0">
                        <p className="font-medium text-sm leading-tight truncate">{c.memberName ?? "Unknown"}</p>
                        <p className="text-xs text-muted-foreground truncate">{c.memberEmail ?? c.memberNumber ?? "Unknown"}</p>
                      </div>
                    </div>
                  </TableCell>
                  <TableCell className={`text-sm max-w-[220px] ${densityCellClass}`}><p className="truncate">{c.campaignTitle ?? "Unknown Campaign"}</p></TableCell>
                  <TableCell className={`font-medium whitespace-nowrap ${densityCellClass}`}>{formatCurrency(c.amount)}</TableCell>
                  <TableCell className={`text-xs text-muted-foreground max-w-[170px] ${densityCellClass}`}><p className="truncate">{c.transactionRef ?? "—"}</p></TableCell>
                  <TableCell className={`whitespace-nowrap ${densityCellClass}`}><Badge variant={c.paymentMethod === "Paystack" ? "info" : "secondary"}>{c.paymentMethod}</Badge></TableCell>
                  <TableCell className={`whitespace-nowrap ${densityCellClass}`}><Badge variant={statusVariant[c.status]}>{c.status}</Badge></TableCell>
                  <TableCell className={`text-sm text-muted-foreground whitespace-nowrap ${densityCellClass}`}>{c.confirmedAt ? formatDate(c.confirmedAt) : formatDate(c.createdAt)}</TableCell>
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
