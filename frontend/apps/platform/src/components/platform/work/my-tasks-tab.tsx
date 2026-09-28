"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, EmptyState, LoadError, Skeleton } from "@alumni/ui";
import { getWorkTasks, type WorkTask } from "@/lib/platform-api";
import { TaskList } from "./task-list";
import { TaskDetailDialog } from "./task-detail-dialog";
import { TaskDialog } from "./task-dialog";

type Filter = "open" | "all";

/** The signed-in person's own tasks, most urgent first: overdue, then by due date, then done. */
export function MyTasksTab() {
  const [filter, setFilter] = useState<Filter>("open");
  const [openId, setOpenId] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const { data, isLoading, isError, refetch } = useQuery({ queryKey: ["work-tasks", "mine"], queryFn: () => getWorkTasks({ mine: true }) });

  const all = data ?? [];
  const open = all.filter((t) => t.status !== "Done");
  const overdue = open.filter((t) => t.isOverdue);
  const shown: WorkTask[] = filter === "open" ? open : all;

  return (
    <div className="p-4 sm:p-7 max-w-[1100px]">
      <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-3 mb-5">
        <div>
          <h2 className="text-[20px] font-bold">My tasks</h2>
          <p className="text-muted-foreground text-[13px] mt-1">
            {isLoading ? "Loading…" : open.length === 0 ? "Nothing open right now." : `${open.length} open${overdue.length ? `, ${overdue.length} overdue` : ""}.`}
          </p>
        </div>
        <div className="flex gap-2">
          <div className="inline-flex border border-border">
            {(["open", "all"] as Filter[]).map((f) => (
              <button
                key={f}
                onClick={() => setFilter(f)}
                className={`px-3.5 py-2 text-[12.5px] font-semibold transition-colors ${filter === f ? "bg-foreground text-background" : "hover:bg-muted"}`}
              >
                {f === "open" ? "Open" : "All"}
              </button>
            ))}
          </div>
          <Button onClick={() => setCreating(true)}>New task</Button>
        </div>
      </div>

      {isLoading ? (
        <div className="space-y-3">{[0, 1, 2].map((i) => <Skeleton key={i} className="h-14" />)}</div>
      ) : isError ? (
        <LoadError title="Couldn't load your tasks" onRetry={() => void refetch()} />
      ) : shown.length === 0 ? (
        <EmptyState
          title={filter === "open" ? "You're all caught up" : "No tasks yet"}
          description="Tasks assigned to you show up here, under the target they belong to. You can also add your own."
        />
      ) : (
        <TaskList tasks={shown} onOpen={(t) => setOpenId(t.id)} showTarget />
      )}

      {openId && <TaskDetailDialog taskId={openId} onClose={() => setOpenId(null)} />}
      {creating && <TaskDialog onClose={() => setCreating(false)} onSaved={() => setCreating(false)} />}
    </div>
  );
}
