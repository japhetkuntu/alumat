"use client";

import { useQuery } from "@tanstack/react-query";
import { Badge, Progress, formatDate } from "@alumni/ui";
import {
  getPlatformStaff,
  type TargetHealth, type WorkTarget, type WorkTask, type WorkTaskPriority, type WorkTaskStatus,
} from "@/lib/platform-api";

export const HEALTH_LABEL: Record<TargetHealth, string> = {
  Achieved: "Achieved", OnTrack: "On track", Behind: "Behind", Missed: "Missed", Cancelled: "Cancelled",
};
export const HEALTH_VARIANT: Record<TargetHealth, "success" | "info" | "warning" | "destructive" | "secondary"> = {
  Achieved: "success", OnTrack: "info", Behind: "warning", Missed: "destructive", Cancelled: "secondary",
};

export const TASK_STATUS_LABEL: Record<WorkTaskStatus, string> = { Todo: "To do", InProgress: "In progress", Blocked: "Blocked", Done: "Done" };
export const TASK_STATUS_VARIANT: Record<WorkTaskStatus, "secondary" | "info" | "destructive" | "success"> = {
  Todo: "secondary", InProgress: "info", Blocked: "destructive", Done: "success",
};
export const TASK_STATUSES: WorkTaskStatus[] = ["Todo", "InProgress", "Blocked", "Done"];
export const PRIORITY_LABEL: Record<WorkTaskPriority, string> = { Low: "Low", Normal: "Normal", High: "High" };

/** Plain number for counts, "GHS 1,200.00" for money. */
export function formatMetricValue(metric: WorkTarget["metric"], value: number) {
  return metric === "PaymentVolume"
    ? `GHS ${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
    : value.toLocaleString(undefined, { maximumFractionDigits: 2 });
}

export function daysLeft(dueDate: string) {
  const ms = new Date(dueDate).setHours(0, 0, 0, 0) - new Date().setHours(0, 0, 0, 0);
  return Math.round(ms / 86400000);
}

export function dueLabel(dueDate?: string | null) {
  if (!dueDate) return "No date";
  const d = daysLeft(dueDate);
  if (d < 0) return `${Math.abs(d)} day${Math.abs(d) === 1 ? "" : "s"} overdue`;
  if (d === 0) return "Due today";
  if (d === 1) return "Due tomorrow";
  return `Due ${formatDate(dueDate)}`;
}

export function HealthBadge({ health }: { health: TargetHealth }) {
  return <Badge variant={HEALTH_VARIANT[health]}>{HEALTH_LABEL[health]}</Badge>;
}

export function TaskStatusBadge({ status }: { status: WorkTaskStatus }) {
  return <Badge variant={TASK_STATUS_VARIANT[status]}>{TASK_STATUS_LABEL[status]}</Badge>;
}

/** Progress toward the goal, with where it should be by now so "behind" is easy to see. */
export function TargetProgressBar({ target }: { target: WorkTarget }) {
  const p = target.progress;
  const ended = target.status !== "Active";
  return (
    <div>
      <div className="flex items-baseline justify-between gap-3 mb-1.5">
        <p className="text-[13px]">
          <span className="text-[20px] font-bold tabular-nums">{formatMetricValue(target.metric, p.current)}</span>
          <span className="text-muted-foreground"> of {formatMetricValue(target.metric, p.goal)}</span>
        </p>
        <p className="text-[12px] text-muted-foreground tabular-nums">{p.percent}%</p>
      </div>
      <Progress value={p.percent} />
      {!ended && (
        <p className="text-[11.5px] text-muted-foreground mt-1.5">
          Should be at {formatMetricValue(target.metric, p.expected)} by today to finish on time.
        </p>
      )}
    </div>
  );
}

/** Active platform staff, for the owner and assignee pickers. Only a Super Admin can read the staff list; others get an empty list. */
export function useStaffOptions(enabled: boolean) {
  const { data } = useQuery({
    queryKey: ["work-staff-options"],
    queryFn: () => getPlatformStaff({ pageSize: 100 }),
    enabled,
    staleTime: 5 * 60 * 1000,
  });
  return (data?.results ?? []).filter((s) => !s.isDisabled).map((s) => ({ value: s.id, label: `${s.name} (${s.role})` }));
}

export function TaskMeta({ task }: { task: WorkTask }) {
  return (
    <span className="text-[12px] text-muted-foreground">
      {task.assigneeName}
      {" · "}
      <span className={task.isOverdue ? "text-destructive font-semibold" : undefined}>{dueLabel(task.dueDate)}</span>
    </span>
  );
}
