"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { CheckCircle2, XCircle, Loader2, RefreshCcw } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { cn } from "@alumni/ui";
import { getPaystackPaymentStatus } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";

export default function PaystackCallbackPage() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const reference = searchParams.get("reference") || searchParams.get("trxref");

  const [status, setStatus] = useState<"loading" | "pending" | "success" | "error">(
    reference ? "loading" : "error"
  );
  const [message, setMessage] = useState(reference ? "" : "No payment reference found");
  const [counter, setCounter] = useState(10);

  const statusLabel = useMemo(() => {
    switch (status) {
      case "loading":
        return "Verifying payment...";
      case "pending":
        return "Payment is pending";
      case "success":
        return "Payment confirmed";
      case "error":
        return "Payment failed";
      default:
        return "Payment status";
    }
  }, [status]);

  const pollStatus = useCallback(async () => {
    if (!reference) return;

    setStatus("loading");
    try {
      const response = await getPaystackPaymentStatus(reference);
      const normalized = response.status?.toLowerCase() ?? "unknown";

      if (normalized === "confirmed" || normalized === "success" || normalized === "successful") {
        setStatus("success");
        setMessage(response.message ?? "Your payment was successful and your contribution has been recorded.");
        return;
      }

      if (normalized === "pending" || normalized === "unknown") {
        setStatus("pending");
        setMessage(response.message ?? "We are still waiting on payment confirmation. Please check again shortly.");
        return;
      }

      // Treat any other status as failure (failed / aborted / timedout)
      setStatus("error");
      setMessage(response.message ?? `Payment status: ${response.status}`);
    } catch (err) {
      setStatus("error");
      setMessage(handleApiError(err));
    }
  }, [reference]);

  useEffect(() => {
    if (!reference) return;

    // Keep the reference so the contributions page can show the status modal
    localStorage.setItem("alumni-paystack-pending-ref", reference);

    // Redirect back to the contributions page (where the modal auto-opens)
    router.push("/contributions");
  }, [reference, router]);

  useEffect(() => {
    if (!reference) return;
    if (status === "success") return;

    const tick = setInterval(() => {
      setCounter((prev) => {
        if (prev <= 1) {
          pollStatus();
          return 10;
        }
        return prev - 1;
      });
    }, 1000);

    return () => clearInterval(tick);
  }, [reference, status, pollStatus]);

  return (
    <div className="flex items-center justify-center min-h-[70vh] p-4 sm:p-6">
      <Card className="w-full max-w-md text-center animate-in fade-in slide-in-from-bottom-3 duration-500">
        <CardHeader className="items-center pb-2">
          <div
            className={cn(
              "w-[72px] h-[72px] rounded-full flex items-center justify-center mb-2",
              status === "success" ? "bg-success/10" : status === "error" ? "bg-destructive/10" : "bg-warning/10",
            )}
          >
            {status === "loading" ? (
              <Loader2 size={30} className="animate-spin text-primary" />
            ) : status === "success" ? (
              <CheckCircle2 size={30} className="text-success" />
            ) : status === "pending" ? (
              <Loader2 size={30} className="text-warning" />
            ) : (
              <XCircle size={30} className="text-destructive" />
            )}
          </div>
          <Badge
            variant={status === "success" ? "success" : status === "error" ? "destructive" : "warning"}
            className="text-[11px] font-bold uppercase tracking-wide"
          >
            {statusLabel}
          </Badge>
          <CardTitle className="text-[22px] mt-1">
            {status === "success" ? "Payment confirmed" : status === "error" ? "We couldn't confirm this payment" : "We're checking your payment"}
          </CardTitle>
          <CardDescription className="leading-relaxed">{message}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="flex flex-col sm:flex-row gap-2">
            <Button
              className="flex-1 font-semibold gap-2"
              variant={status === "success" ? "secondary" : "outline"}
              onClick={() => pollStatus()}
              disabled={status === "loading"}
            >
              <RefreshCcw className="h-4 w-4" />
              {status === "loading" ? "Checking..." : `Check status (${counter}s)`}
            </Button>
            <Button
              className="flex-1 font-semibold"
              onClick={() => router.push("/contributions")}
            >
              Back to Contributions
            </Button>
          </div>
          {status === "success" && (
            <p className="text-xs text-muted-foreground">You will be redirected shortly.</p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
