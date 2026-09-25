"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  Badge, Button, Card, CardContent, ChipRow, EmptyState, FormError, LoadError, Pagination, Textarea, Label,
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, CardSkeleton, ShieldAlert, formatDate,
} from "@alumni/ui";
import { getContentReports, reviewContentReport, type ContentReport } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";

const STATUS_FILTERS = [
  { value: "Open", label: "Waiting for review" },
  { value: "ActionTaken", label: "Action taken" },
  { value: "Dismissed", label: "Dismissed" },
  { value: "", label: "All" },
] as const;

const TYPE_LABELS: Record<ContentReport["entityType"], string> = {
  ForumThread: "Forum thread",
  MentorProfile: "Mentor profile",
  Job: "Job post",
  BusinessListing: "Business listing",
  Spotlight: "Spotlight",
};

// Where an admin goes to look at, edit or remove the reported item.
function itemHref(r: ContentReport): string {
  switch (r.entityType) {
    case "Job": return `/jobs/${r.entityId}`;
    case "ForumThread": return "/forum";
    case "MentorProfile": return "/mentorship";
    case "BusinessListing": return "/business-directory";
    case "Spotlight": return "/spotlights";
  }
}

const STATUS_BADGE: Record<ContentReport["status"], { label: string; variant: "warning" | "success" | "neutral" }> = {
  Open: { label: "Open", variant: "warning" },
  ActionTaken: { label: "Action taken", variant: "success" },
  Dismissed: { label: "Dismissed", variant: "neutral" },
};

