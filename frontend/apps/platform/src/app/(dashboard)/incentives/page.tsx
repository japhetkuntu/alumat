"use client";

import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  Badge, Button, Card, CardContent, Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle, EmptyState, FormError, Input, Label, LoadError, Plus,
} from "@alumni/ui";
import { addIncentiveEntry, getIncentives, voidIncentiveEntry, type IncentiveEntry } from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";
import { PageHeading } from "@/components/platform/page-heading";

const money = (n: number, c: string) => `${c} ${n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
const day = (iso: string) => new Date(iso).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });

function RecordDialog({ kind, staffId, staff, currency, onClose }: {
  kind: "Earned" | "Paid"; staffId: string; staff: { id: string; name: string; owed: number }[]; currency: string; onClose: () => void;
}) {
  const qc = useQueryClient();
  const [who, setWho] = useState(staffId);
  const [amount, setAmount] = useState("");
  const [reason, setReason] = useState("");
  const [reference, setReference] = useState("");
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [error, setError] = useState<string | null>(null);
  const owed = staff.find((s) => s.id === who)?.owed ?? 0;
  const value = Number(amount);

  const save = useMutation({
    mutationFn: () => addIncentiveEntry({ staffId: who, kind, amount: value, reason: reason.trim(), reference: reference.trim() || undefined, occurredOn: date }),
    onSuccess: () => { toast.success(kind === "Paid" ? "Payment recorded" : "Earning recorded"); qc.invalidateQueries({ queryKey: ["incentives"] }); onClose(); },
    onError: (e) => setError(handleApiError(e)),
  });

  const valid = who && value > 0 && reason.trim().length >= 3 && (kind !== "Paid" || value <= owed);

  return (
    <Dialog open onOpenChange={(v) => { if (!v) onClose(); }}>
      <DialogContent>
        <DialogHeader><DialogTitle>{kind === "Paid" ? "Record a payment" : "Record what they earned"}</DialogTitle></DialogHeader>
        <div className="space-y-4">
          <p className="text-[13.5px] text-muted-foreground">
            {kind === "Paid"
              ? "Nothing is sent from here. Pay them yourself (mobile money or bank), then record it so the balance is right."
              : "Add what the platform owes them, for example for an institution they onboarded."}
          </p>
          <div className="space-y-1.5">
            <Label htmlFor="inc-who">Representative</Label>
            <select id="inc-who" value={who} onChange={(e) => setWho(e.target.value)} className="h-10 w-full rounded-md border border-border bg-background px-3 text-[14px]">
              <option value="" disabled>Choose a representative</option>
              {staff.map((s) => <option key={s.id} value={s.id}>{s.name}{kind === "Paid" ? ` (owed ${money(s.owed, currency)})` : ""}</option>)}
            </select>
          </div>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="inc-amount">Amount ({currency})</Label>
              <Input id="inc-amount" inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value.replace(/[^\d.]/g, ""))} placeholder="0.00" />
              {kind === "Paid" && who && value > owed && <p className="text-[12.5px] text-destructive">That is more than they are owed ({money(owed, currency)}).</p>}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="inc-date">Date</Label>
              <Input id="inc-date" type="date" value={date} max={new Date().toISOString().slice(0, 10)} onChange={(e) => setDate(e.target.value)} />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="inc-reason">{kind === "Paid" ? "What it covers" : "Why they earned it"}</Label>
            <Input id="inc-reason" value={reason} onChange={(e) => setReason(e.target.value)} maxLength={300} placeholder={kind === "Paid" ? "October incentives" : "Onboarded Greenfield University"} />
          </div>
          {kind === "Paid" && (
            <div className="space-y-1.5">
              <Label htmlFor="inc-ref">Payment reference (optional)</Label>
              <Input id="inc-ref" value={reference} onChange={(e) => setReference(e.target.value)} maxLength={120} placeholder="Mobile money or bank reference" />
            </div>
          )}
          <FormError message={error} />
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button disabled={!valid} isLoading={save.isPending} loadingText="Saving" onClick={() => { setError(null); save.mutate(); }}>Record</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function VoidDialog({ entry, onClose }: { entry: IncentiveEntry; onClose: () => void }) {
  const qc = useQueryClient();
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const run = useMutation({
    mutationFn: () => voidIncentiveEntry(entry.id, reason.trim()),
    onSuccess: () => { toast.success("Entry voided"); qc.invalidateQueries({ queryKey: ["incentives"] }); onClose(); },
    onError: (e) => setError(handleApiError(e)),
  });
  return (
    <Dialog open onOpenChange={(v) => { if (!v) onClose(); }}>
      <DialogContent>
        <DialogHeader><DialogTitle>Void this entry</DialogTitle></DialogHeader>
        <div className="space-y-4">
          <p className="text-[13.5px] text-muted-foreground">It stays in the history, marked void, and no longer counts toward the balance.</p>
          <div className="space-y-1.5">
            <Label htmlFor="void-reason">Why</Label>
            <Input id="void-reason" value={reason} onChange={(e) => setReason(e.target.value)} maxLength={300} placeholder="Entered twice" />
          </div>
          <FormError message={error} />
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button variant="destructive" disabled={reason.trim().length < 3} isLoading={run.isPending} loadingText="Voiding" onClick={() => run.mutate()}>Void entry</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default function IncentivesPage() {
  const { data, isLoading, isError, refetch } = useQuery({ queryKey: ["incentives"], queryFn: getIncentives });
  const [record, setRecord] = useState<{ kind: "Earned" | "Paid"; staffId: string } | null>(null);
  const [voiding, setVoiding] = useState<IncentiveEntry | null>(null);
  const [only, setOnly] = useState<string>("");

  const currency = data?.currency ?? "GHS";
  const canRecord = data?.canRecord ?? false;
  const entries = useMemo(() => (data?.entries ?? []).filter((e) => !only || e.staffId === only), [data, only]);
  const totals = useMemo(() => (data?.balances ?? []).reduce((t, b) => ({ earned: t.earned + b.earned, paid: t.paid + b.paid, owed: t.owed + b.owed }), { earned: 0, paid: 0, owed: 0 }), [data]);

  return (
    <div className="mx-auto max-w-[1100px] space-y-6 p-4 sm:p-7">
      <PageHeading
        title="Incentives"
        description={canRecord
          ? "What each representative has earned, what you have paid them, and what is still owed. Payments are made outside AlumUnion and recorded here by hand."
          : "What you have earned, what has been paid to you, and what is still owed."}
      >
        {canRecord && (
          <>
            <Button variant="outline" size="sm" onClick={() => setRecord({ kind: "Earned", staffId: only })}><Plus size={14} /> Record earning</Button>
            <Button size="sm" onClick={() => setRecord({ kind: "Paid", staffId: only })}><Plus size={14} /> Record payment</Button>
          </>
        )}
      </PageHeading>

      {isLoading ? <div className="skeleton h-48 w-full" aria-busy="true" /> : isError || !data ? (
        <LoadError className="py-10" onRetry={() => refetch()} />
      ) : (
        <>
          <Card>
            <CardContent className="flex flex-wrap items-center gap-x-10 gap-y-3 p-4 sm:p-5">
              {([["Earned", totals.earned], ["Paid", totals.paid], ["Still owed", totals.owed]] as const).map(([label, value]) => (
                <div key={label}>
                  <p className="text-[20px] font-bold tabular-nums leading-tight">{money(value, currency)}</p>
                  <p className="text-[12px] text-muted-foreground">{label}</p>
                </div>
              ))}
            </CardContent>
          </Card>

          {canRecord && (
            data.balances.length === 0 ? (
              <EmptyState title="No representatives yet" description="Add a representative from Platform Staff, then record what they earn here." />
            ) : (
              <section aria-label="Balances" className="grid gap-2.5 sm:grid-cols-2 lg:grid-cols-3">
                {data.balances.map((b) => (
                  <button
                    key={b.staffId}
                    type="button"
                    onClick={() => setOnly(only === b.staffId ? "" : b.staffId)}
                    className={`border bg-card p-3.5 text-left transition-colors hover:bg-muted/50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary ${only === b.staffId ? "border-primary" : b.owed > 0 ? "border-warning/60" : "border-border"}`}
                  >
                    <span className="block truncate text-[14.5px] font-semibold">{b.staffName}</span>
                    <span className="mt-1 block text-[18px] font-bold tabular-nums">{money(b.owed, currency)} <span className="text-[12px] font-normal text-muted-foreground">owed</span></span>
                    <span className="block text-[12.5px] text-muted-foreground">
                      {money(b.earned, currency)} earned · {money(b.paid, currency)} paid{b.lastPaidOn ? ` · last paid ${day(b.lastPaidOn)}` : ""}
                    </span>
                  </button>
                ))}
              </section>
            )
          )}

          <section aria-label="Ledger" className="space-y-2">
            <div className="flex items-center justify-between gap-3">
              <h2 className="text-[15px] font-bold">Ledger{only && data.balances.find((b) => b.staffId === only) ? `: ${data.balances.find((b) => b.staffId === only)!.staffName}` : ""}</h2>
              {only && <Button size="sm" variant="outline" onClick={() => setOnly("")}>Show everyone</Button>}
            </div>
            {entries.length === 0 ? (
              <EmptyState title="Nothing recorded yet" description={canRecord ? "Record what a representative earned, then record payments as you make them." : "Nothing has been recorded for you yet."} />
            ) : (
              <ul className="divide-y divide-border border border-border bg-card">
                {entries.map((e) => (
                  <li key={e.id} className={`flex flex-wrap items-start justify-between gap-3 p-3.5 ${e.isVoided ? "opacity-60" : ""}`}>
                    <div className="min-w-0">
                      <p className="flex flex-wrap items-center gap-2 text-[14px] font-semibold">
                        {canRecord && <span className="truncate">{e.staffName}</span>}
                        <Badge variant={e.kind === "Paid" ? "success" : "info"}>{e.kind}</Badge>
                        {e.isVoided && <Badge variant="neutral">Void</Badge>}
                      </p>
                      <p className="mt-0.5 text-[13px]">{e.reason}{e.institutionName ? ` · ${e.institutionName}` : ""}</p>
                      <p className="text-[12.5px] text-muted-foreground">
                        {day(e.occurredOn)} · recorded by {e.recordedByName}{e.reference ? ` · ref ${e.reference}` : ""}
                        {e.isVoided && e.voidReason ? ` · voided: ${e.voidReason}` : ""}
                      </p>
                    </div>
                    <div className="flex items-center gap-3">
                      <span className={`text-[15px] font-bold tabular-nums ${e.isVoided ? "line-through" : ""}`}>{e.kind === "Paid" ? "−" : "+"}{money(e.amount, currency)}</span>
                      {canRecord && !e.isVoided && <Button size="sm" variant="outline" onClick={() => setVoiding(e)}>Void</Button>}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      )}

      {record && data && (
        <RecordDialog kind={record.kind} staffId={record.staffId} currency={currency}
          staff={data.balances.map((b) => ({ id: b.staffId, name: b.staffName, owed: b.owed }))} onClose={() => setRecord(null)} />
      )}
      {voiding && <VoidDialog entry={voiding} onClose={() => setVoiding(null)} />}
    </div>
  );
}
