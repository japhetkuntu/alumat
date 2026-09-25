"use client";

import { useState } from "react";
import { toast } from "sonner";
import { useMutation } from "@tanstack/react-query";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@alumni/ui";
import { Button, Label, Textarea, FormError, cn } from "@alumni/ui";
import { reportContent, REPORT_REASONS, type ReportableType } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";

/**
 * A quiet "Report" link that opens a short form. The report goes to this institution's administrators, who
 * decide what to do. Kept small and plain on purpose: it should be easy to find when needed, not a feature
 * that competes with the page.
 */
export function ReportButton({
  entityType, entityId, entityTitle, className, label = "Report",
}: { entityType: ReportableType; entityId: string; entityTitle?: string; className?: string; label?: string }) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState<string>("");
  const [details, setDetails] = useState("");
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () => reportContent({ entityType, entityId, entityTitle, reason, details: details.trim() || undefined }),
    onSuccess: () => {
      toast.success("Thank you. Your report has been sent to the administrators.");
      setOpen(false);
      setReason("");
      setDetails("");
      setError(null);
    },
    onError: (e) => setError(handleApiError(e)),
  });

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className={cn("text-[12px] font-medium underline-offset-2 hover:underline", className)}
        style={{ color: "var(--muted-foreground)" }}
      >
        {label}
      </button>

      <Dialog open={open} onOpenChange={(v) => { setOpen(v); if (!v) setError(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Report this</DialogTitle>
            <DialogDescription>
              Tell the administrators what is wrong. They review reports and decide what to do. Your name is shared with them.
            </DialogDescription>
          </DialogHeader>

          <fieldset className="mt-4 space-y-2">
            <legend className="mb-1 text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>What is the problem?</legend>
            {REPORT_REASONS.map((r) => (
              <label key={r} className="flex cursor-pointer items-center gap-2.5 text-[14px]" style={{ color: "var(--foreground)" }}>
                <input type="radio" name="report-reason" value={r} checked={reason === r} onChange={() => setReason(r)} className="h-4 w-4 accent-primary" />
                {r}
              </label>
            ))}
          </fieldset>

          <div className="mt-4 space-y-1.5">
            <Label htmlFor="report-details">Anything else? <span className="font-normal" style={{ color: "var(--muted-foreground)" }}>(optional)</span></Label>
            <Textarea id="report-details" rows={3} maxLength={1000} value={details} onChange={(e) => setDetails(e.target.value)} className="resize-none text-[14px]" />
          </div>

          <FormError message={error} className="mt-3" />
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
            <Button onClick={() => { setError(null); mutation.mutate(); }} disabled={!reason} isLoading={mutation.isPending} loadingText="Sending…">Send report</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
