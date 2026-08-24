"use client";

import { useCallback, useEffect, useState, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { CheckCircle2, XCircle, Loader2, RefreshCcw, ShieldCheck, ArrowRight } from "lucide-react";
import Link from "next/link";

import { Button } from "@alumni/ui";
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
    // Legitimate external-system sync: fetching the activation status (a
    // network request) on mount/reference change, not adjusting state from a prop.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    checkStatus();
  }, [checkStatus]);

  const loginHref = email
    ? `/login?email=${encodeURIComponent(email)}`
    : "/login";

  return (
    <div className="min-h-screen bg-background">
      {/* Top bar */}
      <header className="bg-card border-b border-border px-4 sm:px-[6vw] h-[76px] flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 bg-primary rounded-xl flex items-center justify-center flex-shrink-0">
            <span className="text-xs font-bold text-primary-foreground">UM</span>
          </div>
          <span className="font-[family-name:var(--font-display)] font-semibold text-sm text-foreground">
            Alumni Portal
          </span>
        </div>
        <a href="mailto:alumni@example.com" className="text-xs font-semibold text-primary hover:underline">
          Support
        </a>
      </header>

      <main className="max-w-[620px] mx-auto px-4 sm:px-5 py-10 sm:py-16">
        <div className="rounded-[22px] border border-border bg-card p-7 sm:p-10 text-center flex flex-col items-center gap-6">
          {/* Status icon */}
          {uiStatus === "loading" && (
            <div className="w-16 h-16 rounded-full bg-primary/10 border border-primary/20 flex items-center justify-center">
              <Loader2 size={30} className="animate-spin text-primary" />
            </div>
          )}
          {uiStatus === "success" && (
            <div className="relative">
              <div className="w-16 h-16 rounded-full bg-success/10 border border-success/20 flex items-center justify-center">
                <CheckCircle2 size={32} className="text-success" />
              </div>
              <div className="absolute -inset-2 rounded-full border-2 border-success/20 animate-ping [animation-duration:2s] pointer-events-none" />
            </div>
          )}
          {uiStatus === "pending" && (
            <div className="w-16 h-16 rounded-full bg-warning/10 border border-warning/20 flex items-center justify-center">
              <Loader2 size={30} className="animate-spin text-warning" />
            </div>
          )}
          {uiStatus === "error" && (
            <div className="w-16 h-16 rounded-full bg-destructive/10 border border-destructive/20 flex items-center justify-center">
              <XCircle size={32} className="text-destructive" />
            </div>
          )}

          {/* Status eyebrow */}
          <span
            className={
              "text-xs font-extrabold tracking-wider -mt-2 " +
              (uiStatus === "success"
                ? "text-success"
                : uiStatus === "error"
                  ? "text-destructive"
                  : "text-warning")
            }
          >
            {uiStatus === "loading" && "CHECKING PAYMENT"}
            {uiStatus === "success" && "MEMBERSHIP ACTIVE"}
            {uiStatus === "pending" && "PAYMENT PENDING"}
            {uiStatus === "error" && "ACTIVATION ISSUE"}
          </span>

          {/* Heading + message */}
          <div className="space-y-2 max-w-sm">
            <h1 className="font-[family-name:var(--font-display)] text-[26px] sm:text-[32px] font-bold text-foreground leading-tight">
              {uiStatus === "loading" && "Confirming payment…"}
              {uiStatus === "success" && "Membership activated!"}
              {uiStatus === "pending" && "We're checking your activation"}
              {uiStatus === "error" && "Something went wrong"}
            </h1>
            <p className="text-sm text-muted-foreground leading-relaxed">
              {uiStatus === "loading"
                ? "We're checking your payment status. This only takes a moment."
                : message}
            </p>
          </div>

          {uiStatus === "pending" && (
            <div className="w-full h-2 rounded-full bg-muted overflow-hidden">
              <div className="h-full w-2/3 rounded-full bg-primary" />
            </div>
          )}

          {/* Success details */}
          {uiStatus === "success" && (
            <div className="w-full rounded-2xl border border-success/20 bg-success/5 p-5 space-y-4 text-left">
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
                    <CheckCircle2 size={14} className="text-success flex-shrink-0 mt-0.5" />
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
                    href="mailto:alumni@example.com"
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