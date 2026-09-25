"use client";

import * as React from "react";
import { CheckCircle2, Circle, ClipboardList, X } from "./icons";
import { Button } from "./button";
import { Progress } from "./progress";
import { cn } from "../lib/utils";

export interface ChecklistItem {
  id: string;
  title: string;
  /** One line on why this step matters. Shown only while the step is open. */
  description?: string;
  /** Derived from real data by the caller, never a "mark as done" toggle. */
  done: boolean;
  /** Explicit verb-first label, e.g. "Add your logo". */
  actionLabel: string;
  onAction: () => void;
}

interface GetStartedChecklistProps {
  title?: string;
  items: ChecklistItem[];
  /** localStorage key, unique per portal and user, so the choice to hide it sticks per person. */
  storageKey: string;
  /** True while the data behind `items` is still loading, so steps never flash as undone. */
  loading?: boolean;
  /** Overrides the default corner offset, e.g. to clear a mobile bottom nav. */
  className?: string;
}

function readDismissed(key: string): boolean {
  try {
    return !!(JSON.parse(localStorage.getItem(key) ?? "{}") as { dismissed?: boolean }).dismissed;
  } catch {
    return false;
  }
}

/**
 * A setup tracker shaped like a chat launcher: one small round button in the
 * corner that never covers content, and opens a panel only when tapped. It
 * never opens on its own, shows how many steps are left on the button, can be
 * hidden for good, and disappears once every step is done.
 */
export function GetStartedChecklist({ title = "Get started", items, storageKey, loading, className }: GetStartedChecklistProps) {
  const [hydrated, setHydrated] = React.useState(false);
  const [dismissed, setDismissed] = React.useState(false);
  const [open, setOpen] = React.useState(false);
  const rootRef = React.useRef<HTMLElement>(null);

  React.useEffect(() => {
    setDismissed(readDismissed(storageKey));
    setHydrated(true);
  }, [storageKey]);

  React.useEffect(() => {
    if (!open) return;
    function onPointerDown(e: MouseEvent | TouchEvent) {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) setOpen(false);
    }
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("touchstart", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("touchstart", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  if (!hydrated || loading || dismissed || items.length === 0) return null;

  const doneCount = items.filter((i) => i.done).length;
  const remaining = items.length - doneCount;
  if (remaining === 0) return null;

  const nextId = items.find((i) => !i.done)?.id;
  const percent = Math.round((doneCount / items.length) * 100);

  function hide() {
    setOpen(false);
    setDismissed(true);
    try {
      localStorage.setItem(storageKey, JSON.stringify({ dismissed: true }));
    } catch { /* ignore */ }
  }

  return (
    <section
      ref={rootRef}
      aria-label={title}
      className={cn("fixed right-4 bottom-4 z-40 flex flex-col items-end gap-3", className)}
    >
      {open && (
        <div
          id="get-started-panel"
          className="max-h-[min(32rem,calc(100dvh-11rem))] w-[min(22rem,calc(100vw-2rem))] overflow-y-auto border border-border bg-card shadow-sm animate-in fade-in-0 duration-150"
        >
          <div className="flex items-start gap-2 px-4 pt-3.5">
            <div className="min-w-0 flex-1">
              <p className="text-[13.5px] font-semibold text-foreground">{title}</p>
              <p className="text-[12px] text-muted-foreground">{doneCount} of {items.length} steps done</p>
            </div>
            <button
              type="button"
              onClick={() => setOpen(false)}
              aria-label="Close checklist"
              className="p-1 text-muted-foreground hover:text-foreground"
            >
              <X size={13} />
            </button>
          </div>
          <Progress value={percent} className="mx-4 mt-2.5 h-1.5 w-auto" />
          <ul className="mt-2">
            {items.map((item) => {
              const isNext = item.id === nextId;
              return (
                <li key={item.id} className={cn("flex gap-3 px-4 py-2.5", isNext && "bg-muted/50")}>
                  {item.done ? (
                    <CheckCircle2 size={15} className="mt-0.5 shrink-0 text-success" />
                  ) : (
                    <Circle size={15} className="mt-0.5 shrink-0 text-muted-foreground/60" />
                  )}
                  <div className="min-w-0 flex-1">
                    <p className={cn("text-[13px] font-semibold", item.done ? "text-muted-foreground line-through" : "text-foreground")}>
                      {item.title}
                    </p>
                    {!item.done && item.description && (
                      <p className="mt-0.5 text-[12px] leading-snug text-muted-foreground">{item.description}</p>
                    )}
                    {!item.done && (
                      isNext ? (
                        <Button
                          size="sm"
                          className="mt-2 h-8 px-3 text-[12px] font-semibold"
                          onClick={() => { setOpen(false); item.onAction(); }}
                        >
                          {item.actionLabel}
                        </Button>
                      ) : (
                        <button
                          type="button"
                          onClick={() => { setOpen(false); item.onAction(); }}
                          className="mt-1 text-[12px] font-semibold text-primary hover:underline"
                        >
                          {item.actionLabel}
                        </button>
                      )
                    )}
                  </div>
                </li>
              );
            })}
          </ul>
          <div className="border-t border-border px-4 py-2.5">
            <button type="button" onClick={hide} className="text-[12px] text-muted-foreground hover:text-foreground hover:underline">
              Hide this checklist
            </button>
          </div>
        </div>
      )}

      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-expanded={open}
        aria-controls="get-started-panel"
        aria-label={`${title}: ${doneCount} of ${items.length} steps done`}
        className="relative flex h-12 w-12 items-center justify-center rounded-full border border-border/60 bg-primary text-primary-foreground shadow-sm transition-transform hover:scale-105 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2"
      >
        {open ? <X size={16} /> : <ClipboardList size={17} />}
        {!open && (
          <span className="absolute -right-1 -top-1 flex h-5 min-w-5 items-center justify-center rounded-full border border-border bg-card px-1 text-[11px] font-bold leading-none text-foreground">
            {remaining}
          </span>
        )}
      </button>
    </section>
  );
}