export default function FlaggedContentPage() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const [status, setStatus] = useState<string>("Open");
  const [page, setPage] = useState(1);
  const [reviewing, setReviewing] = useState<{ report: ContentReport; outcome: "ActionTaken" | "Dismissed" } | null>(null);
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ["content-reports", status, page],
    queryFn: () => getContentReports(page, 20, status),
  });

  const reviewMut = useMutation({
    mutationFn: () => reviewContentReport(reviewing!.report.id, { status: reviewing!.outcome, note: note.trim() || undefined }),
    onSuccess: () => {
      toast.success("Report updated.");
      qc.invalidateQueries({ queryKey: ["content-reports"] });
      setReviewing(null);
      setNote("");
      setError(null);
    },
    onError: (e) => setError(handleApiError(e)),
  });

  const reopenMut = useMutation({
    mutationFn: (id: string) => reviewContentReport(id, { status: "Open" }),
    onSuccess: () => { toast.success("Report reopened."); qc.invalidateQueries({ queryKey: ["content-reports"] }); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (user && user.role !== "SuperAdmin") {
    return (
      <div className="mx-auto max-w-[1240px] p-4 sm:p-[26px]">
        <EmptyState title="Access denied" description="Only Super Admins can review flagged content." />
      </div>
    );
  }

  const reports = data?.results ?? [];

  return (
    <div className="mx-auto max-w-[1240px] space-y-5 p-4 sm:p-[26px]">
      <header>
        <h1 className="m-0 text-[20px] font-bold sm:text-[25px]">Flagged content</h1>
        <p className="mt-1.5 max-w-2xl text-[13px] text-muted-foreground">
          Members can report forum threads, mentor profiles, job posts, business listings and spotlights. You review each report and decide what to do. AlumUnion provides the tool and does not moderate for you.
        </p>
      </header>

      <ChipRow label="Filter reports" activeKey={status}>
        {STATUS_FILTERS.map((f) => (
          <button
            key={f.label}
            aria-pressed={status === f.value}
            onClick={() => { setStatus(f.value); setPage(1); }}
            className={`border px-3 py-2 text-[12.5px] font-semibold transition-colors ${status === f.value ? "border-primary/30 bg-primary/10 text-primary" : "border-border bg-white text-foreground hover:bg-muted"}`}
          >
            {f.label}
          </button>
        ))}
      </ChipRow>

      {isError ? (
        <LoadError onRetry={() => refetch()} />
      ) : isLoading ? (
        <div className="space-y-3">{Array.from({ length: 3 }).map((_, i) => <CardSkeleton key={i} />)}</div>
      ) : reports.length === 0 ? (
        <EmptyState
          icon={<ShieldAlert size={40} />}
          title={status === "Open" ? "No reports waiting" : "Nothing here yet"}
          description="When a member reports a post, mentor, job, business or spotlight, it appears here. Look at the item, then mark the report as action taken or dismiss it. Each decision is saved in the audit log."
        />
      ) : (
        <div className="space-y-3">
          {reports.map((r) => (
            <Card key={r.id}>
              <CardContent className="space-y-3 p-5">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="text-[11px] font-bold uppercase tracking-[0.1em] text-muted-foreground">{TYPE_LABELS[r.entityType]}</p>
                    <p className="mt-1 text-[15px] font-semibold leading-snug">{r.entityTitle || "Untitled"}</p>
                  </div>
                  <Badge variant={STATUS_BADGE[r.status].variant}>{STATUS_BADGE[r.status].label}</Badge>
                </div>

                <div className="text-[13px] leading-relaxed">
                  <p><span className="font-semibold">Reason:</span> {r.reason}</p>
                  {r.details && <p className="mt-1 whitespace-pre-wrap text-muted-foreground">{r.details}</p>}
                  <p className="mt-1 text-[12px] text-muted-foreground">Reported by {r.reporterName} on {formatDate(r.createdAt)}</p>
                  {r.status !== "Open" && (
                    <p className="mt-1 text-[12px] text-muted-foreground">
                      {r.status === "ActionTaken" ? "Action taken" : "Dismissed"} by {r.reviewedByName ?? "an admin"}{r.reviewedAt ? ` on ${formatDate(r.reviewedAt)}` : ""}
                      {r.resolutionNote ? `: ${r.resolutionNote}` : ""}
                    </p>
                  )}
                </div>

                <div className="flex flex-wrap gap-2">
                  <Link href={itemHref(r)}><Button size="sm" variant="outline">Look at the item</Button></Link>
                  {r.status === "Open" ? (
                    <>
                      <Button size="sm" onClick={() => { setReviewing({ report: r, outcome: "ActionTaken" }); setNote(""); setError(null); }}>Action taken</Button>
                      <Button size="sm" variant="outline" onClick={() => { setReviewing({ report: r, outcome: "Dismissed" }); setNote(""); setError(null); }}>Dismiss</Button>
                    </>
                  ) : (
                    <Button size="sm" variant="ghost" onClick={() => reopenMut.mutate(r.id)} disabled={reopenMut.isPending}>Reopen</Button>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
          <Pagination page={page} totalPages={data?.totalPages ?? 1} onPageChange={setPage} />
        </div>
      )}

      <Dialog open={!!reviewing} onOpenChange={(v) => { if (!v) { setReviewing(null); setError(null); } }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{reviewing?.outcome === "ActionTaken" ? "Mark as action taken" : "Dismiss this report"}</DialogTitle>
            <DialogDescription>
              {reviewing?.outcome === "ActionTaken"
                ? "Use this after you have removed, edited or dealt with the item. The reporter is not told."
                : "Use this when the report doesn't need action. The item stays as it is."}
            </DialogDescription>
          </DialogHeader>
          <div className="mt-4 space-y-1.5">
            <Label htmlFor="review-note">Note (optional)</Label>
            <Textarea id="review-note" rows={3} maxLength={500} value={note} onChange={(e) => setNote(e.target.value)} className="resize-none" />
          </div>
          <FormError message={error} className="mt-3" />
          <DialogFooter>
            <Button variant="outline" onClick={() => setReviewing(null)}>Cancel</Button>
            <Button onClick={() => { setError(null); reviewMut.mutate(); }} isLoading={reviewMut.isPending} loadingText="Saving…">Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
