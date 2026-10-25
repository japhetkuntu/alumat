"use client";

import { EmptyState, cn } from "@alumni/ui";
import { FormError } from "@alumni/ui";
import { LoadError } from "@alumni/ui";
import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus } from "@alumni/ui";
import { toast } from "sonner";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { FormSheet } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { TableSkeleton } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@alumni/ui";
import { SettlementAccountFields, type SettlementAccountValue } from "@alumni/ui";
import {
  getBatches, createBatch, createBatchRange, updateBatch, deleteBatch, submitBatchPayoutSetup,
  getBatchPayoutBanks, resolveBatchPayoutAccount, type Batch,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";
import { useInstitutionNavTheme, DEFAULT_COHORT_LABEL, DEFAULT_COHORT_LABEL_PLURAL } from "@/components/institution/institution-layout";

const payoutStatusVariant: Record<Batch["payoutStatus"], "neutral" | "warning" | "success" | "destructive"> = {
  None: "neutral",
  Pending: "warning",
  Approved: "success",
  Rejected: "destructive",
};

function PayoutSetupModal({ batch, onClose }: { batch: Batch; onClose: () => void }) {
  // Everyone starts on the institution's account; a dedicated one is only chosen on purpose.
  const [useOwn, setUseOwn] = useState(batch.payoutStatus !== "None" && !batch.useInstitutionAccount);
  const [value, setValue] = useState<SettlementAccountValue>({
    settlementBankCode: "",
    settlementBankName: batch.settlementBankName ?? "",
    settlementAccountNumber: batch.settlementAccountNumber ?? "",
    settlementAccountName: batch.settlementAccountName ?? "",
  });
  const qc = useQueryClient();

  const [formError, setFormError] = useState<string | null>(null);
  const submitMut = useMutation({
    onMutate: () => setFormError(null),
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
    onError: (e) => { const message = handleApiError(e); setFormError(message); toast.error(message); },
  });

  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Payout setup: {batch.name}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <p className="text-[13px] text-muted-foreground">
            Choose whether this batch's contributions settle into the institution's own account, or its own dedicated one. Either way, this needs platform approval before it takes effect. Until then, this batch keeps using the institution's account.
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
        <FormError message={formError} className="mt-3" />
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

type CreateMode = "one" | "range";

const MAX_RANGE = 150;

function BatchForm({
  id, initial, onSave, onSaveRange, cohortLabel, cohortLabelPlural, mode, onModeChange, existingYears,
}: {
  id: string;
  initial?: Batch;
  onSave: (data: { name: string; year: number }) => void;
  onSaveRange?: (data: { fromYear: number; toYear: number; namePattern: string }) => void;
  cohortLabel: string;
  cohortLabelPlural: string;
  mode: CreateMode;
  onModeChange?: (mode: CreateMode) => void;
  /** Years that already have one, so the range can say what it will add and what it will keep. */
  existingYears: number[];
}) {
  const thisYear = new Date().getFullYear();
  const [name, setName] = useState(initial?.name ?? "");
  const [year, setYear] = useState(initial ? String(initial.year) : String(thisYear));
  const [from, setFrom] = useState("");
  const [to, setTo] = useState(String(thisYear));
  const [pattern, setPattern] = useState(`${cohortLabel} of {year}`);
  const [problem, setProblem] = useState<string | null>(null);

  // What the range would do, worked out as the years are typed.
  const fromN = Number(from);
  const toN = Number(to);
  const rangeValid = Number.isInteger(fromN) && Number.isInteger(toN) && fromN >= 1900 && toN <= 2200 && toN >= fromN && toN - fromN + 1 <= MAX_RANGE;
  const inRange = rangeValid ? Array.from({ length: toN - fromN + 1 }, (_, k) => fromN + k) : [];
  const kept = inRange.filter((y) => existingYears.includes(y));
  const toAdd = inRange.length - kept.length;
  const sample = (y: number) => (pattern.toLowerCase().includes("{year}") ? pattern.replace(/\{year\}/gi, String(y)) : `${pattern.trim()} ${y}`).trim();

  function rangeProblem(): string | null {
    if (!from || !to) return "Enter the first and last year.";
    if (!rangeValid) {
      if (toN < fromN) return "The last year can't be before the first.";
      if (toN - fromN + 1 > MAX_RANGE) return `Add at most ${MAX_RANGE} years at a time.`;
      return "Use years between 1900 and 2200.";
    }
    if (!pattern.trim()) return `Say how each one should be named, for example "${cohortLabel} of {year}".`;
    return null;
  }

  return (
    <form
      id={id}
      className="space-y-5"
      onSubmit={(e) => {
        e.preventDefault();
        if (mode === "range") {
          const p = rangeProblem();
          setProblem(p);
          if (!p) onSaveRange?.({ fromYear: fromN, toYear: toN, namePattern: pattern.trim() });
          return;
        }
        onSave({ name: name.trim(), year: Number(year) });
      }}
    >
      {onModeChange && (
        <div className="inline-flex border border-border" role="group" aria-label="How many to add">
          {([["one", "One year"], ["range", "Several years"]] as const).map(([value, label]) => (
            <button
              key={value}
              type="button"
              aria-pressed={mode === value}
              onClick={() => { setProblem(null); onModeChange(value); }}
              className={cn(
                "px-4 py-2 text-[13px] font-semibold transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary",
                mode === value ? "bg-primary text-primary-foreground" : "bg-card hover:bg-muted",
              )}
            >
              {label}
            </button>
          ))}
        </div>
      )}

      {mode === "one" ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label>Name</Label>
            <Input value={name} onChange={(e) => setName(e.target.value)} placeholder={`${cohortLabel} of 2020`} required />
          </div>
          <div className="space-y-2">
            <Label>Year</Label>
            <Input type="number" min={1900} max={2200} value={year} onChange={(e) => setYear(e.target.value)} required />
          </div>
        </div>
      ) : (
        <div className="space-y-4">
          <p className="text-[13px] text-muted-foreground">
            Add every year in a range at once. Years that already have a {cohortLabel.toLowerCase()} are skipped and left exactly as they are.
          </p>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>First year</Label>
              <Input type="number" min={1900} max={2200} value={from} placeholder="e.g. 1989" onChange={(e) => { setFrom(e.target.value); setProblem(null); }} />
            </div>
            <div className="space-y-2">
              <Label>Last year</Label>
              <Input type="number" min={1900} max={2200} value={to} onChange={(e) => { setTo(e.target.value); setProblem(null); }} />
            </div>
          </div>
          <div className="space-y-2">
            <Label>Name each one</Label>
            <Input value={pattern} maxLength={100} onChange={(e) => { setPattern(e.target.value); setProblem(null); }} placeholder={`${cohortLabel} of {year}`} />
            <p className="text-[12.5px] text-muted-foreground">{"{year}"} is replaced by each year. Without it, the year is added to the end.</p>
          </div>

          <div className="border-l-2 border-primary bg-muted/40 px-4 py-3 text-[13px]" aria-live="polite">
            {rangeValid ? (
              <>
                <p className="font-semibold">
                  {toAdd === 0
                    ? `Nothing to add: every year from ${fromN} to ${toN} already has a ${cohortLabel.toLowerCase()}.`
                    : `Will add ${toAdd} ${toAdd === 1 ? cohortLabel.toLowerCase() : cohortLabelPlural.toLowerCase()}, from ${sample(inRange.find((y) => !existingYears.includes(y))!)} on.`}
                </p>
                {kept.length > 0 && (
                  <p className="mt-0.5 text-muted-foreground">
                    Keeps {kept.length === 1 ? "the one" : `the ${kept.length}`} that already {kept.length === 1 ? "exists" : "exist"}: {kept.length > 8 ? `${kept.slice(0, 8).join(", ")} and ${kept.length - 8} more` : kept.join(", ")}.
                  </p>
                )}
              </>
            ) : (
              <p className="text-muted-foreground">Enter the first and last year to see what will be added.</p>
            )}
          </div>
          {problem && <p role="alert" className="text-[13px] font-medium text-destructive">{problem}</p>}
        </div>
      )}
    </form>
  );
}

export default function BatchesPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";
  const [showCreate, setShowCreate] = useState(false);
  const [createMode, setCreateMode] = useState<CreateMode>("one");
  const [editingBatch, setEditingBatch] = useState<Batch | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Batch | null>(null);
  const [payoutTarget, setPayoutTarget] = useState<Batch | null>(null);
  const qc = useQueryClient();

  const canSetUpPayout = (b: Batch) => isSuperAdmin || (user?.role === "ScopedAdmin" && user.yearGroups?.includes(b.year));

  const { data: batches = [], isLoading, isError, refetch } = useQuery({
    queryKey: ["batches"],
    queryFn: getBatches,
  });

  const { data: navTheme } = useInstitutionNavTheme();
  const cohortLabel = navTheme?.cohortLabel?.trim() || DEFAULT_COHORT_LABEL;
  const cohortLabelPlural = navTheme?.cohortLabelPlural?.trim() || DEFAULT_COHORT_LABEL_PLURAL;

  const createMut = useMutation({
    mutationFn: createBatch,
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["batches"] }); setShowCreate(false); toast.success("Batch created"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const rangeMut = useMutation({
    mutationFn: createBatchRange,
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: ["batches"] });
      setShowCreate(false);
      const kept = r.skippedYears.length;
      toast.success(
        r.created === 0
          ? `Nothing to add: every year in that range already had one.`
          : `Added ${r.created} ${r.created === 1 ? cohortLabel.toLowerCase() : cohortLabelPlural.toLowerCase()}${kept > 0 ? `. ${kept} already existed and ${kept === 1 ? "was" : "were"} left as ${kept === 1 ? "it was" : "they were"}.` : "."}`,
      );
    },
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
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">{cohortLabelPlural}</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">
            The graduating-class year groups your members register into. Set your own list instead of a generic year range.
          </p>
        </div>
        {isSuperAdmin && (
          <Button onClick={() => { setCreateMode("one"); setShowCreate(true); }}>
            <Plus size={16} />Add {cohortLabel.toLowerCase()}
          </Button>
        )}
      </header>

      <FormSheet
        open={showCreate || editingBatch !== null}
        onClose={() => { setShowCreate(false); setEditingBatch(null); }}
        title={editingBatch ? `Edit ${cohortLabel.toLowerCase()}` : `Add ${cohortLabel.toLowerCase()}`}
        description={editingBatch ? editingBatch.name : undefined}
        formId="batch-form"
        submitLabel={editingBatch ? "Save changes" : createMode === "range" ? `Add ${cohortLabelPlural.toLowerCase()}` : `Create ${cohortLabel.toLowerCase()}`}
        saving={createMut.isPending || rangeMut.isPending || updateMut.isPending}
      >
        {editingBatch ? (
          <BatchForm
            key={editingBatch.id}
            id="batch-form"
            initial={editingBatch}
            cohortLabel={cohortLabel}
            cohortLabelPlural={cohortLabelPlural}
            mode="one"
            existingYears={batches.map((b) => b.year)}
            onSave={(d) => updateMut.mutate({ id: editingBatch.id, ...d, isActive: editingBatch.isActive })}
          />
        ) : showCreate ? (
          <BatchForm
            id="batch-form"
            cohortLabel={cohortLabel}
            cohortLabelPlural={cohortLabelPlural}
            mode={createMode}
            onModeChange={setCreateMode}
            existingYears={batches.map((b) => b.year)}
            onSave={(d) => createMut.mutate(d)}
            onSaveRange={(d) => rangeMut.mutate(d)}
          />
        ) : null}
      </FormSheet>

      <Card className="overflow-hidden">
        <div className="px-4 py-3.5 border-b border-border flex items-center justify-between">
          <b className="text-[13.5px]">All {cohortLabelPlural.toLowerCase()}</b>
          <span className="text-[12.5px] text-muted-foreground">{batches.filter((b) => b.isActive).length} active &middot; {batches.length} total</span>
        </div>
        <CardContent className="p-0">
          <Table stackOnMobile className="min-w-[560px]">
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
              {isError ? (
                <TableRow><TableCell colSpan={5}><LoadError className="py-8" onRetry={() => refetch()} /></TableCell></TableRow>
              ) : isLoading ? (
                <TableSkeleton rows={4} cols={5} />
              ) : batches.length === 0 ? (
                <TableRow><TableCell colSpan={5}><EmptyState className="py-8" title={`${cohortLabelPlural} group your members by year`} description="Add the graduating years your institution uses so members pick their own when they register. Until you add some, members see the platform's default year range." /></TableCell></TableRow>
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
        title={`Delete this ${cohortLabel.toLowerCase()}?`}
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
