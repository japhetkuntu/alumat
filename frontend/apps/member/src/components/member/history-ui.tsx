"use client";

import { Check, cn } from "@alumni/ui";
import { formatDate } from "@alumni/ui";
import type { ServiceRequestUpdate } from "@/types";

/** Filter chips with counts. Scrolls sideways on a phone rather than wrapping into two rows. */
export function FilterTabs<T extends string>({
  label, value, onChange, options,
}: { label: string; value: T; onChange: (v: T) => void; options: { value: T; label: string; count: number }[] }) {
  return (
    <div role="tablist" aria-label={label} className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1 sm:mx-0 sm:flex-wrap sm:px-0 sm:pb-0">
      {options.map((o) => {
        const active = o.value === value;
        return (
          <button
            key={o.value}
            type="button"
            role="tab"
            aria-selected={active}
            onClick={() => onChange(o.value)}
            className={cn(
              "inline-flex h-10 shrink-0 items-center gap-2 border px-3.5 text-[13px] font-semibold transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary",
              active ? "border-primary bg-primary text-primary-foreground" : "border-border bg-card text-foreground hover:bg-muted",
            )}
          >
            {o.label}
            <span className={cn("tabular-nums text-[12px]", active ? "text-primary-foreground/80" : "text-muted-foreground")}>{o.count}</span>
          </button>
        );
      })}
    </div>
  );
}

/**
 * Where something is in its journey: "Step 2 of 4 · In review" over a bar of equal segments. Works at any width and with
 * any number of steps, unlike a row of fixed-width circles. `current` is the index of the stage it is at (-1 when unknown).
 */
export function StepProgress({ stages, current, done }: { stages: string[]; current: number; done: boolean }) {
  if (stages.length === 0) return null;
  const at = done ? stages.length : Math.max(current + 1, 0);
  return (
    <div className="space-y-2">
      <p className="text-[13px]">
        <span className="font-semibold">{done ? "Complete" : current >= 0 ? stages[current] : "Submitted"}</span>
        <span className="text-muted-foreground"> · {done ? `all ${stages.length} steps done` : `step ${Math.max(current + 1, 1)} of ${stages.length}`}</span>
      </p>
      <div className="flex gap-1" role="progressbar" aria-valuemin={0} aria-valuemax={stages.length} aria-valuenow={at} aria-label="Progress">
        {stages.map((s, i) => (
          <span key={s + i} className={cn("h-1.5 flex-1", i < at ? "bg-primary" : "bg-border")} />
        ))}
      </div>
    </div>
  );
}

/** Every step as a vertical list, with the ones already reached ticked. */
export function StepList({ stages, current, done }: { stages: string[]; current: number; done: boolean }) {
  return (
    <ol className="space-y-1.5">
      {stages.map((s, i) => {
        const reached = done || i <= current;
        const isNow = !done && i === current;
        return (
          <li key={s + i} className="flex items-center gap-2.5 text-[13px]">
            <span className={cn("flex size-5 shrink-0 items-center justify-center border", reached ? "border-primary bg-primary text-primary-foreground" : "border-border text-transparent")}>
              <Check size={11} />
            </span>
            <span className={cn(reached ? "text-foreground" : "text-muted-foreground", isNow && "font-semibold")}>{s}{isNow && " (now)"}</span>
          </li>
        );
      })}
    </ol>
  );
}

/** What was said at each step, newest first. */
export function UpdateTimeline({ updates }: { updates: ServiceRequestUpdate[] }) {
  if (updates.length === 0) return null;
  return (
    <ol className="space-y-3 border-l-2 border-border pl-4">
      {[...updates].reverse().map((u, i) => (
        <li key={i} className="relative">
          <span className="absolute -left-[21px] top-1.5 size-2 bg-primary" />
          <p className="text-[13px] font-semibold leading-tight">{u.stage ?? "Update"}</p>
          {u.note && <p className="mt-0.5 text-[13px] leading-relaxed text-muted-foreground">{u.note}</p>}
          {u.attachmentUrl && (
            <a href={u.attachmentUrl} target="_blank" rel="noreferrer" className="mt-0.5 inline-block text-[13px] font-semibold text-primary underline-offset-2 hover:underline">
              Open attachment
            </a>
          )}
          <p className="text-[12px] text-muted-foreground">{formatDate(u.changedAt)}{u.changedByStaffName ? ` · ${u.changedByStaffName}` : ""}</p>
        </li>
      ))}
    </ol>
  );
}
