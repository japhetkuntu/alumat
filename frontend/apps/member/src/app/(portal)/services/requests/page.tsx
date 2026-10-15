"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { ChevronDown, Clock, FileText, XCircle } from "@alumni/ui";
import { Badge, Button, EmptyState, LoadError, PageHeader, Skeleton, cn, formatCurrency, formatDate } from "@alumni/ui";
import { getMyServiceRequests, getServiceRequestStatus, getServiceTypes } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { FilterTabs, StepList, StepProgress, UpdateTimeline } from "@/components/member/history-ui";
import type { ServiceRequest } from "@/types";

type Filter = "all" | "active" | "done" | "unpaid";

function RequestCard({ request, stages, onChecked }: { request: ServiceRequest; stages: string[]; onChecked: () => void }) {
  const [open, setOpen] = useState(false);
  const [checking, setChecking] = useState(false);
  const paid = request.paymentStatus === "Successful" || request.paymentStatus === "NotRequired";
  const current = stages.indexOf(request.currentStage);
  const done = paid && stages.length > 0 && current === stages.length - 1;
  const hasMore = request.updates.length > 0 || stages.length > 0;

  async function checkPayment() {
    if (!request.transactionRef) { toast.error("There is no payment reference to check for this request."); return; }
    setChecking(true);
    try {
      const r = await getServiceRequestStatus(request.transactionRef);
      toast(r.message || `Payment is ${r.paymentStatus}`);
      onChecked();
    } catch (e) {
      toast.error(handleApiError(e));
    } finally {
      setChecking(false);
    }
  }

  const badge = request.paymentStatus === "Pending" ? { label: "Awaiting payment", variant: "warning" as const }
    : request.paymentStatus === "Failed" ? { label: "Payment failed", variant: "destructive" as const }
    : done ? { label: "Complete", variant: "success" as const }
    : { label: "In progress", variant: "info" as const };

  return (
    <li className="border border-border bg-card">
      <div className="flex flex-wrap items-start justify-between gap-x-4 gap-y-2 p-4 sm:p-5">
        <div className="flex min-w-0 items-start gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center bg-primary/10 text-primary"><FileText size={17} /></span>
          <div className="min-w-0">
            <h2 className="break-words text-[15px] font-bold leading-snug">{request.serviceTypeName}</h2>
            <p className="text-[12.5px] text-muted-foreground">#{request.requestNumber} · {formatDate(request.createdAt)}</p>
          </div>
        </div>
        <div className="flex items-center gap-3">
          <Badge variant={badge.variant} size="sm">{badge.label}</Badge>
          <span className="text-[16px] font-bold tabular-nums">{request.amount > 0 ? formatCurrency(request.amount) : "Free"}</span>
        </div>
      </div>

      <div className="space-y-3 px-4 pb-4 sm:px-5 sm:pb-5">
        {request.paymentStatus === "Pending" && (
          <div className="flex flex-wrap items-center justify-between gap-3 border border-warning/40 bg-warning/5 p-3">
            <p className="flex items-start gap-2 text-[13px] text-muted-foreground">
              <Clock size={15} className="mt-0.5 shrink-0 text-warning" />
              We have not had confirmation of this payment yet. Your request starts moving once it clears.
            </p>
            <Button size="sm" variant="outline" isLoading={checking} loadingText="Checking" onClick={checkPayment}>Check payment</Button>
          </div>
        )}
        {request.paymentStatus === "Failed" && (
          <p className="flex items-start gap-2 border border-destructive/30 bg-destructive/5 p-3 text-[13px] text-muted-foreground">
            <XCircle size={15} className="mt-0.5 shrink-0 text-destructive" />
            The payment did not go through, so this request was not submitted. Nothing was charged.
          </p>
        )}

        {paid && stages.length > 0 && <StepProgress stages={stages} current={current} done={done} />}
        {paid && stages.length === 0 && <p className="text-[13px]"><span className="font-semibold">{request.currentStage || "Submitted"}</span></p>}

        {paid && hasMore && (
          <>
            <button
              type="button"
              aria-expanded={open}
              onClick={() => setOpen((v) => !v)}
              className="inline-flex min-h-10 items-center gap-1.5 text-[13px] font-semibold text-primary focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary"
            >
              <ChevronDown size={14} className={cn("transition-transform", open && "rotate-180")} />
              {open ? "Hide details" : request.updates.length > 0 ? `Steps and updates (${request.updates.length})` : "See all steps"}
            </button>
            {open && (
              <div className="space-y-5 border-t border-border pt-4">
                {stages.length > 0 && <StepList stages={stages} current={current} done={done} />}
                <UpdateTimeline updates={request.updates} />
              </div>
            )}
          </>
        )}
      </div>
    </li>
  );
}

