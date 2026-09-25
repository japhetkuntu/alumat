"use client";

import { useState } from "react";
import { toast } from "sonner";
import { useMutation } from "@tanstack/react-query";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@alumni/ui";
import { Button, Input, Label, FormError } from "@alumni/ui";
import { deleteMyAccount } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";

/**
 * Closing an account. Deliberately low-key: a small grey line at the very bottom of the profile, not a button
 * in the settings cards. The dialog is plain about what goes and what stays, and asks for DELETE to be typed.
 * It only affects this institution: a member of two communities keeps the other account.
 */
export function DeleteAccountLink({ institutionName }: { institutionName: string }) {
  const { logout } = useAuth();
  const [open, setOpen] = useState(false);
  const [confirmation, setConfirmation] = useState("");
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () => deleteMyAccount(confirmation.trim()),
    onSuccess: () => {
      toast.success("Your account has been deleted.");
      logout();
    },
    onError: (e) => setError(handleApiError(e)),
  });

  return (
    <div className="pb-10 pt-6 text-center">
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="text-[11.5px] underline-offset-2 hover:underline"
        style={{ color: "var(--muted-foreground)", opacity: 0.7 }}
      >
        Close my account
      </button>

      <Dialog open={open} onOpenChange={(v) => { setOpen(v); if (!v) { setConfirmation(""); setError(null); } }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete your {institutionName} account?</DialogTitle>
            <DialogDescription>
              This can&apos;t be undone. It only closes your account with {institutionName}.
            </DialogDescription>
          </DialogHeader>

          <div className="mt-4 space-y-3 text-[13.5px] leading-relaxed" style={{ color: "var(--foreground)" }}>
            <p><strong>Removed:</strong> your profile, photo, contact details, directory listing, mentor profile, business listings, spotlights, RSVPs, community memberships and notification settings. Any monthly giving is cancelled.</p>
            <p><strong>Kept:</strong> payment, order and service records that {institutionName} needs for its accounts. They no longer show your name or contact details.</p>
            <p>You will be signed out straight away.</p>
          </div>

          <div className="mt-4 space-y-1.5">
            <Label htmlFor="delete-confirm">Type DELETE to confirm</Label>
            <Input id="delete-confirm" value={confirmation} onChange={(e) => setConfirmation(e.target.value)} autoComplete="off" />
          </div>

          <FormError message={error} className="mt-3" />
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>Keep my account</Button>
            <Button
              variant="destructive"
              disabled={confirmation.trim() !== "DELETE"}
              isLoading={mutation.isPending}
              loadingText="Deleting…"
              onClick={() => { setError(null); mutation.mutate(); }}
            >
              Delete my account
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
