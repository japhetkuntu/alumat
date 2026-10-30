"use client";

import { useEffect, useRef, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  Button, Card, CardContent, CardDescription, CardHeader, CardTitle, Input, Label, Mail, CheckCircle2, cn,
  Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle,
} from "@alumni/ui";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";
import {
  cancelEmailChange, confirmEmailChangeCurrent, confirmEmailChangeNew, resendEmailChangeCode, startEmailChange,
  type EmailChangeStatus,
} from "@/lib/member-api";

type Stage = "address" | "current" | "new" | "done";

const CODE_LENGTH = 6;

/** Six boxes for a one-time code: paste fills them all, backspace walks back. */
function CodeBoxes({ value, onChange, disabled }: { value: string; onChange: (v: string) => void; disabled?: boolean }) {
  const refs = useRef<(HTMLInputElement | null)[]>([]);
  const digits = Array.from({ length: CODE_LENGTH }, (_, i) => value[i] ?? "");

  function set(index: number, raw: string) {
    const clean = raw.replace(/\D/g, "");
    if (!clean) return;
    const next = value.padEnd(CODE_LENGTH, " ").split("");
    clean.split("").forEach((d, offset) => { if (index + offset < CODE_LENGTH) next[index + offset] = d; });
    onChange(next.join("").replace(/ /g, ""));
    refs.current[Math.min(index + clean.length, CODE_LENGTH - 1)]?.focus();
  }

  return (
    <div
      className="flex justify-center gap-2"
      onPaste={(e) => { e.preventDefault(); onChange(e.clipboardData.getData("text").replace(/\D/g, "").slice(0, CODE_LENGTH)); }}
    >
      {digits.map((d, i) => (
        <input
          key={i}
          ref={(el) => { refs.current[i] = el; }}
          type="text"
          inputMode="numeric"
          autoComplete="one-time-code"
          maxLength={CODE_LENGTH}
          value={d}
          disabled={disabled}
          aria-label={`Digit ${i + 1}`}
          onChange={(e) => set(i, e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Backspace" && !d && i > 0) {
              onChange(value.slice(0, i - 1));
              refs.current[i - 1]?.focus();
            }
          }}
          className={cn(
            "h-12 w-10 sm:w-12 rounded-xl border text-center text-xl font-bold transition-all duration-150",
            "focus:outline-none focus:ring-2 focus:ring-offset-0",
            d ? "border-primary bg-primary/5 text-primary" : "border-input bg-background text-foreground focus:border-primary focus:ring-primary/20",
          )}
          style={{ fontVariantNumeric: "tabular-nums" }}
        />
      ))}
    </div>
  );
}

/**
 * Change the email the member signs in with. Two codes, and nothing changes until both are right:
 * one to the address they have now (proves it is them, not someone at an unlocked screen), one to the new address
 * (proves it is reachable). Works the same whether they signed up with a password or with Google.
 */
