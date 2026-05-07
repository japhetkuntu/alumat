"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { CheckCircle2, XCircle, Loader2, RefreshCcw } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
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

      if (normalized === "confirmed" || normalized === "success") {
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
    localStorage.setItem("umat-paystack-pending-ref", reference);

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
    <div className="flex items-center justify-center min-h-[60vh] p-6">
      <Card className="w-full max-w-md text-center">
        <CardHeader>
          <div className="mx-auto mb-3">
            {status === "loading" ? (
              <Loader2 size={48} className="animate-spin text-primary" />
            ) : status === "success" ? (
              <CheckCircle2 size={48} className="text-green-600" />
            ) : (
              <XCircle size={48} className="text-destructive" />
            )}
          </div>
          <CardTitle>{statusLabel}</CardTitle>
          <CardDescription>{message}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="flex gap-2">
            <Button
              className="flex-1"
              variant={status === "success" ? "secondary" : "outline"}
              onClick={() => pollStatus()}
              disabled={status === "loading"}
            >
              <RefreshCcw className="mr-2 h-4 w-4" />
              {status === "loading" ? "Checking..." : `Check status (${counter}s)`}
            </Button>
            <Button
              className="flex-1"
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
