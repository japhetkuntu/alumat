"use client";

import { useState } from "react";
import { toast } from "sonner";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@alumni/ui";
import { Button, FormError, Input, Label, InstitutionAgreementBody, INSTITUTION_AGREEMENT_DATE } from "@alumni/ui";
import { acceptAgreement, getAgreementStatus } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";

/**
 * Asks the institution's Super Admin to read and accept the Institution Agreement, once per version.
 * Nothing else in the portal is usable until they do or sign out. Other admins never see it.
 */
export function AgreementGate() {
  const { user, logout } = useAuth();
  const qc = useQueryClient();
  const [title, setTitle] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const { data } = useQuery({
    queryKey: ["agreement-status"],
    queryFn: getAgreementStatus,
    enabled: user?.role === "SuperAdmin",
    staleTime: 5 * 60 * 1000,
    retry: 1,
  });

  const acceptMut = useMutation({
    mutationFn: () => acceptAgreement({ version: data!.currentVersion, title: title.trim() }),
    onSuccess: () => {
      toast.success("Thank you. The agreement has been recorded.");
      qc.invalidateQueries({ queryKey: ["agreement-status"] });
    },
    onError: (e) => setError(handleApiError(e)),
  });

  if (!data?.requiresAcceptance) return null;

  return (
    <Dialog open onOpenChange={() => { /* cannot be dismissed: accept or sign out */ }}>
      <DialogContent size="lg" className="flex max-h-[90dvh] flex-col p-0" onEscapeKeyDown={(e) => e.preventDefault()} onPointerDownOutside={(e) => e.preventDefault()}>
        <div className="border-b p-5 sm:p-6" style={{ borderColor: "var(--border)" }}>
          <DialogHeader>
            <DialogTitle>Institution Agreement</DialogTitle>
            <DialogDescription>
              Please read this and accept it on behalf of your institution. Version dated {INSTITUTION_AGREEMENT_DATE}.
            </DialogDescription>
          </DialogHeader>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto p-5 sm:p-6">
          <InstitutionAgreementBody />
        </div>

        <div className="space-y-3 border-t p-5 sm:p-6" style={{ borderColor: "var(--border)" }}>
          <div className="space-y-1.5">
            <Label htmlFor="agreement-title" required>Your role at the institution</Label>
            <Input id="agreement-title" required placeholder="e.g. Chairperson, Secretary, Registrar" value={title} onChange={(e) => setTitle(e.target.value)} maxLength={120} />
          </div>
          <label className="flex cursor-pointer items-start gap-2.5 text-[13px] leading-snug">
            <input type="checkbox" className="mt-0.5 h-4 w-4 shrink-0 accent-primary" checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} />
            <span>I am authorised to act for this institution, and I accept the Institution Agreement.</span>
          </label>
          <FormError message={error} />
          <div className="flex flex-wrap items-center justify-between gap-3">
            <button type="button" onClick={logout} className="text-[12.5px] underline-offset-2 hover:underline" style={{ color: "var(--muted-foreground)" }}>Sign out instead</button>
            <Button disabled={!confirmed || !title.trim()} isLoading={acceptMut.isPending} loadingText="Saving…" onClick={() => { setError(null); acceptMut.mutate(); }}>
              Accept and continue
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
