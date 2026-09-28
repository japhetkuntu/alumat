"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { toast } from "sonner";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Button, ConfirmModal, EmptyState, LoadError, Skeleton, formatDate } from "@alumni/ui";
import { cancelWorkTarget, getWorkTarget, type WorkTask } from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";
import { useAuth } from "@/hooks/use-auth";
import { HealthBadge, TargetProgressBar, daysLeft } from "@/components/platform/work/work-common";
import { TaskList } from "@/components/platform/work/task-list";
import { TaskDialog } from "@/components/platform/work/task-dialog";
import { TaskDetailDialog } from "@/components/platform/work/task-detail-dialog";
import { TargetDialog } from "@/components/platform/work/target-dialog";

type Filter = "open" | "done" | "all";

export default function TargetDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const qc = useQueryClient();
  const { isSuperAdmin } = useAuth();
  const [filter, setFilter] = useState<Filter>("open");
  const [openTaskId, setOpenTaskId] = useState<string | null>(null);
  const [addingTask, setAddingTask] = useState(false);
  const [editing, setEditing] = useState(false);
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [newTarget, setNewTarget] = useState(false);

  const { data, isLoading, isError, refetch } = useQuery({ queryKey: ["work-target", id], queryFn: () => getWorkTarget(id) });

  const cancel = useMutation({
    mutationFn: () => cancelWorkTarget(id),
    onSuccess: () => {
      toast.success("Target cancelled");
      setConfirmCancel(false);
      qc.invalidateQueries({ queryKey: ["work-target", id] });
      qc.invalidateQueries({ queryKey: ["work-targets"] });
    },
    onError: (e) => { toast.error(handleApiError(e)); setConfirmCancel(false); },
  });

  const back = (
    <Link href="/activation?tab=targets" className="inline-flex items-center gap-1.5 text-[13px] text-muted-foreground hover:text-foreground mb-4">
      <ArrowLeft size={14} /> All targets
    </Link>
  );

  if (isLoading) return <div className="p-4 sm:p-7 max-w-[1000px]">{back}<Skeleton className="h-40 mb-4" /><Skeleton className="h-32" /></div>;
  if (isError || !data) return <div className="p-4 sm:p-7 max-w-[1000px]">{back}<LoadError title="Couldn't load this target" onRetry={() => void refetch()} /></div>;

  const { target, tasks } = data;
  const active = target.status === "Active";
  const left = daysLeft(target.dueDate);
  const shown: WorkTask[] = tasks.filter((t) => (filter === "open" ? t.status !== "Done" : filter === "done" ? t.status === "Done" : true));

  return (
    <div className="p-4 sm:p-7 max-w-[1000px]">
      {back}

      <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-3 mb-5">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2.5">
            <h1 className="text-[24px] font-bold leading-tight">{target.title}</h1>
            <HealthBadge health={target.progress.health} />
          </div>
          <p className="text-muted-foreground text-[13px] mt-1">
            {target.metricLabel} · owner <b className="text-foreground">{target.ownerName}</b> · {formatDate(target.startDate)} to {formatDate(target.dueDate)}
            {active && (left >= 0 ? ` · ${left} day${left === 1 ? "" : "s"} left` : "")}
          </p>
        </div>
        {isSuperAdmin && active && (
          <div className="flex gap-2 shrink-0">
            <Button variant="outline" onClick={() => setEditing(true)}>{target.metric === "Custom" ? "Update / edit" : "Edit"}</Button>
            <Button variant="outline" onClick={() => setConfirmCancel(true)}>Cancel target</Button>
          </div>
        )}
      </div>

      <div className="border border-border bg-card p-5 mb-6">
        <TargetProgressBar target={target} />
        {target.description && <p className="mt-4 text-[13.5px] leading-relaxed whitespace-pre-wrap text-muted-foreground">{target.description}</p>}
      </div>

      {!active && (
        <div className="border border-border bg-muted/40 p-4 mb-6 flex flex-wrap items-center justify-between gap-3">
          <p className="text-[13.5px]">
            {target.status === "Achieved" && <>This target was <b>achieved</b> on {formatDate(target.closedAt ?? target.dueDate)}. Its tasks are now read-only.</>}
            {target.status === "Missed" && <>This target <b>ended without reaching its goal</b>. Its tasks are now read-only.</>}
            {target.status === "Cancelled" && <>This target was <b>cancelled</b>. Its tasks are now read-only.</>}
          </p>
          {isSuperAdmin && <Button variant="outline" size="sm" onClick={() => setNewTarget(true)}>Start a new target</Button>}
        </div>
      )}

      <div className="flex flex-wrap items-center justify-between gap-3 mb-3">
        <div className="flex items-center gap-3">
          <h2 className="text-[16px] font-semibold">Tasks</h2>
          <div className="inline-flex border border-border">
            {(["open", "done", "all"] as Filter[]).map((f) => (
              <button
                key={f}
                onClick={() => setFilter(f)}
                className={`px-3 py-1.5 text-[12px] font-semibold capitalize transition-colors ${filter === f ? "bg-foreground text-background" : "hover:bg-muted"}`}
              >
                {f}
              </button>
            ))}
          </div>
        </div>
        {active && <Button size="sm" onClick={() => setAddingTask(true)}>Add task</Button>}
      </div>

      {shown.length === 0 ? (
        <EmptyState
          title={tasks.length === 0 ? "No tasks yet" : "Nothing here"}
          description={tasks.length === 0 ? "Break this target into tasks and give each one an owner and a date." : "Try another filter."}
          action={active && tasks.length === 0 ? <Button onClick={() => setAddingTask(true)}>Add the first task</Button> : undefined}
        />
      ) : (
        <TaskList tasks={shown} onOpen={(t) => setOpenTaskId(t.id)} />
      )}

      {openTaskId && <TaskDetailDialog taskId={openTaskId} onClose={() => setOpenTaskId(null)} />}
      {addingTask && <TaskDialog targetId={id} onClose={() => setAddingTask(false)} onSaved={() => setAddingTask(false)} />}
      {editing && <TargetDialog target={target} onClose={() => setEditing(false)} onSaved={() => setEditing(false)} />}
      {newTarget && <TargetDialog onClose={() => setNewTarget(false)} onSaved={(t) => { setNewTarget(false); router.push(`/activation/targets/${t.id}`); }} />}
      <ConfirmModal
        open={confirmCancel}
        title="Cancel this target?"
        message="It will stop counting as running and its tasks become read-only. The result so far is kept. This is recorded in the audit log."
        confirmLabel="Cancel target"
        isLoading={cancel.isPending}
        onConfirm={() => cancel.mutate()}
        onCancel={() => setConfirmCancel(false)}
      />
    </div>
  );
}
