"use client";

import { LoadError } from "@alumni/ui";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { Input, Label } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@alumni/ui";
import { Inbox, formatDate } from "@alumni/ui";
import Link from "next/link";
import { LeadEditDialog, LEAD_SOURCE_OPTIONS } from "@/components/platform/lead-edit-dialog";
import { LeadImportDialog } from "@/components/platform/lead-import-dialog";
import { followUpState } from "@/lib/leads";
import { trialLabel } from "@/lib/activation";
import {
  addOnboardingLeadNote,
  createOnboardingLead,
  getOnboardingLeads,
  updateOnboardingLeadStatus,
  type CreateStaffOnboardingLeadRequest,
  type OnboardingLeadStatus,
} from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";

const STATUS_LABELS: Record<OnboardingLeadStatus, string> = {
  New: "New",
  Contacted: "Contacted",
  DemoBooked: "Demo booked",
  Trial: "Trial",
  Approved: "Approved",
  Rejected: "Rejected",
};

const STATUS_OPTIONS = [
  { value: "all", label: "All statuses" },
  ...(Object.keys(STATUS_LABELS) as OnboardingLeadStatus[]).map((value) => ({ value, label: STATUS_LABELS[value] })),
];

const SOURCE_OPTIONS = LEAD_SOURCE_OPTIONS.filter((o) => o !== "Website" && o !== "Import");

/**
 * Stages a lead can move to by hand. Trial and Approved both go through creating
 * the institution (Start trial / Approve), so the lead is always linked to it.
 */
const NEXT_STAGES: Partial<Record<OnboardingLeadStatus, OnboardingLeadStatus[]>> = {
  New: ["Contacted", "DemoBooked"],
  Contacted: ["DemoBooked"],
};

const STAGE_ACTION_LABELS: Partial<Record<OnboardingLeadStatus, string>> = {
  Contacted: "Mark contacted",
  DemoBooked: "Mark demo booked",
};

const PRE_INSTITUTION: OnboardingLeadStatus[] = ["New", "Contacted", "DemoBooked"];

function FollowUpBadge({ date }: { date?: string | null }) {
  const state = followUpState(date);
  if (!state || state === "upcoming") return null;
  return <Badge variant={state === "overdue" ? "destructive" : "warning"}>{state === "overdue" ? "Follow-up overdue" : "Follow up today"}</Badge>;
}

const EMPTY_LEAD: CreateStaffOnboardingLeadRequest = {
  institutionName: "",
  contactName: "",
  contactEmail: "",
  contactPhone: "",
  contactRole: "",
  source: "Warm intro",
  status: "Contacted",
  note: "",
  nextFollowUpAt: "",
};

