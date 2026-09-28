"use client";

import { Badge, cn } from "@alumni/ui";
import type { WorkTask } from "@/lib/platform-api";
import { TaskMeta, TaskStatusBadge } from "./work-common";

/** A plain list of tasks: title, status, who and when. Clicking a row opens the task. */
export function TaskList({ tasks, onOpen, showTarget }: { tasks: WorkTask[]; onOpen: (t: WorkTask) => void; showTarget?: boolean }) {
  return (
    <ul className="divide-y divide-border border-y border-border">
      {tasks.map((t) => (
        <li key={t.id}>
          <button type="button" onClick={() => onOpen(t)} className="flex w-full items-start justify-between gap-4 px-1 py-3 text-left hover:bg-muted/40 transition-colors">
            <div className="min-w-0">
              <p className={cn("text-[14px] font-semibold leading-snug", t.status === "Done" && "text-muted-foreground line-through")}>{t.title}</p>
              <p className="mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-0.5">
                <TaskMeta task={t} />
                {showTarget && <span className="text-[12px] text-muted-foreground">· {t.targetTitle}</span>}
                {(t.institutionName || t.leadName) && <span className="text-[12px] text-muted-foreground">· {t.institutionName ?? t.leadName}</span>}
              </p>
              {t.status === "Blocked" && t.blockedReason && <p className="mt-0.5 text-[12px] text-destructive">Blocked: {t.blockedReason}</p>}
            </div>
            <div className="flex shrink-0 items-center gap-2">
              {t.priority === "High" && t.status !== "Done" && <Badge variant="destructive">High</Badge>}
              <TaskStatusBadge status={t.status} />
            </div>
          </button>
        </li>
      ))}
    </ul>
  );
}
