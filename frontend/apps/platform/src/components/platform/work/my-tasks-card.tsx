"use client";

import Link from "next/link";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Card } from "@alumni/ui";
import { getWorkTasks } from "@/lib/platform-api";
import { TaskDetailDialog } from "./task-detail-dialog";
import { dueLabel } from "./work-common";

/** The dashboard's short list of the signed-in person's open tasks, worst first. Renders nothing when they have none. */
export function MyTasksCard() {
  const [openId, setOpenId] = useState<string | null>(null);
  const { data } = useQuery({ queryKey: ["work-tasks", "mine"], queryFn: () => getWorkTasks({ mine: true }), staleTime: 60 * 1000 });

  const open = (data ?? []).filter((t) => t.status !== "Done");
  if (open.length === 0) return null;
  const overdue = open.filter((t) => t.isOverdue).length;

  return (
    <Card className="mb-4">
      <div className="px-5 py-4 border-b border-border flex items-center justify-between gap-3">
        <p className="text-[14px] font-semibold">
          My tasks <span className="font-normal text-muted-foreground">{open.length} open{overdue > 0 && <span className="text-destructive font-semibold">, {overdue} overdue</span>}</span>
        </p>
        <Link href="/activation?tab=tasks" className="text-[12px] font-semibold text-accent hover:underline">See all</Link>
      </div>
      <ul>
        {open.slice(0, 4).map((t) => (
          <li key={t.id} className="border-t border-border first:border-0">
            <button type="button" onClick={() => setOpenId(t.id)} className="flex w-full items-baseline justify-between gap-4 px-5 py-3 text-left hover:bg-muted/40 transition-colors">
              <span className="min-w-0 truncate text-[13.5px] font-medium">{t.title}</span>
              <span className={`shrink-0 text-[12px] ${t.isOverdue ? "text-destructive font-semibold" : "text-muted-foreground"}`}>{dueLabel(t.dueDate)}</span>
            </button>
          </li>
        ))}
      </ul>
      {openId && <TaskDetailDialog taskId={openId} onClose={() => setOpenId(null)} />}
    </Card>
  );
}
