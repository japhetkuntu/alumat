"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  Button,
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  Input,
  Label,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@alumni/ui";
import { handleApiError } from "@/lib/api-client";
import {
  getLeadAssignees,
  updateOnboardingLead,
  type OnboardingLead,
  type UpdateOnboardingLeadRequest,
} from "@/lib/platform-api";

export const LEAD_SOURCE_OPTIONS = ["Warm intro", "Outreach", "Referral", "Event", "Import", "Website", "Website walkthrough", "Founding 20", "Other"];
const UNASSIGNED = "__none";

function toForm(lead: OnboardingLead): UpdateOnboardingLeadRequest {
  return {
    institutionName: lead.institutionName,
    contactName: lead.contactName,
    contactEmail: lead.contactEmail ?? "",
    contactPhone: lead.contactPhone ?? "",
    contactRole: lead.contactRole ?? "",
    source: lead.source ?? "Other",
    assigneeStaffId: lead.assigneeStaffId ?? null,
    nextFollowUpAt: lead.nextFollowUpAt?.slice(0, 10) ?? "",
  };
}

/** Edit a lead's contact details, owner and next follow-up date. */
export function LeadEditDialog({ lead, open, onOpenChange }: { lead: OnboardingLead | undefined; open: boolean; onOpenChange: (open: boolean) => void }) {
  if (!lead) return null;
  // Keyed so each opening starts from the lead's current values.
  return <LeadEditDialogBody key={`${lead.id}:${open}`} lead={lead} open={open} onOpenChange={onOpenChange} />;
}

function LeadEditDialogBody({ lead, open, onOpenChange }: { lead: OnboardingLead; open: boolean; onOpenChange: (open: boolean) => void }) {
  const queryClient = useQueryClient();
  const [form, setForm] = useState<UpdateOnboardingLeadRequest>(() => toForm(lead));
  const assignees = useQuery({ queryKey: ["lead-assignees"], queryFn: getLeadAssignees, enabled: open, staleTime: 5 * 60_000 });

  const mutation = useMutation({
    mutationFn: (req: UpdateOnboardingLeadRequest) =>
      updateOnboardingLead(lead.id, {
        ...req,
        contactEmail: req.contactEmail?.trim() || null,
        contactPhone: req.contactPhone?.trim() || null,
        contactRole: req.contactRole?.trim() || null,
        nextFollowUpAt: req.nextFollowUpAt || null,
      }),
    onSuccess: () => {
      toast.success("Lead updated");
      queryClient.invalidateQueries({ queryKey: ["onboarding-leads"] });
      onOpenChange(false);
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const set = (patch: Partial<UpdateOnboardingLeadRequest>) => setForm({ ...form, ...patch });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit lead</DialogTitle>
        </DialogHeader>
        <div className="grid gap-3.5 mt-2">
          <div className="grid gap-1.5">
            <Label htmlFor="edit-inst">Institution or association</Label>
            <Input id="edit-inst" value={form.institutionName} onChange={(e) => set({ institutionName: e.target.value })} />
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
            <div className="grid gap-1.5">
              <Label htmlFor="edit-contact">Contact name</Label>
              <Input id="edit-contact" value={form.contactName} onChange={(e) => set({ contactName: e.target.value })} />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="edit-role">Role</Label>
              <Input id="edit-role" value={form.contactRole ?? ""} onChange={(e) => set({ contactRole: e.target.value })} />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="edit-phone">Phone</Label>
              <Input id="edit-phone" value={form.contactPhone ?? ""} onChange={(e) => set({ contactPhone: e.target.value })} />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="edit-email">Email</Label>
              <Input id="edit-email" type="email" value={form.contactEmail ?? ""} onChange={(e) => set({ contactEmail: e.target.value })} />
            </div>
            <div className="grid gap-1.5">
              <Label>Owner</Label>
              <Select value={form.assigneeStaffId ?? UNASSIGNED} onValueChange={(v) => set({ assigneeStaffId: v === UNASSIGNED ? null : v })}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={UNASSIGNED}>Unassigned</SelectItem>
                  {(assignees.data ?? []).map((a) => (
                    <SelectItem key={a.id} value={a.id}>{a.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="edit-followup">Next follow-up</Label>
              <Input id="edit-followup" type="date" value={form.nextFollowUpAt ?? ""} onChange={(e) => set({ nextFollowUpAt: e.target.value })} />
            </div>
            <div className="grid gap-1.5">
              <Label>Source</Label>
              <Select value={form.source ?? "Other"} onValueChange={(v) => set({ source: v })}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {LEAD_SOURCE_OPTIONS.map((o) => <SelectItem key={o} value={o}>{o}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          </div>
          <p className="text-[12.5px] text-muted-foreground">The owner gets an in-app reminder on the follow-up date. With no owner, every SuperAdmin and Sales staffer does.</p>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button
            onClick={() => mutation.mutate(form)}
            disabled={!form.institutionName.trim() || !form.contactName.trim() || mutation.isPending}
          >
            {mutation.isPending ? "Saving…" : "Save changes"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