export function ChangeEmailCard({ email }: { email: string }) {
  const qc = useQueryClient();
  const { setSession } = useAuth();
  const [open, setOpen] = useState(false);
  const [stage, setStage] = useState<Stage>("address");
  const [newEmail, setNewEmail] = useState("");
  const [code, setCode] = useState("");
  const [hints, setHints] = useState<{ current: string; next: string | null }>({ current: "", next: null });
  const [finalEmail, setFinalEmail] = useState("");
  const [error, setError] = useState<string | null>(null);

  function reset() {
    setStage("address"); setNewEmail(""); setCode(""); setError(null); setHints({ current: "", next: null });
  }

  function apply(status: EmailChangeStatus) {
    setHints({ current: status.currentEmailHint, next: status.newEmailHint });
    setCode("");
    setError(null);
    setStage(status.step === "New" ? "new" : "current");
  }

  const fail = (e: unknown) => setError(handleApiError(e));

  const startMut = useMutation({ mutationFn: () => startEmailChange(newEmail.trim()), onSuccess: apply, onError: fail });
  const currentMut = useMutation({ mutationFn: () => confirmEmailChangeCurrent(code), onSuccess: apply, onError: fail });
  const newMut = useMutation({
    mutationFn: () => confirmEmailChangeNew(code),
    onSuccess: (result) => {
      // The old session was tied to the old address; sign this device in again as the new one.
      setSession(result.session.user, result.session.tokens);
      setFinalEmail(result.email);
      setStage("done");
      qc.invalidateQueries({ queryKey: ["m-profile"] });
    },
    onError: fail,
  });
  const resendMut = useMutation({
    mutationFn: resendEmailChangeCode,
    onSuccess: () => { setCode(""); setError(null); toast.success("We sent a new code."); },
    onError: fail,
  });

  const busy = startMut.isPending || currentMut.isPending || newMut.isPending;

  // Wrong-code messages that end the change ("Too many wrong codes", expired) send the member back to the start.
  useEffect(() => {
    if (error && /start again|expired/i.test(error)) setStage("address");
  }, [error]);

  function close(next: boolean) {
    if (!next && stage !== "address" && stage !== "done") void cancelEmailChange().catch(() => undefined);
    setOpen(next);
    if (!next) setTimeout(reset, 200);
  }

  const emailLooksValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(newEmail.trim());

  return (
    <>
      <Card className="border-border/40 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-300">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Mail size={18} className="text-primary" />
            Email address
          </CardTitle>
          <CardDescription>The address you sign in with and where we send your notices</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <p className="min-w-0 truncate text-[14px] font-semibold text-foreground">{email}</p>
          <Button variant="outline" className="shrink-0 font-semibold text-[13.5px]" style={{ height: 40 }} onClick={() => setOpen(true)}>
            Change email
          </Button>
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={close}>
        <DialogContent className="max-w-md">
          {stage === "address" && (
            <form
              className="space-y-4"
              onSubmit={(e) => { e.preventDefault(); if (emailLooksValid && !busy) startMut.mutate(); }}
            >
              <DialogHeader>
                <DialogTitle>Change your email</DialogTitle>
                <DialogDescription>
                  We will send a code to your current address ({email}) to make sure it is you, then another to the new address.
                </DialogDescription>
              </DialogHeader>
              <div className="space-y-1.5">
                <Label htmlFor="new-email" className="text-[13px] font-semibold">New email address</Label>
                <Input
                  id="new-email" type="email" autoComplete="email" autoFocus placeholder="you@example.com"
                  value={newEmail} onChange={(e) => { setNewEmail(e.target.value); setError(null); }} className="h-11 text-[14px]"
                />
              </div>
              {error && <p className="text-[13px] font-medium text-destructive">{error}</p>}
              <Button type="submit" className="w-full font-semibold" style={{ height: 44 }} isLoading={startMut.isPending} loadingText="Sending code…" disabled={!emailLooksValid}>
                Send code
              </Button>
            </form>
          )}

          {(stage === "current" || stage === "new") && (
            <form
              className="space-y-5"
              onSubmit={(e) => { e.preventDefault(); if (code.length === CODE_LENGTH && !busy) (stage === "current" ? currentMut : newMut).mutate(); }}
            >
              <DialogHeader>
                <DialogTitle>{stage === "current" ? "Check your current email" : "Now your new email"}</DialogTitle>
                <DialogDescription>
                  {stage === "current"
                    ? <>Enter the 6-digit code we sent to <b>{hints.current}</b>.</>
                    : <>Enter the 6-digit code we sent to <b>{hints.next}</b>. Your email changes once this is right.</>}
                </DialogDescription>
              </DialogHeader>
              <CodeBoxes value={code} onChange={(v) => { setCode(v); setError(null); }} disabled={busy} />
              {error && <p className="text-center text-[13px] font-medium text-destructive">{error}</p>}
              <Button
                type="submit" className="w-full font-semibold" style={{ height: 44 }}
                isLoading={currentMut.isPending || newMut.isPending} loadingText="Checking…" disabled={code.length !== CODE_LENGTH}
              >
                {stage === "current" ? "Continue" : "Change my email"}
              </Button>
              <div className="flex items-center justify-between text-[13px] text-muted-foreground">
                <button type="button" className="hover:text-foreground disabled:opacity-50" disabled={resendMut.isPending} onClick={() => resendMut.mutate()}>
                  {resendMut.isPending ? "Sending…" : "Send a new code"}
                </button>
                <button type="button" className="hover:text-foreground" onClick={() => close(false)}>Cancel</button>
              </div>
              <p className="text-center text-[12px] text-muted-foreground">Codes expire after 10 minutes. Check spam if it does not arrive.</p>
            </form>
          )}

          {stage === "done" && (
            <div className="space-y-4 py-2 text-center">
              <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-success/10">
                <CheckCircle2 className="text-success" size={26} />
              </div>
              <DialogHeader className="items-center">
                <DialogTitle>Email changed</DialogTitle>
                <DialogDescription>
                  You now sign in with <b>{finalEmail}</b>. We signed you out of other devices, and sent a note to your old address in case this was not you.
                </DialogDescription>
              </DialogHeader>
              <Button className="w-full font-semibold" style={{ height: 44 }} onClick={() => close(false)}>Done</Button>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
