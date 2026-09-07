"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus } from "@alumni/ui";
import { toast } from "sonner";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { TableSkeleton } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@alumni/ui";
import { SettlementAccountFields, type SettlementAccountValue } from "@alumni/ui";
import {
  getBatches, createBatch, updateBatch, deleteBatch, submitBatchPayoutSetup,
  getBatchPayoutBanks, resolveBatchPayoutAccount, type Batch,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";

const payoutStatusVariant: Record<Batch["payoutStatus"], "neutral" | "warning" | "success" | "destructive"> = {
  None: "neutral",
  Pending: "warning",
  Approved: "success",
  Rejected: "destructive",
};

function PayoutSetupModal({ batch, onClose }: { batch: Batch; onClose: () => void }) {
  const [useOwn, setUseOwn] = useState(!batch.useInstitutionAccount);
  const [value, setValue] = useState<SettlementAccountValue>({
    settlementBankCode: "",
    settlementBankName: batch.settlementBankName ?? "",
    settlementAccountNumber: batch.settlementAccountNumber ?? "",
    settlementAccountName: batch.settlementAccountName ?? "",
  });
  const qc = useQueryClient();

  const submitMut = useMutation({
    mutationFn: () => submitBatchPayoutSetup(batch.id, {
      useInstitutionAccount: !useOwn,
      settlementBankCode: useOwn ? value.settlementBankCode : undefined,
      settlementBankName: useOwn ? value.settlementBankName : undefined,
      settlementAccountNumber: useOwn ? value.settlementAccountNumber : undefined,
      settlementAccountName: useOwn ? value.settlementAccountName : undefined,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["batches"] });
      toast.success("Submitted for platform review");
      onClose();
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Payout setup — {batch.name}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <p className="text-[13px] text-muted-foreground">
            Choose whether this batch's contributions settle into the institution's own account, or its own dedicated one. Either way, this needs platform approval before it takes effect — until then, this batch keeps using the institution's account.
          </p>
          <div className="flex gap-2">
            <Button type="button" size="sm" variant={!useOwn ? "default" : "outline"} onClick={() => setUseOwn(false)}>
              Use institution's account
            </Button>
            <Button type="button" size="sm" variant={useOwn ? "default" : "outline"} onClick={() => setUseOwn(true)}>
              This batch's own account
            </Button>
          </div>
          {useOwn && (
            <SettlementAccountFields
              value={value}
              onChange={setValue}
              getBanks={getBatchPayoutBanks}
              resolveAccount={resolveBatchPayoutAccount}
            />
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button
            disabled={useOwn && (!value.settlementBankCode || !value.settlementAccountNumber || !value.settlementAccountName)}
            isLoading={submitMut.isPending}
            onClick={() => submitMut.mutate()}
          >
            Submit for review
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function BatchForm({
  initial, onSave, onCancel, saving,
}: {
  initial?: Batch;
  onSave: (data: { name: string; year: number }) => void;
  onCancel: () => void;
  saving: boolean;
}) {
  const [name, setName] = useState(initial?.name ?? "");
  const [year, setYear] = useState(initial ? String(initial.year) : String(new Date().getFullYear()));

  return (
    <Card>
      <CardHeader><CardTitle className="text-base">{initial ? "Edit batch" : "Add batch"}</CardTitle></CardHeader>
      <CardContent>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            onSave({ name: name.trim(), year: Number(year) });
          }}
        >
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Name</Label>
              <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Batch of 2020" required />
            </div>
            <div className="space-y-2">
              <Label>Year</Label>
              <Input type="number" min={1900} max={2200} value={year} onChange={(e) => setYear(e.target.value)} required />
            </div>
          </div>
          <div className="flex gap-3">
            <Button type="submit" size="sm" isLoading={saving} loadingText="Saving">
              {initial ? "Save changes" : "Create batch"}
            </Button>
            <Button type="button" size="sm" variant="outline" onClick={onCancel}>Cancel</Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

export default function BatchesPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";
  const [showCreate, setShowCreate] = useState(false);
  const [editingBatch, setEditingBatch] = useState<Batch | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Batch | null>(null);
  const [payoutTarget, setPayoutTarget] = useState<Batch | null>(null);
  const qc = useQueryClient();

  const canSetUpPayout = (b: Batch) => isSuperAdmin || (user?.role === "ScopedAdmin" && user.yearGroups?.includes(b.year));

  const { data: batches = [], isLoading } = useQuery({
    queryKey: ["batches"],
    queryFn: getBatches,
  });

  const createMut = useMutation({
    mutationFn: createBatch,
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["batches"] }); setShowCreate(false); toast.success("Batch created"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: ({ id, ...body }: { id: string; name: string; year: number; isActive: boolean }) => updateBatch(id, body),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["batches"] }); setEditingBatch(null); toast.success("Batch updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const deleteMut = useMutation({
    mutationFn: deleteBatch,
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["batches"] }); setDeleteTarget(null); toast.success("Batch deleted"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const toggleActive = (b: Batch) => updateMut.mutate({ id: b.id, name: b.name, year: b.year, isActive: !b.isActive });

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      <header className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Batches</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">
            The graduating-class year groups your members register into — set your own list instead of a generic year range.
          </p>
        </div>
        {isSuperAdmin && (
          <Button onClick={() => setShowCreate(true)}>
            <Plus size={16} />Add batch
          </Button>
        )}
      </header>

      {showCreate && (
        <BatchForm
          onSave={(d) => createMut.mutate(d)}
          onCancel={() => setShowCreate(false)}
          saving={createMut.isPending}
        />
      )}

      {editingBatch && (
        <BatchForm
          initial={editingBatch}
          onSave={(d) => updateMut.mutate({ id: editingBatch.id, ...d, isActive: editingBatch.isActive })}
          onCancel={() => setEditingBatch(null)}
          saving={updateMut.isPending}
        />
      )}

      <Card className="overflow-hidden">
        <div className="px-4 py-3.5 border-b border-border flex items-center justify-between">
          <b className="text-[13.5px]">All batches</b>
          <span className="text-[12.5px] text-muted-foreground">{batches.filter((b) => b.isActive).length} active &middot; {batches.length} total</span>
        </div>
        <CardContent className="p-0">
          <Table className="min-w-[560px]">
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Year</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Payout</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableSkeleton rows={5} cols={5} />
              ) : batches.length === 0 ? (
                <TableRow><TableCell colSpan={5} className="text-center py-8 text-muted-foreground">No batches yet — members see the platform&apos;s default year range until you add one.</TableCell></TableRow>
              ) : batches.map((b) => (
                <TableRow key={b.id}>
                  <TableCell className="font-medium">{b.name}</TableCell>
                  <TableCell>{b.year}</TableCell>
                  <TableCell>
                    <Badge variant={b.isActive ? "success" : "neutral"}>{b.isActive ? "Active" : "Inactive"}</Badge>
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2">
                      <Badge variant={payoutStatusVariant[b.payoutStatus]}>
                        {b.payoutStatus === "None" ? "Institution account" : b.payoutStatus}
                      </Badge>
                      {canSetUpPayout(b) && (
                        <Button size="sm" variant="outline" onClick={() => setPayoutTarget(b)}>
                          {b.payoutStatus === "None" ? "Set up" : "Edit"}
                        </Button>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>
                    {isSuperAdmin && (
                      <div className="flex items-center gap-2 justify-end">
                        <Button size="sm" variant="outline" onClick={() => setEditingBatch(b)}>Edit</Button>
                        <Button size="sm" variant="secondary" onClick={() => toggleActive(b)} isLoading={updateMut.isPending}>
                          {b.isActive ? "Deactivate" : "Activate"}
                        </Button>
                        <Button
                          size="sm"
                          variant="destructive"
                          onClick={() => setDeleteTarget(b)}
                          isLoading={deleteMut.isPending}
                        >
                          Delete
                        </Button>
                      </div>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <ConfirmModal
        open={!!deleteTarget}
        title="Delete this batch?"
        message={`Delete "${deleteTarget?.name ?? ""}"? This won't affect members already registered with this year.`}
        confirmLabel="Delete"
        variant="destructive"
        isLoading={deleteMut.isPending}
        onConfirm={() => { if (deleteTarget) deleteMut.mutate(deleteTarget.id); }}
        onCancel={() => setDeleteTarget(null)}
      />

      {payoutTarget && <PayoutSetupModal batch={payoutTarget} onClose={() => setPayoutTarget(null)} />}
    </div>
  );
}