function statusBadgeVariant(status: string) {
  switch (status) {
    case "New":
      return "info" as const;
    case "Contacted":
    case "DemoBooked":
    case "Trial":
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
  const { data: leads = [], isLoading, isError, refetch } = useQuery({
    queryKey: ["onboarding-leads", statusFilter],
    queryFn: () => getOnboardingLeads(statusFilter === "all" ? undefined : statusFilter),
  });
  const [activeId, setActiveId] = useState<string | undefined>(undefined);
  const active = leads.find((l) => l.id === activeId) ?? leads[0];

  const [noteOpen, setNoteOpen] = useState(false);
  const [note, setNote] = useState("");

  const [rejectOpen, setRejectOpen] = useState(false);
  const [rejectReason, setRejectReason] = useState("");

  const stageMutation = useMutation({
    mutationFn: (vars: { id: string; status: OnboardingLeadStatus }) => updateOnboardingLeadStatus(vars.id, { status: vars.status }),
    onSuccess: (_, vars) => {
      toast.success(`Moved to ${STATUS_LABELS[vars.status].toLowerCase()}`);
      queryClient.invalidateQueries({ queryKey: ["onboarding-leads"] });
      queryClient.invalidateQueries({ queryKey: ["activation-funnel"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const [editOpen, setEditOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const [logOpen, setLogOpen] = useState(false);
  const [newLead, setNewLead] = useState<CreateStaffOnboardingLeadRequest>(EMPTY_LEAD);
  const logMutation = useMutation({
    mutationFn: (req: CreateStaffOnboardingLeadRequest) =>
      createOnboardingLead({
        ...req,
        contactEmail: req.contactEmail?.trim() || undefined,
        contactPhone: req.contactPhone?.trim() || undefined,
        contactRole: req.contactRole?.trim() || undefined,
        note: req.note?.trim() || undefined,
        nextFollowUpAt: req.nextFollowUpAt || undefined,
      }),
    onSuccess: (lead) => {
      toast.success("Lead logged");
      queryClient.invalidateQueries({ queryKey: ["onboarding-leads"] });
      queryClient.invalidateQueries({ queryKey: ["activation-funnel"] });
      setActiveId(lead.id);
      setLogOpen(false);
      setNewLead(EMPTY_LEAD);
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
    <div className="p-4 sm:p-7 max-w-[1500px]">
      <div className="flex items-end justify-between mb-6">
        <div>
          <h1 className="text-[24px] font-bold">Onboarding Requests</h1>
          <p className="text-muted-foreground text-[13px] mt-1">Review prospective institutions, track them through demo and trial, and approve them into the platform.</p>
        </div>
        <div className="flex gap-2">
        <Button variant="outline" onClick={() => setImportOpen(true)}>Import CSV</Button>
        <Button variant="outline" onClick={() => setLogOpen(true)}>Log a lead</Button>
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
      </div>

      {isError ? (
        <LoadError onRetry={() => refetch()} />
      ) : isLoading ? (
        <p className="text-[13px] text-muted-foreground">Loading…</p>
      ) : leads.length === 0 ? (
        <Card>
          <EmptyState
            icon={<Inbox size={24} />}
            title="Requests from institutions that want to join"
            description="When a school or group fills in the request form on your marketing site, it lands here. Log outreach and warm intros yourself with Log a lead, so the activation funnel counts them."
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
                    l.id === (activeId ?? leads[0]?.id) ? "bg-accent/5 border-l-4 border-l-accent" : "hover:bg-muted/40"
                  }`}
                >
                  <div className="flex justify-between items-start gap-2">
                    <p className="font-semibold text-[13.5px]">{l.institutionName}</p>
                    <Badge variant={statusBadgeVariant(l.status)}>{STATUS_LABELS[l.status] ?? l.status}</Badge>
                  </div>
                  {followUpState(l.nextFollowUpAt) && followUpState(l.nextFollowUpAt) !== "upcoming" && (
                    <div className="mt-1.5"><FollowUpBadge date={l.nextFollowUpAt} /></div>
                  )}
                  <p className="text-[12.5px] text-muted-foreground mt-1">
                    {l.contactName} &middot; {l.ageHours < 48 ? `${l.ageHours}h ago` : `${Math.floor(l.ageHours / 24)}d ago`}
                    {l.source && <> &middot; {l.source}</>}
                  </p>
                </button>
              ))}
            </CardContent>
          </Card>

          {active && (
            <Card>
              <CardContent className="p-5">
                <h2 className="text-[17px] font-semibold">{active.institutionName}</h2>
                <p className="text-[12.5px] text-muted-foreground mt-1">
                  {active.source === "Website" || !active.source ? "Submitted" : `Logged (${active.source})`}{" "}
                  {active.ageHours < 48 ? `${active.ageHours}h ago` : `${Math.floor(active.ageHours / 24)} days ago`} &middot; {STATUS_LABELS[active.status] ?? active.status}
                </p>
                {(active.contactedAt || active.demoBookedAt || active.trialStartedAt || active.approvedAt) && (
                  <p className="text-[12.5px] text-muted-foreground mt-1">
                    {[
                      active.contactedAt && `Contacted ${formatDate(active.contactedAt)}`,
                      active.demoBookedAt && `demo ${formatDate(active.demoBookedAt)}`,
                      active.trialStartedAt && `trial ${formatDate(active.trialStartedAt)}`,
                      active.approvedAt && `approved ${formatDate(active.approvedAt)}`,
                    ].filter(Boolean).join(" · ")}
                  </p>
                )}

                <div className="flex flex-wrap items-center gap-2 mt-2 text-[12.5px] text-muted-foreground">
                  <span>Owner: {active.assigneeName ?? "unassigned"}</span>
                  <span>&middot;</span>
                  <span>{active.nextFollowUpAt ? `Follow up ${formatDate(active.nextFollowUpAt)}` : "No follow-up set"}</span>
                  <FollowUpBadge date={active.nextFollowUpAt} />
                  {trialLabel(active.institutionTrialEndsAt, false) && (
                    <>
                      <span>&middot;</span>
                      <span>{trialLabel(active.institutionTrialEndsAt, false)}</span>
                    </>
                  )}
                </div>

                <div className="border-t border-border mt-4 pt-3 text-[13.5px] leading-relaxed space-y-1">
                  <p><b>Contact:</b> {active.contactName}</p>
                  {active.contactEmail && <p><b>Email:</b> {active.contactEmail}</p>}
                  {active.contactPhone && <p><b>Phone:</b> {active.contactPhone}</p>}
                  {active.country && <p><b>Country:</b> {active.country}</p>}
                  {active.estimatedMemberCount && <p><b>Estimated members:</b> {active.estimatedMemberCount}</p>}
                  {active.organizationType && <p><b>Organization:</b> {active.organizationType}</p>}
                  {active.contactRole && <p><b>Contact role:</b> {active.contactRole}</p>}
                  {active.currentMemberManagement && <p><b>Current process:</b> {active.currentMemberManagement}</p>}
                  {active.dataImportStatus && <p><b>Data import:</b> {active.dataImportStatus}</p>}
                  {active.preferredContactChannel && <p><b>Preferred contact:</b> {active.preferredContactChannel}</p>}
                  {(active.preferredContactTime || active.timeZone) && (
                    <p><b>Contact timing:</b> {[active.preferredContactTime, active.timeZone].filter(Boolean).join(" · ")}</p>
                  )}
                  {active.website && <p><b>Website:</b> <a className="text-primary hover:underline" href={active.website} target="_blank" rel="noreferrer">{active.website}</a></p>}
                  {active.primaryGoals?.length > 0 && <p><b>Goals:</b> {active.primaryGoals.join(", ")}</p>}
                </div>

                <p>
                  <b>Institution Agreement:</b>{" "}
                  {active.agreementAcceptedAt
                    ? `accepted ${new Date(active.agreementAcceptedAt).toLocaleString()} by ${active.agreementAcceptedByName ?? "the contact"}${active.agreementAcceptedByTitle ? ` (${active.agreementAcceptedByTitle})` : ""}, version ${active.agreementVersion}${active.agreementAcceptedIp ? `, from ${active.agreementAcceptedIp}` : ""}`
                    : active.source && active.source !== "Website"
                      ? "not yet accepted (logged by platform staff)"
                      : "not recorded (request made before the agreement was introduced)"}
                </p>
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
                  <Button variant="outline" onClick={() => setEditOpen(true)}>Edit</Button>
                  <Button variant="outline" onClick={() => setNoteOpen(true)}>Add internal note</Button>
                  {(NEXT_STAGES[active.status] ?? []).map((stage) => (
                    <Button
                      key={stage}
                      variant="outline"
                      onClick={() => stageMutation.mutate({ id: active.id, status: stage })}
                      disabled={stageMutation.isPending}
                    >
                      {STAGE_ACTION_LABELS[stage]}
                    </Button>
                  ))}
                  {PRE_INSTITUTION.includes(active.status) && (
                    <Button variant="outline" onClick={() => router.push(`/institutions/new?fromLead=${active.id}&trial=1`)}>
                      Start trial
                    </Button>
                  )}
                  {active.status === "Trial" && active.approvedInstitutionId ? (
                    <>
                      <Link href={`/institutions/${active.approvedInstitutionId}`}>
                        <Button variant="outline">View institution</Button>
                      </Link>
                      <Button
                        onClick={() => stageMutation.mutate({ id: active.id, status: "Approved" })}
                        disabled={stageMutation.isPending}
                      >
                        Convert to full
                      </Button>
                    </>
                  ) : active.status !== "Approved" && active.status !== "Rejected" ? (
                    <Button onClick={() => router.push(`/institutions/new?fromLead=${active.id}`)}>
                      Approve
                    </Button>
                  ) : null}
                  {active.status !== "Approved" && active.status !== "Rejected" && (
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

      <Dialog open={logOpen} onOpenChange={setLogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Log a lead</DialogTitle>
          </DialogHeader>
          <p className="text-[13px] text-muted-foreground">
            For outreach, warm intros and referrals that didn&apos;t come through the website form. Logged leads count in the activation funnel.
          </p>
          <div className="grid gap-3.5 mt-2">
            <div className="grid gap-1.5">
              <Label htmlFor="lead-inst">Institution or association</Label>
              <Input id="lead-inst" value={newLead.institutionName} onChange={(e) => setNewLead({ ...newLead, institutionName: e.target.value })} />
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
              <div className="grid gap-1.5">
                <Label htmlFor="lead-contact">Contact name</Label>
                <Input id="lead-contact" value={newLead.contactName} onChange={(e) => setNewLead({ ...newLead, contactName: e.target.value })} />
              </div>
              <div className="grid gap-1.5">
                <Label htmlFor="lead-role">Role (optional)</Label>
                <Input id="lead-role" placeholder="e.g. General Secretary" value={newLead.contactRole} onChange={(e) => setNewLead({ ...newLead, contactRole: e.target.value })} />
              </div>
              <div className="grid gap-1.5">
                <Label htmlFor="lead-phone">Phone</Label>
                <Input id="lead-phone" value={newLead.contactPhone} onChange={(e) => setNewLead({ ...newLead, contactPhone: e.target.value })} />
              </div>
              <div className="grid gap-1.5">
                <Label htmlFor="lead-email">Email (optional)</Label>
                <Input id="lead-email" type="email" value={newLead.contactEmail} onChange={(e) => setNewLead({ ...newLead, contactEmail: e.target.value })} />
              </div>
              <div className="grid gap-1.5">
                <Label>Source</Label>
                <Select value={newLead.source} onValueChange={(v) => setNewLead({ ...newLead, source: v })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {SOURCE_OPTIONS.map((o) => <SelectItem key={o} value={o}>{o}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-1.5">
                <Label>Stage</Label>
                <Select value={newLead.status} onValueChange={(v) => setNewLead({ ...newLead, status: v as OnboardingLeadStatus })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {(["New", "Contacted", "DemoBooked"] as OnboardingLeadStatus[]).map((o) => (
                      <SelectItem key={o} value={o}>{STATUS_LABELS[o]}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="lead-followup">Next follow-up (optional)</Label>
              <Input id="lead-followup" type="date" value={newLead.nextFollowUpAt ?? ""} onChange={(e) => setNewLead({ ...newLead, nextFollowUpAt: e.target.value })} />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="lead-note">Note (optional)</Label>
              <Textarea id="lead-note" rows={3} placeholder="Who introduced them, what they use today, next follow-up…" value={newLead.note} onChange={(e) => setNewLead({ ...newLead, note: e.target.value })} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setLogOpen(false)}>Cancel</Button>
            <Button
              onClick={() => logMutation.mutate(newLead)}
              disabled={!newLead.institutionName.trim() || !newLead.contactName.trim() || logMutation.isPending}
            >
              {logMutation.isPending ? "Saving…" : "Log lead"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <LeadEditDialog lead={active} open={editOpen} onOpenChange={setEditOpen} />
      <LeadImportDialog open={importOpen} onOpenChange={setImportOpen} />

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
