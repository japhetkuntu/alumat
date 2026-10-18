"use client";

import { Suspense, useEffect } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { CheckCircle2, XCircle, Loader2, RefreshCcw } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { cn } from "@alumni/ui";
import { getEventTicketStatus } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";

/** Where Paystack sends a member after paying for an event ticket. Confirms the seat, then sends them back to the event. */
function TicketStatusContent() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const qc = useQueryClient();
  const reference = searchParams.get("reference") || searchParams.get("trxref");

  // Asks the server, which settles the payment with Paystack if the webhook hasn't landed yet, and keeps asking while it is pending.
  const { data: ticket, error, isFetching, refetch } = useQuery({
    queryKey: ["event-ticket-status", reference],
    queryFn: () => getEventTicketStatus(reference!),
    enabled: !!reference,
    retry: false,
    refetchInterval: (q) => (q.state.data?.status === "Pending" ? 8000 : false),
  });

  const confirmed = ticket?.status === "Successful";
  useEffect(() => {
    if (!confirmed) return;
    // The event page, the events list, the calendar and "Going" all read these.
    ["m-rsvps", "event", "event-updates", "m-events-list", "cal-rsvps", "cal-events"].forEach((k) => qc.invalidateQueries({ queryKey: [k] }));
  }, [confirmed, qc]);

  const status: "loading" | "pending" | "success" | "error" =
    !reference || error ? "error"
      : !ticket ? "loading"
      : ticket.status === "Successful" ? "success"
      : ticket.status === "Pending" ? "pending"
      : "error";
  const message = !reference ? "No ticket reference found" : error ? handleApiError(error) : ticket?.message ?? "";

  const eventHref = ticket?.eventId ? `/events/${ticket.eventId}` : "/events";

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
            {status === "loading" ? <Loader2 size={30} className="animate-spin text-primary" />
              : status === "success" ? <CheckCircle2 size={30} className="text-success" />
              : status === "pending" ? <Loader2 size={30} className="text-warning" />
              : <XCircle size={30} className="text-destructive" />}
          </div>
          <Badge
            variant={status === "success" ? "success" : status === "error" ? "destructive" : "warning"}
            className="text-[12px] font-bold uppercase tracking-wide"
          >
            {status === "success" ? "Ticket confirmed" : status === "error" ? "Payment failed" : status === "pending" ? "Payment is pending" : "Verifying payment..."}
          </Badge>
          <CardTitle className="text-[22px] mt-1">
            {status === "success" ? "You're going" : status === "error" ? "We couldn't confirm this payment" : "We're checking your payment"}
          </CardTitle>
          <CardDescription className="leading-relaxed">
            {status === "success" && ticket?.eventTitle ? `Your seat at "${ticket.eventTitle}" is reserved. ` : ""}{message}
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {status === "success" && (
            <p className="text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>
              Tickets can&apos;t be cancelled or refunded.
            </p>
          )}
          <div className="flex flex-col sm:flex-row gap-2">
            {status !== "success" && (
              <Button className="flex-1 font-semibold gap-2" variant="outline" onClick={() => void refetch()} disabled={isFetching}>
                <RefreshCcw className="h-4 w-4" />
                {isFetching ? "Checking..." : "Check again"}
              </Button>
            )}
            <Button className="flex-1 font-semibold" onClick={() => router.push(eventHref)}>
              {status === "success" ? "View the event" : status === "error" ? "Back to the event" : "Go to the event"}
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

export default function TicketStatusPage() {
  return (
    <Suspense fallback={<div className="flex min-h-[70vh] items-center justify-center"><Loader2 size={24} className="animate-spin text-primary" /></div>}>
      <TicketStatusContent />
    </Suspense>
  );
}
