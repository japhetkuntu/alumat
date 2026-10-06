"use client";

import { useState } from "react";
import { toast } from "sonner";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@alumni/ui";
import { Button, Input, Label, FormError, formatCurrency, formatDate } from "@alumni/ui";
import { cancelPledge, createPledge, getMyPledges, type MemberPledge } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";

const OPEN_STATES = ["Pledged", "PartPaid", "Overdue"];

function toDateInput(d: Date) {
  return d.toISOString().slice(0, 10);
}

/**
 * "Give later" for a fundraiser: promise an amount by a date, get gentle reminders, pay whenever ready. Shows the
 * member's own open pledge if they have one. Nothing here is visible to other members; only the institution's
 * administrators see pledges.
 *
 * Pledging is switched on per fundraiser by its administrators. When it is off ({@link allowNew} false) nobody can start
 * a pledge, but a member who already has one still sees it here and can cancel it.
 */
export function PledgeCard({ campaignId, campaignTitle, closesOn, allowNew }: { campaignId: string; campaignTitle: string; closesOn?: string; allowNew: boolean }) {
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const [amount, setAmount] = useState("");
  const [dueDate, setDueDate] = useState(() => toDateInput(new Date(Date.now() + 14 * 24 * 60 * 60 * 1000)));
  const [error, setError] = useState<string | null>(null);

  const { data: pledges } = useQuery({ queryKey: ["m-pledges"], queryFn: getMyPledges, staleTime: 60 * 1000 });
  const mine: MemberPledge | undefined = pledges?.find((p) => p.campaignId === campaignId && OPEN_STATES.includes(p.state));

  const create = useMutation({
    mutationFn: () => createPledge({ campaignId, amount: Number(amount), dueDate }),
    onSuccess: () => {
      toast.success("Pledge saved. We'll remind you as your date gets close.");
      qc.invalidateQueries({ queryKey: ["m-pledges"] });
      setOpen(false);
      setAmount("");
      setError(null);
    },
    onError: (e) => setError(handleApiError(e)),
  });

  const cancel = useMutation({
    mutationFn: (id: string) => cancelPledge(id),
    onSuccess: () => {
      toast.success("Pledge cancelled");
      qc.invalidateQueries({ queryKey: ["m-pledges"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const valid = Number(amount) > 0 && !!dueDate;
  const pastClose = !!closesOn && dueDate > closesOn.slice(0, 10);

  if (mine) {
    return (
      <div className="rounded-xl p-3.5 text-[13px]" style={{ background: "var(--secondary)", border: "1px solid var(--border)" }}>
        <p className="font-semibold" style={{ color: "var(--foreground)" }}>
          Your pledge: {formatCurrency(mine.amount)} by {formatDate(mine.dueDate)}
        </p>
        <p className="mt-0.5" style={{ color: "var(--muted-foreground)" }}>
          {mine.paid > 0 ? `${formatCurrency(mine.paid)} given so far, ${formatCurrency(mine.outstanding)} to go. ` : "Not paid yet. "}
          Give above whenever you are ready.
        </p>
        <button
          type="button"
          onClick={() => cancel.mutate(mine.id)}
          disabled={cancel.isPending}
          className="mt-1.5 text-[12px] font-medium underline-offset-2 hover:underline"
          style={{ color: "var(--muted-foreground)" }}
        >
          Cancel my pledge
        </button>
      </div>
    );
  }

  if (!allowNew) return null;

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="w-full text-center text-[13px] font-semibold underline-offset-2 hover:underline"
        style={{ color: "var(--primary)" }}
      >
        Can&apos;t give right now? Pledge for later
      </button>

      <Dialog open={open} onOpenChange={(v) => { setOpen(v); if (!v) setError(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Pledge to {campaignTitle}</DialogTitle>
            <DialogDescription>
              Tell us what you plan to give and when. No money moves now, and a pledge is a promise, not a payment, so it isn&apos;t
              binding. Only this community&apos;s administrators can see it.
            </DialogDescription>
          </DialogHeader>

          <div className="mt-4 space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="pledge-amount">Amount (GHS)</Label>
              <Input id="pledge-amount" type="number" inputMode="decimal" min="1" step="any" value={amount} onChange={(e) => setAmount(e.target.value)} placeholder="0.00" />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="pledge-date">I plan to give by</Label>
              <Input id="pledge-date" type="date" min={toDateInput(new Date())} value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
              <p className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>
                We&apos;ll send a reminder 3 days before, on the day, and once a week after if it&apos;s still open. Then we stop.
              </p>
              {pastClose && (
                <p className="text-[12px]" style={{ color: "var(--warning, var(--muted-foreground))" }}>
                  This fundraiser closes on {formatDate(closesOn!)}. A pledge for a later date can only be paid if the fundraiser is extended.
                </p>
              )}
            </div>
          </div>

          <FormError message={error} className="mt-3" />
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
            <Button onClick={() => { setError(null); create.mutate(); }} disabled={!valid} isLoading={create.isPending} loadingText="Saving…">Save pledge</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
