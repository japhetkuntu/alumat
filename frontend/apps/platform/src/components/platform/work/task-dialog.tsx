"use client";

import { useState } from "react";
import { toast } from "sonner";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, FormError, FormSelect, Input, Label, Textarea } from "@alumni/ui";
import {
  createWorkTask, getInstitutions, getWorkTargets, updateWorkTask,
  type WorkTask, type WorkTaskPriority,
} from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";
import { PRIORITY_LABEL, useStaffOptions } from "./work-common";

export interface TaskPrefill {
  title?: string;
  description?: string;
  institutionId?: string;
  leadId?: string;
  leadName?: string;
}

const NONE = "__none__";

/**
 * Create a task under a target, or edit one. Anyone on the team can create a task for themselves; only a Super Admin
 * can pick someone else. A task can point at an institution or an onboarding request, and can start pre-filled from
 * the scorecard or a lead.
 */
export function TaskDialog({
  task, targetId, prefill, onClose, onSaved,
}: { task?: WorkTask; targetId?: string; prefill?: TaskPrefill; onClose: () => void; onSaved: (t: WorkTask) => void }) {
  const qc = useQueryClient();
  const { user, isSuperAdmin } = useAuth();
  const editing = !!task;
  const staff = useStaffOptions(isSuperAdmin);

  const { data: targets } = useQuery({ queryKey: ["work-targets", "Active"], queryFn: () => getWorkTargets("Active"), enabled: !editing && !targetId });
  const { data: institutions } = useQuery({ queryKey: ["work-institution-options"], queryFn: () => getInstitutions({ pageSize: 100 }), staleTime: 5 * 60 * 1000 });

  const [pickedTarget, setPickedTarget] = useState(targetId ?? "");
  const [title, setTitle] = useState(task?.title ?? prefill?.title ?? "");
  const [description, setDescription] = useState(task?.description ?? prefill?.description ?? "");
  const [assigneeId, setAssigneeId] = useState(task?.assigneeId ?? user?.id ?? "");
  const [dueDate, setDueDate] = useState(task?.dueDate ? task.dueDate.slice(0, 10) : "");
  const [priority, setPriority] = useState<WorkTaskPriority>(task?.priority ?? "Normal");
  const [institutionId, setInstitutionId] = useState(task?.institutionId ?? prefill?.institutionId ?? NONE);
  const [leadId, setLeadId] = useState<string | undefined>(task?.leadId ?? prefill?.leadId ?? undefined);
  const leadName = task?.leadName ?? prefill?.leadName;
  const [error, setError] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: () => {
      const body = {
        title: title.trim(),
        description: description.trim() || undefined,
        assigneeId: isSuperAdmin ? assigneeId : undefined,
        dueDate: dueDate || null,
        priority,
        institutionId: institutionId === NONE ? null : institutionId,
        leadId: leadId ?? null,
      };
      return editing ? updateWorkTask(task!.id, body) : createWorkTask({ ...body, targetId: pickedTarget });
    },
    onSuccess: (t) => {
      toast.success(editing ? "Task updated" : "Task created");
      qc.invalidateQueries({ queryKey: ["work-target"] });
      qc.invalidateQueries({ queryKey: ["work-targets"] });
      qc.invalidateQueries({ queryKey: ["work-tasks"] });
      onSaved(t);
    },
    onError: (e) => setError(handleApiError(e)),
  });

  const valid = title.trim().length > 0 && (editing || !!pickedTarget) && !!assigneeId;
  const institutionOptions = [{ value: NONE, label: "No institution" }, ...(institutions?.results ?? []).map((i) => ({ value: i.id, label: i.name }))];

  return (
    <Dialog open onOpenChange={(v) => { if (!v) onClose(); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{editing ? "Edit task" : "New task"}</DialogTitle>
          <DialogDescription>
            {isSuperAdmin ? "Assign it to anyone on the team." : "It will be assigned to you."}
          </DialogDescription>
        </DialogHeader>

        <div className="mt-4 space-y-4">
          {!editing && !targetId && (
            <div className="space-y-1.5">
              <Label>Under which target?</Label>
              <FormSelect className="w-full sm:w-full" value={pickedTarget} onValueChange={setPickedTarget} placeholder="Choose a target" options={(targets ?? []).map((t) => ({ value: t.id, label: t.title }))} />
              {targets && targets.length === 0 && <p className="text-[12px] text-muted-foreground">There is no active target yet. A Super Admin needs to create one first.</p>}
            </div>
          )}

          <div className="space-y-1.5">
            <Label htmlFor="task-title">What needs doing?</Label>
            <Input id="task-title" value={title} maxLength={200} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. Call the registrar about member import" />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="task-desc">Details <span className="font-normal text-muted-foreground">(optional)</span></Label>
            <Textarea id="task-desc" rows={3} maxLength={4000} value={description} onChange={(e) => setDescription(e.target.value)} className="resize-none" />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            {isSuperAdmin && (
              <div className="space-y-1.5">
                <Label>Assign to</Label>
                <FormSelect className="w-full sm:w-full" value={assigneeId} onValueChange={setAssigneeId} placeholder="Choose someone" options={staff} />
              </div>
            )}
            <div className="space-y-1.5">
              <Label htmlFor="task-due">Due date <span className="font-normal text-muted-foreground">(optional)</span></Label>
              <Input id="task-due" type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label>Priority</Label>
              <FormSelect className="w-full sm:w-full" value={priority} onValueChange={(v) => setPriority(v as WorkTaskPriority)} options={(Object.keys(PRIORITY_LABEL) as WorkTaskPriority[]).map((p) => ({ value: p, label: PRIORITY_LABEL[p] }))} />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label>About an institution <span className="font-normal text-muted-foreground">(optional)</span></Label>
            <FormSelect className="w-full sm:w-full" value={institutionId} onValueChange={setInstitutionId} options={institutionOptions} />
          </div>

          {leadId && (
            <div className="flex items-center justify-between gap-3 border border-border px-3 py-2 text-[13px]">
              <span>Onboarding request: <b>{leadName ?? "linked"}</b></span>
              <button type="button" onClick={() => setLeadId(undefined)} className="text-[12px] text-muted-foreground hover:text-foreground underline underline-offset-2">Remove</button>
            </div>
          )}
        </div>

        <FormError message={error} className="mt-3" />
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button onClick={() => { setError(null); save.mutate(); }} disabled={!valid} isLoading={save.isPending} loadingText="Saving…">
            {editing ? "Save changes" : "Create task"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
