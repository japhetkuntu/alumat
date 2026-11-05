"use client";

import * as React from "react";
import { Check, ClipboardList, X } from "./icons";
import { Button } from "./button";
import { Dialog, DialogContent, DialogDescription, DialogTitle } from "./dialog";
import { useSwipeDownToClose } from "./sheet-gestures";
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

/** True on a phone-width screen, where the panel is a bottom sheet instead of a small card beside the button. */
function useIsPhone() {
  const [phone, setPhone] = React.useState(false);
  React.useEffect(() => {
    const m = window.matchMedia("(max-width: 639px)");
    const on = () => setPhone(m.matches);
    on();
    m.addEventListener("change", on);
    return () => m.removeEventListener("change", on);
  }, []);
  return phone;
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
  const phone = useIsPhone();
  const [confirmHide, setConfirmHide] = React.useState(false);
  React.useEffect(() => { if (!confirmHide) return; const t = window.setTimeout(() => setConfirmHide(false), 4000); return () => window.clearTimeout(t); }, [confirmHide]);
  React.useEffect(() => { if (!open) setConfirmHide(false); }, [open]);
  const swipe = useSwipeDownToClose(() => setOpen(false));

  React.useEffect(() => {
    setDismissed(readDismissed(storageKey));
    setHydrated(true);
  }, [storageKey]);

  React.useEffect(() => {
    if (!open || phone) return;
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
  }, [open, phone]);

  // Phones: slide out of the way while the page scrolls down (it would otherwise sit on top of buttons
  // like "Pay now"), and come back as soon as the person scrolls up. Never tucks away while open.
  const [tucked, setTucked] = React.useState(false);
  React.useEffect(() => {
    const lastTop = new WeakMap<EventTarget, number>();
    function onScroll(e: Event) {
      const target = e.target as HTMLElement | Document;
      const top = target instanceof Document ? window.scrollY : target.scrollTop;
      const prev = lastTop.get(target);
      if (prev === undefined) {
        lastTop.set(target, top);
        return;
      }
      const delta = top - prev;
      if (Math.abs(delta) < 6) return;
      lastTop.set(target, top);
      setTucked(delta > 0 && top > 80);
    }
    document.addEventListener("scroll", onScroll, { passive: true, capture: true });
    return () => document.removeEventListener("scroll", onScroll, true);
  }, []);

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

  const steps = (
    <ul className="space-y-2.5">
      {items.map((item, index) => {
        const isNext = item.id === nextId;
        return (
          <li key={item.id}
            className={cn("flex gap-3.5 rounded-[16px] border px-3.5 py-3.5 transition-colors",
              item.done ? "border-transparent bg-muted/40" : isNext ? "border-primary/25 bg-primary/[0.05]" : "border-border/70 bg-background")}>
            <span aria-hidden
              className={cn("mt-0.5 grid h-7 w-7 shrink-0 place-items-center rounded-full text-[12px] font-bold",
                item.done ? "bg-success text-white" : isNext ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground")}>
              {item.done ? <Check size={13} /> : index + 1}
            </span>
            <div className="min-w-0 flex-1">
              <p className={cn("text-[14px] font-semibold leading-snug", item.done ? "text-muted-foreground" : "text-foreground")}>
                {item.title}
              </p>
              {!item.done && item.description && (
                <p className="mt-1 text-[12.5px] leading-relaxed text-muted-foreground">{item.description}</p>
              )}
              {!item.done && (
                isNext ? (
                  <Button className="mt-3 h-10 w-full rounded-full text-[13.5px] font-semibold sm:w-auto sm:px-5"
                    onClick={() => { setOpen(false); item.onAction(); }}>
                    {item.actionLabel}
                  </Button>
                ) : (
                  <button type="button" onClick={() => { setOpen(false); item.onAction(); }}
                    className="mt-2 text-[13px] font-semibold text-primary hover:underline">
                    {item.actionLabel}
                  </button>
                )
              )}
            </div>
          </li>
        );
      })}
    </ul>
  );

  const progress = (
    <div className="flex items-center gap-3">
      <div className="h-2 flex-1 overflow-hidden rounded-full bg-primary/10" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={percent}>
        <div className="h-full rounded-full bg-primary transition-all duration-500 ease-out" style={{ width: `${percent}%` }} />
      </div>
      <span className="text-[12px] font-semibold tabular-nums text-muted-foreground">{doneCount}/{items.length}</span>
    </div>
  );

  // Hiding is permanent, so it takes a deliberate second tap. Closing the sheet is the Close button, a tap outside it, or a drag down.
  const hideLink = (
    <button type="button" onClick={() => (confirmHide ? hide() : setConfirmHide(true))}
      className={cn("text-[12.5px] hover:underline", confirmHide ? "font-semibold text-destructive" : "text-muted-foreground hover:text-foreground")}>
      {confirmHide ? "Tap again to hide it for good" : "Don't show this again"}
    </button>
  );

  const launcher = (
    <button
      type="button"
      onClick={() => setOpen((o) => !o)}
      aria-expanded={open}
      aria-controls="get-started-panel"
      aria-label={`${title}: ${doneCount} of ${items.length} steps done`}
      className="relative flex h-11 w-11 sm:h-12 sm:w-12 items-center justify-center rounded-full border border-border/60 bg-primary text-primary-foreground shadow-md transition-transform hover:scale-105 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring active:scale-95"
    >
      {open && !phone ? <X size={16} /> : <ClipboardList size={17} />}
      {!(open && !phone) && (
        <span className="absolute -right-1 -top-1 flex h-5 min-w-5 items-center justify-center rounded-full border border-border bg-card px-1 text-[11px] font-bold leading-none text-foreground">
          {remaining}
        </span>
      )}
    </button>
  );

  return (
    <section
      ref={rootRef}
      aria-label={title}
      className={cn(
        "fixed right-4 bottom-4 z-40 flex flex-col items-end gap-3 transition-[transform,opacity] duration-200",
        tucked && !open && "max-lg:pointer-events-none max-lg:translate-y-24 max-lg:opacity-0",
        className
      )}
    >
      {/* Wide screens: a small card beside the button. */}
      {open && !phone && (
        <div
          id="get-started-panel"
          className="max-h-[min(34rem,calc(100dvh-11rem))] w-[min(23rem,calc(100vw-2rem))] overflow-y-auto rounded-[20px] border border-border bg-card p-4 shadow-xl animate-in fade-in-0 slide-in-from-bottom-2 duration-200"
        >
          <div className="flex items-start gap-2">
            <div className="min-w-0 flex-1">
              <p className="text-[15px] font-semibold text-foreground">{title}</p>
              <p className="text-[12.5px] text-muted-foreground">{remaining} {remaining === 1 ? "step" : "steps"} to go</p>
            </div>
            <button type="button" onClick={() => setOpen(false)} aria-label="Close checklist"
              className="grid h-8 w-8 place-items-center rounded-full text-muted-foreground hover:bg-muted hover:text-foreground">
              <X size={14} />
            </button>
          </div>
          <div className="mt-3">{progress}</div>
          <div className="mt-4">{steps}</div>
          <div className="mt-4 border-t border-border pt-3">{hideLink}</div>
        </div>
      )}

      {launcher}

      {/* Phones: a bottom sheet, like the rest of the app's sheets. */}
      {phone && (
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogContent ref={swipe.ref} id="get-started-panel" className="flex max-h-[88dvh] flex-col gap-0 overflow-hidden rounded-t-[24px] p-0 pt-4 sm:rounded-[20px]">
            <div className="shrink-0 touch-none px-5 pb-4 pr-16" {...swipe.bind}>
              <DialogTitle className="font-[family-name:var(--font-display)] text-[20px] font-semibold tracking-tight">{title}</DialogTitle>
              <DialogDescription className="mt-0.5 text-[13px]">
                {remaining === 1 ? "One step to go." : `${remaining} steps to go.`} Each one ticks itself off.
              </DialogDescription>
              <div className="mt-3.5">{progress}</div>
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-4 pb-3">{steps}</div>
            <div className="shrink-0 space-y-2 border-t border-border/60 px-5 pb-[max(1rem,env(safe-area-inset-bottom))] pt-3.5 text-center">
              <Button variant="outline" className="h-11 w-full rounded-full text-[14px] font-semibold text-foreground" onClick={() => setOpen(false)}>Close</Button>
              {hideLink}
            </div>
          </DialogContent>
        </Dialog>
      )}
    </section>
  );
}
