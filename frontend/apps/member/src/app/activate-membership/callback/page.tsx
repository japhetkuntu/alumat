"use client";

import { useCallback, useEffect, useState, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { CheckCircle2, XCircle, Loader2, RefreshCcw, ShieldCheck, ArrowRight } from "lucide-react";
import Link from "next/link";

import { Button } from "@/components/ui/button";
import { getActivationStatus } from "@/lib/member-api";

type UIStatus = "loading" | "success" | "pending" | "error";

function ActivationCallbackContent() {
  const searchParams = useSearchParams();
  const reference = searchParams.get("reference") || searchParams.get("trxref");

  const [uiStatus, setUiStatus] = useState<UIStatus>(reference ? "loading" : "error");
  const [message, setMessage] = useState(reference ? "" : "No payment reference was found. Please contact support.");
  const [email, setEmail] = useState<string | undefined>();
  const [memberNumber, setMemberNumber] = useState<string | undefined>();

  const checkStatus = useCallback(async () => {
    if (!reference) return;
    setUiStatus("loading");
    try {
      const result = await getActivationStatus(reference);
      const s = result.status?.toLowerCase();
      if (result.email) setEmail(result.email);
      if (result.memberNumber) setMemberNumber(result.memberNumber);

      if (s === "confirmed") {
        setUiStatus("success");
        setMessage(result.message ?? "Your membership has been activated successfully.");
      } else if (s === "failed") {
        setUiStatus("error");
        setMessage(result.message ?? "Payment could not be completed. Please try again.");
      } else {
        setUiStatus("pending");
        setMessage(result.message ?? "Your payment is still being processed. Please check again in a moment.");
      }
    } catch {
      setUiStatus("error");
      setMessage("Unable to retrieve payment status. Please try again.");
    }
  }, [reference]);

  useEffect(() => {
    checkStatus();
  }, [checkStatus]);

  const loginHref = email
    ? `/login?email=${encodeURIComponent(email)}`
    : "/login";

  return (
    <div className="min-h-screen bg-gradient-to-b from-primary/5 via-background to-background">
      {/* Top bar */}
      <header className="sticky top-0 z-10 bg-background/80 backdrop-blur border-b border-border/50 px-4 py-3">
        <div className="max-w-lg mx-auto flex items-center gap-3">
          <div className="w-8 h-8 bg-primary rounded-xl flex items-center justify-center flex-shrink-0">
            <span className="text-xs font-bold text-primary-foreground">UM</span>
          </div>
          <span className="font-semibold text-sm text-foreground">UMaT Alumni</span>
          <div className="ml-auto flex items-center gap-1.5 text-xs text-muted-foreground">
            <ShieldCheck size={13} className="text-green-500" />
            Secured by Paystack
          </div>
        </div>
      </header>

      <main className="max-w-lg mx-auto px-4 py-16 flex flex-col items-center gap-8">
        {/* Status icon */}
        {uiStatus === "loading" && (
          <div className="w-20 h-20 rounded-full bg-primary/10 border border-primary/20 flex items-center justify-center">
            <Loader2 size={36} className="animate-spin text-primary" />
          </div>
        )}
        {uiStatus === "success" && (
          <div className="relative">
            <div className="w-20 h-20 rounded-full bg-green-500/10 border border-green-500/20 flex items-center justify-center">
              <CheckCircle2 size={40} className="text-green-500" />
            </div>
            <div className="absolute -inset-2 rounded-full border-2 border-green-500/20 animate-ping [animation-duration:2s] pointer-events-none" />
          </div>
        )}
        {uiStatus === "pending" && (
          <div className="w-20 h-20 rounded-full bg-amber-500/10 border border-amber-500/20 flex items-center justify-center">
            <Loader2 size={36} className="animate-spin text-amber-500" />
          </div>
        )}
        {uiStatus === "error" && (
          <div className="w-20 h-20 rounded-full bg-destructive/10 border border-destructive/20 flex items-center justify-center">
            <XCircle size={40} className="text-destructive" />
          </div>
        )}

        {/* Heading + message */}
        <div className="text-center space-y-2 max-w-sm">
          <h1 className="text-2xl font-bold text-foreground">
            {uiStatus === "loading" && "Confirming payment…"}
            {uiStatus === "success" && "Membership activated!"}
            {uiStatus === "pending" && "Payment in progress"}
            {uiStatus === "error" && "Something went wrong"}
          </h1>
          <p className="text-sm text-muted-foreground leading-relaxed">
            {uiStatus === "loading"
              ? "We're checking your payment status. This only takes a moment."
              : message}
          </p>
        </div>

        {/* Success details */}
        {uiStatus === "success" && (
          <div className="w-full rounded-2xl border border-green-500/20 bg-green-500/5 p-5 space-y-4">
            {memberNumber && (
              <div className="text-center">
                <p className="text-[11px] text-muted-foreground uppercase tracking-widest mb-1">
                  Your membership number
                </p>
                <p className="text-2xl font-extrabold text-primary font-mono tracking-wide">
                  {memberNumber}
                </p>
              </div>
            )}
            <p className="text-sm font-semibold text-foreground">What&apos;s next?</p>
            <ul className="space-y-2.5">
              {[
                "Your alumni membership number has been assigned",
                "Your account is now fully active",
                "Sign in to access the full alumni portal",
              ].map((item) => (
                <li key={item} className="flex items-start gap-2 text-xs text-muted-foreground">
                  <CheckCircle2 size={14} className="text-green-500 flex-shrink-0 mt-0.5" />
                  {item}
                </li>
              ))}
            </ul>
          </div>
        )}

        {/* Actions */}
        <div className="w-full space-y-3">
          {uiStatus === "success" && (
            <Link href={loginHref} className="block">
              <Button className="w-full h-12 font-semibold gap-2 rounded-xl">
                Sign in to your account
                <ArrowRight size={16} />
              </Button>
            </Link>
          )}

          {uiStatus === "pending" && (
            <>
              <Button
                className="w-full h-12 font-semibold gap-2 rounded-xl"
                onClick={checkStatus}
              >
                <RefreshCcw size={16} />
                Check again
              </Button>
              <p className="text-center text-xs text-muted-foreground">
                Paystack is still processing your payment. It usually completes within 30 seconds.
              </p>
            </>
          )}

          {uiStatus === "error" && (
            <>
              <Button
                variant="outline"
                className="w-full h-12 font-semibold gap-2 rounded-xl"
                onClick={checkStatus}
              >
                <RefreshCcw size={16} />
                Try again
              </Button>
              <p className="text-center text-xs text-muted-foreground">
                If you were charged and your account is not active, please{" "}
                <a
                  href="mailto:alumni@umat.edu.gh"
                  className="text-primary hover:underline font-medium"
                >
                  contact support
                </a>
                .
              </p>
            </>
          )}

          {uiStatus !== "loading" && uiStatus !== "success" && (
            <p className="text-center text-xs text-muted-foreground">
              Already have access?{" "}
              <Link href={loginHref} className="text-primary hover:underline font-medium">
                Sign in
              </Link>
            </p>
          )}
        </div>
      </main>
    </div>
  );
}

export default function ActivationCallbackPage() {
  return (
    <Suspense
      fallback={
        <div className="min-h-screen flex items-center justify-center">
          <Loader2 size={32} className="animate-spin text-primary" />
        </div>
      }
    >
      <ActivationCallbackContent />
    </Suspense>
  );
}

type StatusResult = "success" | "pending" | "error";