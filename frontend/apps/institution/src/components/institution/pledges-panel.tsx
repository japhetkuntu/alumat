"use client";

import { useState } from "react";
import { toast } from "sonner";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Badge, Button, Card, CardContent, ConfirmModal, EmptyState, FormError, Input, Label, LoadError, formatCurrency, formatDate } from "@alumni/ui";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@alumni/ui";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { getCampaignPledges, remindPledge, updatePledge, type AdminPledge, type PledgeState } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";

const STATE_LABEL: Record<PledgeState, string> = {
  Pledged: "Pledged", PartPaid: "Part-paid", Fulfilled: "Fulfilled", Overdue: "Overdue", Cancelled: "Cancelled", WrittenOff: "Written off",
};
const STATE_VARIANT: Record<PledgeState, "success" | "warning" | "destructive" | "secondary" | "default"> = {
  Pledged: "default", PartPaid: "warning", Fulfilled: "success", Overdue: "destructive", Cancelled: "secondary", WrittenOff: "secondary",
};
const ACTIVE: PledgeState[] = ["Pledged", "PartPaid", "Overdue"];

function Figure({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div>
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="font-bold text-lg tabular-nums">{value}</p>
      {hint && <p className="text-[12px] text-muted-foreground">{hint}</p>}
    </div>
  );
}

/**
 * Pledges to one fundraiser, for administrators only. Collected, pledged and still-to-come are three separate
 * figures: a pledge is a promise, never money in hand, and must not be added into the collected total.
 */
export function PledgesPanel({ campaignId, canManage }: { campaignId: string; canManage: boolean }) {
  const qc = useQueryClient();
  const [editing, setEditing] = useState<AdminPledge | null>(null);
  const [closing, setClosing] = useState<{ pledge: AdminPledge; status: "Cancelled" | "WrittenOff" } | null>(null);

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ["admin-campaign-pledges", campaignId],
    queryFn: () => getCampaignPledges(campaignId),
  });
  const refresh = () => qc.invalidateQueries({ queryKey: ["admin-campaign-pledges", campaignId] });

  const remind = useMutation({
    mutationFn: (id: string) => remindPledge(id),
    onSuccess: () => toast.success("Reminder sent"),
    onError: (e) => toast.error(handleApiError(e)),
  });
  const close = useMutation({
    mutationFn: ({ id, status }: { id: string; status: "Cancelled" | "WrittenOff" }) => updatePledge(id, { status }),
    onSuccess: () => { toast.success("Pledge updated"); setClosing(null); refresh(); },
    onError: (e) => { toast.error(handleApiError(e)); setClosing(null); },
  });

  if (isLoading) return null;
  if (isError || !data) return <LoadError title="Couldn't load pledges" onRetry={() => void refetch()} />;
  if (data.pledges.length === 0) return null; // nothing pledged yet: no need to add noise to the page

  return (
    <Card>
      <CardContent className="p-[18px] space-y-4">
        <div>
          <h2 className="text-[15px] font-semibold m-0">Pledges</h2>
          <p className="text-xs text-muted-foreground mt-1">
            Promises to give, not money received. Only administrators see this. Members are reminded automatically before and after their date.
          </p>
        </div>

        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
          <Figure label="Collected" value={formatCurrency(data.collected)} hint="Real payments" />
          <Figure label="Pledged" value={formatCurrency(data.pledgedTotal)} hint="Promised, not received" />
          <Figure label="Still to come" value={formatCurrency(data.outstandingTotal)} hint="Pledged minus paid" />
          <Figure label="Overdue" value={String(data.overdueCount)} />
        </div>

        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Member</TableHead>
              <TableHead className="text-right">Pledged</TableHead>
              <TableHead className="text-right">Paid</TableHead>
              <TableHead>By</TableHead>
              <TableHead>Status</TableHead>
              {canManage && <TableHead />}
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.pledges.map((p) => (
              <TableRow key={p.id}>
                <TableCell className="font-medium">{p.memberName}</TableCell>
                <TableCell className="text-right tabular-nums">{formatCurrency(p.amount)}</TableCell>
                <TableCell className="text-right tabular-nums">{formatCurrency(p.paid)}</TableCell>
                <TableCell>{formatDate(p.dueDate)}</TableCell>
                <TableCell>
                  <Badge variant={STATE_VARIANT[p.state]}>{STATE_LABEL[p.state]}</Badge>
                  {p.note && <p className="text-[12px] text-muted-foreground mt-0.5">{p.note}</p>}
                </TableCell>
                {canManage && (
                  <TableCell className="text-right whitespace-nowrap">
                    {ACTIVE.includes(p.state) && (
                      <>
                        <Button size="sm" variant="ghost" disabled={remind.isPending} onClick={() => remind.mutate(p.id)}>Remind</Button>
                        <Button size="sm" variant="ghost" onClick={() => setEditing(p)}>Edit</Button>
                        <Button size="sm" variant="ghost" onClick={() => setClosing({ pledge: p, status: "WrittenOff" })}>Write off</Button>
                      </>
                    )}
                  </TableCell>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>

      {editing && <EditPledgeDialog pledge={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); refresh(); }} />}
      <ConfirmModal
        open={!!closing}
        title="Write off this pledge?"
        message={closing ? `${closing.pledge.memberName}'s pledge of ${formatCurrency(closing.pledge.amount)} will be closed and they won't be reminded again. This is recorded in the audit log.` : ""}
        confirmLabel="Write off"
        isLoading={close.isPending}
        onConfirm={() => closing && close.mutate({ id: closing.pledge.id, status: closing.status })}
        onCancel={() => setClosing(null)}
      />
    </Card>
  );
}

function EditPledgeDialog({ pledge, onClose, onSaved }: { pledge: AdminPledge; onClose: () => void; onSaved: () => void }) {
  const [dueDate, setDueDate] = useState(pledge.dueDate.slice(0, 10));
  const [amount, setAmount] = useState(String(pledge.amount));
  const [error, setError] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: () => updatePledge(pledge.id, {
      dueDate: dueDate !== pledge.dueDate.slice(0, 10) ? dueDate : undefined,
      amount: Number(amount) !== pledge.amount ? Number(amount) : undefined,
    }),
    onSuccess: () => { toast.success("Pledge updated"); onSaved(); },
    onError: (e) => setError(handleApiError(e)),
  });

  return (
    <Dialog open onOpenChange={(v) => { if (!v) onClose(); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit {pledge.memberName}&apos;s pledge</DialogTitle>
          <DialogDescription>You can move the date later or reduce the amount. A new date restarts the reminders.</DialogDescription>
        </DialogHeader>
        <div className="mt-4 space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="edit-pledge-date">Planned date</Label>
            <Input id="edit-pledge-date" type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="edit-pledge-amount">Amount (GHS, can only go down)</Label>
            <Input id="edit-pledge-amount" type="number" min="1" max={pledge.amount} step="any" value={amount} onChange={(e) => setAmount(e.target.value)} />
          </div>
        </div>
        <FormError message={error} className="mt-3" />
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button onClick={() => { setError(null); save.mutate(); }} isLoading={save.isPending} loadingText="Saving…">Save</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
