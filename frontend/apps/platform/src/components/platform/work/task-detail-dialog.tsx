"use client";

import { useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Badge, Button, ConfirmModal, Dialog, DialogContent, DialogHeader, DialogTitle, FormError, Input, LoadError, Textarea, formatDateTime,
} from "@alumni/ui";
import {
  addWorkTaskNote, deleteWorkTask, getWorkTask, updateWorkTaskStatus,
  type WorkTask, type WorkTaskStatus,
} from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";
import { PRIORITY_LABEL, TASK_STATUSES, TASK_STATUS_LABEL, TaskStatusBadge, dueLabel } from "./work-common";
import { TaskDialog } from "./task-dialog";

/** One task: change its status, read and add notes, and (for the person who set it, or a Super Admin) edit or delete it. */
export function TaskDetailDialog({ taskId, onClose }: { taskId: string; onClose: () => void }) {
  const qc = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [blockedReason, setBlockedReason] = useState("");
  const [askBlockReason, setAskBlockReason] = useState(false);
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);

  const { data, isLoading, isError, refetch } = useQuery({ queryKey: ["work-task", taskId], queryFn: () => getWorkTask(taskId) });
  const refresh = () => {
    qc.invalidateQueries({ queryKey: ["work-task", taskId] });
    qc.invalidateQueries({ queryKey: ["work-tasks"] });
    qc.invalidateQueries({ queryKey: ["work-target"] });
    qc.invalidateQueries({ queryKey: ["work-targets"] });
  };

  const setStatus = useMutation({
    mutationFn: (v: { status: WorkTaskStatus; blockedReason?: string }) => updateWorkTaskStatus(taskId, v),
    onSuccess: () => { setAskBlockReason(false); setBlockedReason(""); setError(null); refresh(); },
    onError: (e) => setError(handleApiError(e)),
  });
  const addNote = useMutation({
    mutationFn: () => addWorkTaskNote(taskId, note.trim()),
    onSuccess: () => { setNote(""); setError(null); refresh(); },
    onError: (e) => setError(handleApiError(e)),
  });
  const remove = useMutation({
    mutationFn: () => deleteWorkTask(taskId),
    onSuccess: () => { toast.success("Task deleted"); refresh(); onClose(); },
    onError: (e) => { toast.error(handleApiError(e)); setConfirmDelete(false); },
  });

  const task: WorkTask | undefined = data?.task;

  function pick(status: WorkTaskStatus) {
    if (!task || status === task.status) return;
    if (status === "Blocked") { setAskBlockReason(true); return; }
    setStatus.mutate({ status });
  }

  return (
    <>
      <Dialog open onOpenChange={(v) => { if (!v) onClose(); }}>
        <DialogContent className="max-w-xl">
          {isLoading && <p className="py-10 text-center text-[13px] text-muted-foreground">Loading…</p>}
          {isError && <LoadError title="Couldn't load this task" onRetry={() => void refetch()} />}
          {task && (
            <div className="space-y-4">
              <DialogHeader>
                <DialogTitle className="pr-6">{task.title}</DialogTitle>
              </DialogHeader>

              <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5 text-[12.5px] text-muted-foreground">
                <TaskStatusBadge status={task.status} />
                {task.priority === "High" && <Badge variant="destructive">{PRIORITY_LABEL.High} priority</Badge>}
                <span>{task.assigneeName}</span>
                <span className={task.isOverdue ? "text-destructive font-semibold" : undefined}>{dueLabel(task.dueDate)}</span>
              </div>

              <p className="text-[12.5px] text-muted-foreground">
                Under <Link href={`/activation/targets/${task.targetId}`} className="underline underline-offset-2 hover:text-foreground">{task.targetTitle}</Link>
                {" · "}set by {task.createdByName}
                {task.institutionName && <> · about <b className="text-foreground">{task.institutionName}</b></>}
                {task.leadName && <> · onboarding request <b className="text-foreground">{task.leadName}</b></>}
              </p>

              {task.description && <p className="text-[13.5px] leading-relaxed whitespace-pre-wrap">{task.description}</p>}
              {task.status === "Blocked" && task.blockedReason && (
                <p className="border-l-2 border-destructive pl-3 text-[13px]"><b>Blocked:</b> {task.blockedReason}</p>
              )}

              {task.canUpdateStatus && (
                <div>
                  <p className="text-[12px] font-semibold mb-1.5">Status</p>
                  <div className="flex flex-wrap gap-1.5">
                    {TASK_STATUSES.map((s) => (
                      <Button key={s} size="sm" variant={task.status === s ? "default" : "outline"} disabled={setStatus.isPending} onClick={() => pick(s)}>
                        {TASK_STATUS_LABEL[s]}
                      </Button>
                    ))}
                  </div>
                  {askBlockReason && (
                    <div className="mt-2.5 flex gap-2">
                      <Input value={blockedReason} maxLength={500} onChange={(e) => setBlockedReason(e.target.value)} placeholder="What is blocking this?" autoFocus />
                      <Button disabled={!blockedReason.trim() || setStatus.isPending} onClick={() => setStatus.mutate({ status: "Blocked", blockedReason })}>Mark blocked</Button>
                    </div>
                  )}
                </div>
              )}

              <div>
                <p className="text-[12px] font-semibold mb-1.5">Notes</p>
                {data!.notes.length === 0 ? (
                  <p className="text-[13px] text-muted-foreground">No notes yet.</p>
                ) : (
                  <ul className="space-y-2.5 max-h-56 overflow-y-auto">
                    {data!.notes.map((n) => (
                      <li key={n.id} className="text-[13px]">
                        <p className="text-[11.5px] text-muted-foreground">{n.authorName} · {formatDateTime(n.createdAt)}</p>
                        <p className="whitespace-pre-wrap">{n.text}</p>
                      </li>
                    ))}
                  </ul>
                )}
                {task.canUpdateStatus && (
                  <div className="mt-2.5 flex gap-2 items-start">
                    <Textarea rows={2} value={note} maxLength={2000} onChange={(e) => setNote(e.target.value)} placeholder="Add a note" className="resize-none" />
                    <Button variant="outline" disabled={!note.trim() || addNote.isPending} onClick={() => addNote.mutate()}>Add</Button>
                  </div>
                )}
              </div>

              <FormError message={error} />

              {task.canEdit && (
                <div className="flex justify-between border-t border-border pt-3">
                  <Button variant="ghost" size="sm" className="text-destructive" onClick={() => setConfirmDelete(true)}>Delete task</Button>
                  <Button variant="outline" size="sm" onClick={() => setEditing(true)}>Edit details</Button>
                </div>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>

      {editing && task && <TaskDialog task={task} onClose={() => setEditing(false)} onSaved={() => setEditing(false)} />}
      <ConfirmModal
        open={confirmDelete}
        title="Delete this task?"
        message="It and its notes will be removed. This is recorded in the audit log."
        confirmLabel="Delete"
        isLoading={remove.isPending}
        onConfirm={() => remove.mutate()}
        onCancel={() => setConfirmDelete(false)}
      />
    </>
  );
}
