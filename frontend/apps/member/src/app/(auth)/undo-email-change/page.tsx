"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { CheckCircle2 } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { handleApiError } from "@/lib/api-client";
import { undoEmailChange } from "@/lib/member-api";

function UndoEmailChange() {
  const token = useSearchParams().get("token") ?? "";
  const [state, setState] = useState<"ask" | "working" | "done" | "failed">("ask");
  const [message, setMessage] = useState("");

  // Needs a click rather than running on load: mail scanners open links, and that must not undo anything.
  async function undo() {
    setState("working");
    try {
      setMessage(await undoEmailChange(token));
      setState("done");
    } catch (err) {
      setMessage(handleApiError(err));
      setState("failed");
    }
  }

  if (!token) {
    return (
      <div className="w-full max-w-[420px] mx-auto text-center space-y-4">
        <h1 className="font-[family-name:var(--font-display)] text-[28px] font-semibold text-foreground">Invalid link</h1>
        <p className="text-sm text-muted-foreground">This link is missing its code. Open it again from the email we sent.</p>
      </div>
    );
  }

  if (state === "done") {
    return (
      <div className="w-full max-w-[420px] mx-auto text-center space-y-4 animate-in fade-in slide-in-from-bottom-4 duration-500">
        <div className="w-14 h-14 rounded-full bg-success/10 flex items-center justify-center mx-auto">
          <CheckCircle2 className="text-success" size={26} />
        </div>
        <h1 className="font-[family-name:var(--font-display)] text-[26px] font-semibold text-foreground">Old email restored</h1>
        <p className="text-sm text-muted-foreground">{message}</p>
        <Link href="/forgot-password" className="inline-block text-primary hover:underline font-semibold text-sm">Reset my password</Link>
      </div>
    );
  }

  return (
    <div className="w-full max-w-[420px] mx-auto animate-in fade-in slide-in-from-bottom-6 duration-700 space-y-6">
      <div className="space-y-2.5">
        <div className="text-sm font-semibold tracking-widest text-primary/90 uppercase">Security</div>
        <h1 className="font-[family-name:var(--font-display)] text-[32px] font-semibold text-foreground leading-tight">Restore your old email?</h1>
        <p className="text-sm text-muted-foreground">
          This puts your previous email address back on your account and signs everyone out. Do this only if you did not change your email yourself.
        </p>
      </div>
      {state === "failed" && <p className="text-[13px] font-medium text-destructive">{message}</p>}
      <Button className="w-full font-semibold" style={{ height: 44 }} onClick={undo} isLoading={state === "working"} loadingText="Restoring…">
        Restore my old email
      </Button>
      <p className="text-center text-sm text-muted-foreground">
        Changed it yourself? <Link href="/login" className="text-primary hover:underline font-semibold">Just sign in</Link>
      </p>
    </div>
  );
}

export default function UndoEmailChangePage() {
  return (
    <Suspense fallback={null}>
      <UndoEmailChange />
    </Suspense>
  );
}