export default function MyServiceRequestsPage() {
  const { data, isLoading, isError, refetch } = useQuery({ queryKey: ["my-service-requests"], queryFn: () => getMyServiceRequests(1, 50) });
  // A request only carries its current stage by name; the service type's own list of stages turns that into progress.
  const { data: typesData } = useQuery({ queryKey: ["service-types-for-requests"], queryFn: () => getServiceTypes(1, 100) });
  const stagesByType = useMemo(() => {
    const map = new Map<string, string[]>();
    typesData?.results.forEach((s) => map.set(s.id, s.stages));
    return map;
  }, [typesData]);

  const requests = useMemo(() => data?.results ?? [], [data]);
  const [filter, setFilter] = useState<Filter>("all");

  const stateOf = (r: ServiceRequest): Exclude<Filter, "all"> => {
    if (r.paymentStatus === "Pending") return "unpaid";
    if (r.paymentStatus === "Failed") return "done"; // closed, nothing left to follow
    const stages = stagesByType.get(r.serviceTypeId) ?? [];
    return stages.length > 0 && r.currentStage === stages[stages.length - 1] ? "done" : "active";
  };

  const counts = useMemo(() => {
    const c = { all: requests.length, active: 0, done: 0, unpaid: 0 };
    for (const r of requests) c[stateOf(r)]++;
    return c;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [requests, stagesByType]);
  const shown = requests.filter((r) => filter === "all" || stateOf(r) === filter);

  return (
    <div className="mx-auto max-w-[860px] space-y-5 p-4 sm:p-6 lg:p-8">
      <PageHeader eyebrow="Services" title="My requests" description="Every service you have asked for, and which step it is at. Your institution handles each one directly." />

      {isLoading ? (
        <div className="space-y-3">{Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-36 w-full" />)}</div>
      ) : isError || !data ? (
        <LoadError onRetry={() => refetch()} />
      ) : requests.length === 0 ? (
        <EmptyState
          icon={<FileText size={28} />}
          title="No requests yet"
          description="When you request a service, each step shows up here from submitted to complete."
          action={<Link href="/services"><Button size="sm" className="font-semibold">Browse services</Button></Link>}
        />
      ) : (
        <>
          <p className="text-[13.5px] text-muted-foreground">
            {requests.length} {requests.length === 1 ? "request" : "requests"}
            {counts.unpaid > 0 && <span className="font-semibold text-warning"> · {counts.unpaid} awaiting payment</span>}
          </p>
          <FilterTabs
            label="Filter requests"
            value={filter}
            onChange={setFilter}
            options={[
              { value: "all", label: "All", count: counts.all },
              { value: "active", label: "In progress", count: counts.active },
              { value: "done", label: "Complete", count: counts.done },
              { value: "unpaid", label: "Awaiting payment", count: counts.unpaid },
            ]}
          />
          {shown.length === 0 ? (
            <p className="border border-dashed border-border p-6 text-center text-[13.5px] text-muted-foreground">Nothing here right now.</p>
          ) : (
            <ul className="space-y-3">
              {shown.map((r) => <RequestCard key={r.id} request={r} stages={stagesByType.get(r.serviceTypeId) ?? []} onChecked={() => void refetch()} />)}
            </ul>
          )}
        </>
      )}
    </div>
  );
}
