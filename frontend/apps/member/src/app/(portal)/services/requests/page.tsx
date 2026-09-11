"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { FileText, Download, ChevronDown, CheckCircle2, Circle, XCircle, Clock } from "@alumni/ui";
import { Card, CardContent, StatCard, StatCardSkeleton } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { Skeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { formatCurrency, formatDate } from "@alumni/ui";
import { cn } from "@alumni/ui";
import { getMyServiceRequests, getServiceTypes } from "@/lib/member-api";

const paymentVariant: Record<string, "success" | "secondary" | "warning" | "destructive"> = {
  Successful: "success",
  NotRequired: "secondary",
  Pending: "warning",
  Failed: "destructive",
};

function paymentLabel(status: string) {
  return status === "NotRequired" ? "Free" : status;
}

function RequestSkeleton() {
  return (
    <Card>
      <CardContent className="p-5 flex items-center gap-4">
        <Skeleton className="h-11 w-11 rounded-xl shrink-0" />
        <div className="flex-1 space-y-2">
          <Skeleton className="h-4 w-40" variant="text" />
          <Skeleton className="h-3 w-28" variant="text" />
        </div>
        <Skeleton className="h-6 w-20" />
      </CardContent>
    </Card>
  );
}

export default function MyServiceRequestsPage() {
  const { data, isLoading } = useQuery({
    queryKey: ["my-service-requests"],
    queryFn: () => getMyServiceRequests(1, 50),
  });
  // Requests only carry their current stage as a bare string — the parent
  // service type's own stage order is what turns that into a real progress
  // stepper, so it's fetched alongside (same trick the institution queue uses).
  const { data: typesData } = useQuery({
    queryKey: ["service-types-for-requests"],
    queryFn: () => getServiceTypes(1, 100),
  });
  const stagesByServiceType = useMemo(() => {
    const map = new Map<string, string[]>();
    typesData?.results.forEach((s) => map.set(s.id, s.stages));
    return map;
  }, [typesData]);

  const requests = data?.results ?? [];
  const [expandedId, setExpandedId] = useState<string | null>(null);

  const stats = useMemo(() => {
    const awaitingPayment = requests.filter((r) => r.paymentStatus === "Pending").length;
    const inProgress = requests.filter((r) => {
      if (r.paymentStatus !== "Successful") return false;
      const stages = stagesByServiceType.get(r.serviceTypeId) ?? [];
      return stages.length === 0 || r.currentStage !== stages[stages.length - 1];
    }).length;
    const completed = requests.filter((r) => {
      if (r.paymentStatus !== "Successful") return false;
      const stages = stagesByServiceType.get(r.serviceTypeId) ?? [];
      return stages.length > 0 && r.currentStage === stages[stages.length - 1];
    }).length;
    return { total: requests.length, inProgress, completed, awaitingPayment };
  }, [requests, stagesByServiceType]);

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">
      <PageHeader
        eyebrow="Services"
        title="My requests"
        description="Track the status of every service you've requested. Each one is reviewed and processed by your institution directly."
      />

      {isLoading ? (
        <>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 sm:gap-4">
            {Array.from({ length: 4 }).map((_, i) => <StatCardSkeleton key={i} />)}
          </div>
          <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
            {Array.from({ length: 3 }).map((_, i) => <RequestSkeleton key={i} />)}
          </div>
        </>
      ) : requests.length === 0 ? (
        <EmptyState icon={<FileText size={28} />} title="No requests yet" description="Requests you make will show up here with their progress." />
      ) : (
        <>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 sm:gap-4">
            <StatCard label="Total requests" value={stats.total} />
            <StatCard label="In progress" value={stats.inProgress} tone="accent" />
            <StatCard label="Completed" value={stats.completed} />
            <StatCard
              label="Awaiting payment"
              value={stats.awaitingPayment}
              sub={stats.awaitingPayment > 0 ? "Complete payment to move these forward" : "All caught up"}
            />
          </div>

          <div className="grid grid-cols-1 xl:grid-cols-2 gap-4 items-start">
            {requests.map((r) => {
              const stages = stagesByServiceType.get(r.serviceTypeId) ?? [];
              const currentIndex = stages.indexOf(r.currentStage);
              const isExpanded = expandedId === r.id;
              const showStepper = r.paymentStatus === "Successful" && stages.length > 0;

              return (
                <Card key={r.id} className="overflow-hidden">
                  <CardContent className="p-5 space-y-4">
                    <div className="flex items-start justify-between gap-3">
                      <div className="flex items-start gap-3 min-w-0">
                        <div className="w-11 h-11 rounded-xl flex items-center justify-center bg-accent/10 shrink-0">
                          <FileText size={18} className="text-accent" />
                        </div>
                        <div className="min-w-0">
                          <h3 className="font-semibold text-[14.5px] truncate">{r.serviceTypeName}</h3>
                          <p className="text-[12px] text-muted-foreground">#{r.requestNumber} · {formatDate(r.createdAt)}</p>
                        </div>
                      </div>
                      <div className="text-right shrink-0">
                        <p className="text-[15px] font-bold tabular-nums">{formatCurrency(r.amount)}</p>
                        <Badge variant={paymentVariant[r.paymentStatus]} size="sm" className="mt-1">{paymentLabel(r.paymentStatus)}</Badge>
                      </div>
                    </div>

                    {r.paymentStatus === "Pending" ? (
                      <div className="flex items-center gap-2.5 rounded-lg bg-warning/5 border border-warning/20 p-3">
                        <Clock size={16} className="text-warning shrink-0" />
                        <p className="text-[13px] text-muted-foreground">Awaiting payment confirmation — this will start moving once payment clears.</p>
                      </div>
                    ) : r.paymentStatus === "Failed" ? (
                      <div className="flex items-center gap-2.5 rounded-lg bg-destructive/5 border border-destructive/20 p-3">
                        <XCircle size={16} className="text-destructive shrink-0" />
                        <p className="text-[13px] text-muted-foreground">Payment didn&apos;t go through, so this request was never submitted.</p>
                      </div>
                    ) : showStepper ? (
                      <div className="flex items-center gap-0 pt-1">
                        {stages.map((stage, i) => {
                          const done = currentIndex >= 0 ? i <= currentIndex : false;
                          return (
                            <div key={stage} className="flex items-center flex-1 last:flex-none">
                              <div className="flex flex-col items-center gap-1.5 shrink-0" style={{ width: 74 }}>
                                {done ? (
                                  <CheckCircle2 size={18} className="text-accent" />
                                ) : (
                                  <Circle size={18} className="text-border" fill="currentColor" fillOpacity={0.15} />
                                )}
                                <span className={cn("text-[10.5px] text-center leading-tight px-0.5", done ? "font-medium text-foreground" : "text-muted-foreground")}>
                                  {stage}
                                </span>
                              </div>
                              {i < stages.length - 1 && (
                                <div className={cn("h-0.5 flex-1 -mt-4", i < currentIndex ? "bg-accent" : "bg-border")} />
                              )}
                            </div>
                          );
                        })}
                      </div>
                    ) : (
                      <Badge variant="secondary">{r.currentStage || "Submitted"}</Badge>
                    )}

                    {r.updates.length > 0 && (
                      <div className="border-t border-border/50 pt-3">
                        <button
                          onClick={() => setExpandedId(isExpanded ? null : r.id)}
                          className="flex items-center gap-1.5 text-[12.5px] font-medium text-accent hover:underline"
                        >
                          <ChevronDown size={14} className={cn("transition-transform", isExpanded && "rotate-180")} />
                          {isExpanded ? "Hide updates" : `View updates (${r.updates.length})`}
                        </button>

                        {isExpanded && (
                          <ol className="relative border-l border-border/60 ml-1.5 space-y-4 pl-6 mt-4">
                            {[...r.updates].reverse().map((u, i) => (
                              <li key={i} className="relative">
                                <span className="absolute -left-[29px] top-0.5 w-3.5 h-3.5 rounded-full bg-accent/15 border-2 border-accent" />
                                <div className="flex items-center gap-2 flex-wrap">
                                  {u.stage && <span className="text-[13px] font-medium">{u.stage}</span>}
                                  <span className="text-[11.5px] text-muted-foreground">{formatDate(u.changedAt)}</span>
                                </div>
                                {u.note && <p className="text-[12.5px] text-muted-foreground mt-0.5 leading-relaxed">{u.note}</p>}
                                {u.attachmentUrl && (
                                  <a href={u.attachmentUrl} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1.5 text-[12.5px] font-medium text-accent hover:underline mt-1">
                                    <Download size={12} /> Download file
                                  </a>
                                )}
                              </li>
                            ))}
                          </ol>
                        )}
                      </div>
                    )}
                  </CardContent>
                </Card>
              );
            })}
          </div>
        </>
      )}
    </div>
  );
}
