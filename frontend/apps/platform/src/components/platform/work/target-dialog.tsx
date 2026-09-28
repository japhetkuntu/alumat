"use client";

import { useState } from "react";
import { toast } from "sonner";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button, Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, FormError, FormSelect, Input, Label, Textarea } from "@alumni/ui";
import { createWorkTarget, updateWorkTarget, TARGET_METRICS, type TargetMetric, type WorkTarget } from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";
import { useStaffOptions } from "./work-common";

function daysFromToday(days: number) {
  return new Date(Date.now() + days * 86400000).toISOString().slice(0, 10);
}

/** Create a target, or edit an active one (the metric and start are fixed once set, so progress stays honest). */
export function TargetDialog({ target, onClose, onSaved }: { target?: WorkTarget; onClose: () => void; onSaved: (t: WorkTarget) => void }) {
  const qc = useQueryClient();
  const staff = useStaffOptions(true);
  const [title, setTitle] = useState(target?.title ?? "");
  const [description, setDescription] = useState(target?.description ?? "");
  const [metric, setMetric] = useState<TargetMetric>(target?.metric ?? "LiveInstitutions");
  const [goal, setGoal] = useState(target ? String(target.goalValue) : "");
  const [dueDate, setDueDate] = useState(target ? target.dueDate.slice(0, 10) : daysFromToday(30));
  const [ownerId, setOwnerId] = useState(target?.ownerId ?? "");
  const [manual, setManual] = useState(target?.manualValue != null ? String(target.manualValue) : "");
  const [error, setError] = useState<string | null>(null);

  const meta = TARGET_METRICS.find((m) => m.value === metric)!;
  const editing = !!target;

  const save = useMutation({
    mutationFn: () => {
      const base = {
        title: title.trim(),
        description: description.trim() || undefined,
        goalValue: Number(goal),
        dueDate,
        ownerId,
        manualValue: metric === "Custom" && manual !== "" ? Number(manual) : undefined,
      };
      return editing ? updateWorkTarget(target!.id, base) : createWorkTarget({ ...base, metric });
    },
    onSuccess: (t) => {
      toast.success(editing ? "Target updated" : "Target created");
      qc.invalidateQueries({ queryKey: ["work-targets"] });
      qc.invalidateQueries({ queryKey: ["work-target", t.id] });
      onSaved(t);
    },
    onError: (e) => setError(handleApiError(e)),
  });

  const valid = title.trim().length > 0 && Number(goal) > 0 && !!dueDate && !!ownerId;

  return (
    <Dialog open onOpenChange={(v) => { if (!v) onClose(); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{editing ? "Edit target" : "New target"}</DialogTitle>
          <DialogDescription>
            A target has one owner and an end date. It never repeats: when it ends, you create a new one.
          </DialogDescription>
        </DialogHeader>

        <div className="mt-4 space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="t-title">What are we aiming for?</Label>
            <Input id="t-title" value={title} maxLength={160} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. 20 live institutions by year end" />
          </div>

          <div className="space-y-1.5">
            <Label>Measured by</Label>
            <FormSelect className="w-full sm:w-full" value={metric} onValueChange={(v) => setMetric(v as TargetMetric)} disabled={editing} options={TARGET_METRICS.map((m) => ({ value: m.value, label: m.label }))} />
            <p className="text-[12px] text-muted-foreground">{meta.hint}</p>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="t-goal">Goal</Label>
              <Input id="t-goal" type="number" min="1" step="any" value={goal} onChange={(e) => setGoal(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="t-due">Ends on</Label>
              <Input id="t-due" type="date" min={daysFromToday(1)} value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
            </div>
          </div>

          {metric === "Custom" && (
            <div className="space-y-1.5">
              <Label htmlFor="t-manual">{editing ? "Current figure" : "Starting figure"}</Label>
              <Input id="t-manual" type="number" step="any" value={manual} onChange={(e) => setManual(e.target.value)} placeholder="0" />
            </div>
          )}

          <div className="space-y-1.5">
            <Label>Owner</Label>
            <FormSelect className="w-full sm:w-full" value={ownerId} onValueChange={setOwnerId} placeholder="Who is accountable for this?" options={staff} />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="t-desc">Notes <span className="font-normal text-muted-foreground">(optional)</span></Label>
            <Textarea id="t-desc" rows={3} maxLength={2000} value={description} onChange={(e) => setDescription(e.target.value)} className="resize-none" />
          </div>
        </div>

        <FormError message={error} className="mt-3" />
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button onClick={() => { setError(null); save.mutate(); }} disabled={!valid} isLoading={save.isPending} loadingText="Saving…">
            {editing ? "Save changes" : "Create target"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
