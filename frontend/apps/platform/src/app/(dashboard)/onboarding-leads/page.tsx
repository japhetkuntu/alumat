"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@alumni/ui";
import { Inbox } from "lucide-react";
import { addOnboardingLeadNote, getOnboardingLeads, updateOnboardingLeadStatus } from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";

const STATUS_OPTIONS = [
  { value: "all", label: "All statuses" },
  { value: "New", label: "New" },
  { value: "Contacted", label: "Contacted" },
  { value: "Approved", label: "Approved" },
  { value: "Rejected", label: "Rejected" },
];

function statusBadgeVariant(status: string) {
  switch (status) {
    case "New":
      return "info" as const;
    case "Contacted":
      return "warning" as const;
    case "Approved":
      return "success" as const;
    case "Rejected":
      return "destructive" as const;
    default:
      return "neutral" as const;
  }
}

export default function OnboardingLeadsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [statusFilter, setStatusFilter] = useState("all");
  const { data: leads = [], isLoading } = useQuery({
    queryKey: ["onboarding-leads", statusFilter],
    queryFn: () => getOnboardingLeads(statusFilter === "all" ? undefined : statusFilter),
  });
  const [activeId, setActiveId] = useState<string | undefined>(undefined);
  const active = leads.find((l) => l.id === activeId) ?? leads[0];

  const [noteOpen, setNoteOpen] = useState(false);
  const [note, setNote] = useState("");

  const [rejectOpen, setRejectOpen] = useState(false);
  const [rejectReason, setRejectReason] = useState("");

  const contactedMutation = useMutation({
    mutationFn: (id: string) => updateOnboardingLeadStatus(id, { status: "Contacted" }),
    onSuccess: () => {
      toast.success("Marked as contacted");
      queryClient.invalidateQueries({ queryKey: ["onboarding-leads"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const noteMutation = useMutation({
    mutationFn: (vars: { id: string; note: string }) => addOnboardingLeadNote(vars.id, vars.note),
    onSuccess: () => {
      toast.success("Note added");
      queryClient.invalidateQueries({ queryKey: ["onboarding-leads"] });
      setNoteOpen(false);
      setNote("");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const rejectMutation = useMutation({
    mutationFn: async (vars: { id: string; reason: string }) => {
      if (vars.reason.trim()) {
        await addOnboardingLeadNote(vars.id, vars.reason.trim());
      }
      return updateOnboardingLeadStatus(vars.id, { status: "Rejected" });
    },
    onSuccess: () => {
      toast.success("Request rejected");
      queryClient.invalidateQueries({ queryKey: ["onboarding-leads"] });
      setRejectOpen(false);
      setRejectReason("");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  return (
    <div className="p-7 max-w-[1500px]">
      <div className="flex items-end justify-between mb-6">
        <div>
          <h1 className="text-[24px] font-bold">Onboarding Requests</h1>
          <p className="text-muted-foreground text-[13px] mt-1">Review prospective institutions and approve them into the platform.</p>
        </div>
        <Select value={statusFilter} onValueChange={setStatusFilter}>
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {STATUS_OPTIONS.map((opt) => (
              <SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {isLoading ? (
        <p className="text-[13px] text-muted-foreground">Loading…</p>
      ) : leads.length === 0 ? (
        <Card>
          <EmptyState
            icon={<Inbox size={24} />}
            title="No onboarding requests"
            description="Requests submitted by prospective institutions will show up here for review."
          />
        </Card>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-[1fr_1.2fr] gap-4">
          <Card>
            <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">Requests</p></div>
            <CardContent className="p-0">
              {leads.map((l) => (
                <button
                  key={l.id}
                  onClick={() => setActiveId(l.id)}
                  className={`w-full text-left px-5 py-3.5 border-b border-border last:border-0 transition-colors ${
                    l.id === (activeId ?? leads[0]?.id) ? "bg-primary/5 border-l-4 border-l-primary" : "hover:bg-muted/40"
                  }`}
                >
                  <div className="flex justify-between items-start gap-2">
                    <p className="font-semibold text-[13.5px]">{l.institutionName}</p>
                    <Badge variant={statusBadgeVariant(l.status)}>{l.status}</Badge>
                  </div>
                  <p className="text-[12.5px] text-muted-foreground mt-1">{l.contactName} &middot; {l.ageHours}h ago</p>
                </button>
              ))}
            </CardContent>
          </Card>

          {active && (
            <Card>
              <CardContent className="p-5">
                <h2 className="text-[17px] font-semibold">{active.institutionName}</h2>
                <p className="text-[12.5px] text-muted-foreground mt-1">Submitted {active.ageHours}h ago &middot; {active.status}</p>

                <div className="border-t border-border mt-4 pt-3 text-[13.5px] leading-relaxed space-y-1">
                  <p><b>Contact:</b> {active.contactName}</p>
                  <p><b>Email:</b> {active.contactEmail}</p>
                  {active.contactPhone && <p><b>Phone:</b> {active.contactPhone}</p>}
                  {active.country && <p><b>Country:</b> {active.country}</p>}
                  {active.estimatedMemberCount && <p><b>Estimated members:</b> {active.estimatedMemberCount}</p>}
                </div>

                {active.message && (
                  <div className="border-t border-border mt-3 pt-3 text-[13.5px] leading-relaxed">
                    <b>Message</b><br />{active.message}
                  </div>
                )}

                {active.internalNote && (
                  <div className="border-t border-border mt-3 pt-3 text-[13.5px] leading-relaxed">
                    <b>Internal note</b><br />{active.internalNote}
                  </div>
                )}

                <div className="flex flex-wrap gap-2 mt-5">
                  <Button variant="outline" onClick={() => setNoteOpen(true)}>Add internal note</Button>
                  {active.status === "New" && (
                    <Button
                      variant="outline"
                      onClick={() => contactedMutation.mutate(active.id)}
                      disabled={contactedMutation.isPending}
                    >
                      {contactedMutation.isPending ? "Marking…" : "Mark contacted"}
                    </Button>
                  )}
                  {(active.status === "New" || active.status === "Contacted") && (
                    <Button onClick={() => router.push(`/institutions/new?fromLead=${active.id}`)}>
                      Approve
                    </Button>
                  )}
                  {(active.status === "New" || active.status === "Contacted") && (
                    <Button variant="destructive" onClick={() => setRejectOpen(true)}>Reject</Button>
                  )}
                </div>
              </CardContent>
            </Card>
          )}
        </div>
      )}

      <Dialog open={noteOpen} onOpenChange={setNoteOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add internal note</DialogTitle>
          </DialogHeader>
          <div className="mt-2">
            <Textarea rows={4} value={note} onChange={(e) => setNote(e.target.value)} placeholder="Internal context, not visible to the institution…" />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setNoteOpen(false)}>Cancel</Button>
            <Button
              onClick={() => active && noteMutation.mutate({ id: active.id, note })}
              disabled={!note.trim() || noteMutation.isPending}
            >
              {noteMutation.isPending ? "Saving…" : "Save note"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmModal
        open={rejectOpen}
        title="Reject onboarding request"
        message={`Are you sure you want to reject ${active?.institutionName ?? "this request"}? You can optionally record a reason below.`}
        confirmLabel="Reject"
        variant="destructive"
        isLoading={rejectMutation.isPending}
        onConfirm={() => active && rejectMutation.mutate({ id: active.id, reason: rejectReason })}
        onCancel={() => { setRejectOpen(false); setRejectReason(""); }}
      >
        <Textarea
          rows={3}
          value={rejectReason}
          onChange={(e) => setRejectReason(e.target.value)}
          placeholder="Reason for rejection (optional)…"
        />
      </ConfirmModal>
    </div>
  );
}
