"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@alumni/ui";
import { FormError } from "@alumni/ui";
import { addSupportCaseNote, createSupportCase, getSupportCases, updateSupportCaseStatus } from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";

export default function SupportPage() {
  const queryClient = useQueryClient();
  const { data: supportCases = [] } = useQuery({ queryKey: ["support-cases"], queryFn: () => getSupportCases() });
  const [activeId, setActiveId] = useState<string | undefined>(undefined);
  const active = supportCases.find((c) => c.id === activeId) ?? supportCases[0];

  const [createOpen, setCreateOpen] = useState(false);
  const [createForm, setCreateForm] = useState({ subject: "", severity: "Medium", requester: "", requesterEmail: "", message: "" });
  const [createError, setCreateError] = useState<string | null>(null);

  const [noteOpen, setNoteOpen] = useState(false);
  const [note, setNote] = useState("");

  const createMutation = useMutation({
    mutationFn: () => createSupportCase(createForm),
    onSuccess: (created) => {
      toast.success("Case created");
      queryClient.invalidateQueries({ queryKey: ["support-cases"] });
      setActiveId(created.id);
      setCreateOpen(false);
      setCreateForm({ subject: "", severity: "Medium", requester: "", requesterEmail: "", message: "" });
      setCreateError(null);
    },
    onError: (e) => {
      const msg = handleApiError(e);
      setCreateError(msg);
      toast.error(msg);
    },
  });

  const noteMutation = useMutation({
    mutationFn: (vars: { id: string; note: string }) => addSupportCaseNote(vars.id, vars.note),
    onSuccess: () => {
      toast.success("Note added");
      queryClient.invalidateQueries({ queryKey: ["support-cases"] });
      setNoteOpen(false);
      setNote("");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const resolveMutation = useMutation({
    mutationFn: (id: string) => updateSupportCaseStatus(id, "Resolved"),
    onSuccess: () => {
      toast.success("Case resolved");
      queryClient.invalidateQueries({ queryKey: ["support-cases"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  return (
    <div className="p-7 max-w-[1500px]">
      <div className="flex items-end justify-between mb-6">
        <div>
          <h1 className="text-[24px] font-bold">Support</h1>
          <p className="text-muted-foreground text-[13px] mt-1">Triage institution issues from report to accountable resolution.</p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>Create case</Button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-[1fr_1.2fr] gap-4">
        <Card>
          <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">Open cases</p></div>
          <CardContent className="p-0">
            {supportCases.length === 0 && <p className="px-5 py-6 text-[13px] text-muted-foreground">No support cases yet.</p>}
            {supportCases.map((c) => (
              <button
                key={c.id}
                onClick={() => setActiveId(c.id)}
                className={`w-full text-left px-5 py-3.5 border-b border-border last:border-0 transition-colors ${
                  c.id === (activeId ?? supportCases[0]?.id) ? "bg-primary/5 border-l-4 border-l-primary" : "hover:bg-muted/40"
                }`}
              >
                <div className="flex justify-between items-start gap-2">
                  <p className="font-semibold text-[13.5px]">{c.subject}</p>
                  <Badge variant={c.severity === "High" ? "destructive" : c.severity === "Medium" ? "warning" : "neutral"}>{c.severity} &middot; {c.ageHours}h</Badge>
                </div>
                <p className="text-[12.5px] text-muted-foreground mt-1">{c.institutionName ?? "No institution"} &middot; {c.assigneeName ? `Assigned to ${c.assigneeName}` : "Unassigned"} &middot; {c.status}</p>
              </button>
            ))}
          </CardContent>
        </Card>

        {active && (
          <Card>
            <CardContent className="p-5">
              <h2 className="text-[17px] font-semibold">{active.subject}</h2>
              <p className="text-[12.5px] text-muted-foreground mt-1">{active.institutionName ?? "No institution"} &middot; {active.severity} severity</p>
              <div className="border-t border-border mt-4 pt-3 text-[13.5px] leading-relaxed">
                <b>Requester: {active.requester}</b><br />{active.message}
              </div>
              {active.internalNote && (
                <div className="border-t border-border mt-3 pt-3 text-[13.5px] leading-relaxed">
                  <b>Internal note</b><br />{active.internalNote}
                </div>
              )}
              <div className="flex gap-2 mt-5">
                <Button variant="outline" onClick={() => setNoteOpen(true)}>Add internal note</Button>
                <Button
                  onClick={() => resolveMutation.mutate(active.id)}
                  disabled={active.status === "Resolved" || resolveMutation.isPending}
                >
                  {active.status === "Resolved" ? "Resolved" : "Resolve case"}
                </Button>
              </div>
            </CardContent>
          </Card>
        )}
      </div>

      <Dialog open={createOpen} onOpenChange={(o) => { setCreateOpen(o); if (!o) setCreateError(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create support case</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 mt-2">
            <div className="space-y-1.5">
              <Label>Subject</Label>
              <Input value={createForm.subject} onChange={(e) => setCreateForm((f) => ({ ...f, subject: e.target.value }))} />
            </div>
            <div className="space-y-1.5">
              <Label>Severity</Label>
              <select
                className="w-full h-9 rounded-md border border-border bg-background px-3 text-[13px]"
                value={createForm.severity}
                onChange={(e) => setCreateForm((f) => ({ ...f, severity: e.target.value }))}
              >
                <option value="High">High</option>
                <option value="Medium">Medium</option>
                <option value="Low">Low</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <Label>Requester name</Label>
              <Input value={createForm.requester} onChange={(e) => setCreateForm((f) => ({ ...f, requester: e.target.value }))} />
            </div>
            <div className="space-y-1.5">
              <Label>Requester email</Label>
              <Input type="email" value={createForm.requesterEmail} onChange={(e) => setCreateForm((f) => ({ ...f, requesterEmail: e.target.value }))} />
            </div>
            <div className="space-y-1.5">
              <Label>Message</Label>
              <Textarea rows={4} value={createForm.message} onChange={(e) => setCreateForm((f) => ({ ...f, message: e.target.value }))} />
            </div>
            <FormError message={createError} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>Cancel</Button>
            <Button onClick={() => createMutation.mutate()} disabled={createMutation.isPending || !createForm.subject || !createForm.requester || !createForm.message}>
              {createMutation.isPending ? "Creating…" : "Create case"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

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
    </div>
  );
}
