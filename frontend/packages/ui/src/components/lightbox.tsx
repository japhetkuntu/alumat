"use client";

import * as React from "react";
import { createPortal } from "react-dom";
import { X, ChevronLeft, ChevronRight } from "./icons";
import { cn } from "../lib/utils";

interface LightboxShellProps {
  /** Accessible name, e.g. "Image 2 of 8". */
  label: string;
  onClose: () => void;
  /** Shown in the top bar, e.g. "2 / 8". */
  counter?: React.ReactNode;
  onPrev?: () => void;
  onNext?: () => void;
  prevDisabled?: boolean;
  nextDisabled?: boolean;
  /** Replaces the next arrow with a spinner while the following page loads. */
  nextBusy?: boolean;
  /** Under the photo: dots, a caption. */
  footer?: React.ReactNode;
  /** The photo. Give it `h-full w-full object-contain` so it fills the space left by the bars. */
  children: React.ReactNode;
}

/**
 * The full-screen photo viewer used everywhere. The close button lives in its own bar above the photo, never on top of it, and is a
 * solid white button, so it is plain to see whatever the photo looks like (a bright poster, a snow scene, a photo viewed in landscape).
 * The previous and next buttons are solid too. Escape, the arrow keys, tapping the dark margin, and the close button all work; the page
 * behind does not scroll while it is open; the bars keep clear of a phone's notch and home bar.
 */
export function LightboxShell({ label, onClose, counter, onPrev, onNext, prevDisabled, nextDisabled, nextBusy, footer, children }: LightboxShellProps) {
  // Keep the latest handlers without re-binding the keyboard listener every render.
  const latest = React.useRef({ onClose, onPrev, onNext });
  React.useEffect(() => { latest.current = { onClose, onPrev, onNext }; });

  React.useEffect(() => {
    const original = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") latest.current.onClose();
      else if (e.key === "ArrowRight") latest.current.onNext?.();
      else if (e.key === "ArrowLeft") latest.current.onPrev?.();
    };
    window.addEventListener("keydown", onKey);
    return () => { document.body.style.overflow = original; window.removeEventListener("keydown", onKey); };
  }, []);

  if (typeof document === "undefined") return null;

  const arrow = "pointer-events-auto flex size-12 items-center justify-center bg-black/80 text-white ring-1 ring-white/50 shadow-lg transition-colors hover:bg-black focus-visible:outline focus-visible:outline-2 focus-visible:outline-white disabled:opacity-30 disabled:cursor-not-allowed";

  return createPortal(
    <div
      className="fixed inset-0 z-[10010] flex h-[100dvh] flex-col bg-black animate-fade-in"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-label={label}
    >
      {/* Top bar: its own row, so nothing in the photo can ever cover the close button. */}
      <div
        className="flex shrink-0 items-center justify-between gap-3 px-4 pb-2"
        style={{
          paddingTop: "max(0.75rem, env(safe-area-inset-top))",
          paddingLeft: "max(1rem, env(safe-area-inset-left))",
          paddingRight: "max(1rem, env(safe-area-inset-right))",
        }}
      >
        <span className="min-w-0 text-[14px] font-medium tabular-nums text-white/80">{counter}</span>
        <button
          type="button"
          onClick={(e) => { e.stopPropagation(); onClose(); }}
          aria-label="Close"
          className="flex h-12 shrink-0 items-center justify-center gap-2 bg-white px-4 text-[14px] font-semibold text-black shadow-lg transition-colors hover:bg-white/90 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
        >
          <X size={18} />
          Close
        </button>
      </div>

      {/* The photo gets everything the bars leave. */}
      <div className="relative min-h-0 flex-1 px-4" style={{ paddingLeft: "max(1rem, env(safe-area-inset-left))", paddingRight: "max(1rem, env(safe-area-inset-right))" }}>
        <div className="flex h-full w-full items-center justify-center" onClick={(e) => e.stopPropagation()}>{children}</div>

        {(onPrev || onNext) && (
          <div className="pointer-events-none absolute inset-y-0 left-2 right-2 flex items-center justify-between sm:left-4 sm:right-4">
            {onPrev ? (
              <button type="button" className={arrow} onClick={(e) => { e.stopPropagation(); onPrev(); }} disabled={prevDisabled} aria-label="Previous">
                <ChevronLeft size={22} />
              </button>
            ) : <span />}
            {onNext ? (
              <button type="button" className={arrow} onClick={(e) => { e.stopPropagation(); onNext(); }} disabled={nextDisabled} aria-label="Next">
                {nextBusy ? <span className="h-4 w-4 rounded-full border-2 border-current border-t-transparent animate-spin" /> : <ChevronRight size={22} />}
              </button>
            ) : <span />}
          </div>
        )}
      </div>

      {footer && (
        <div className="flex shrink-0 flex-col items-center gap-2 px-4 pt-3" style={{ paddingBottom: "max(1rem, env(safe-area-inset-bottom))" }} onClick={(e) => e.stopPropagation()}>
          {footer}
        </div>
      )}
      {!footer && <div className="shrink-0" style={{ height: "max(0.75rem, env(safe-area-inset-bottom))" }} />}
    </div>,
    document.body,
  );
}

/**
 * The row of dots under a photo set; the current one is longer. Each dot is a roomy tap target around a small visible mark, and the row is
 * left out on very short screens (a phone on its side), where the height is better spent on the photo.
 */
export function LightboxDots({ count, index, onSelect }: { count: number; index: number; onSelect: (i: number) => void }) {
  return (
    <div className="flex items-center [@media(max-height:480px)]:hidden">
      {Array.from({ length: count }).map((_, i) => (
        <button
          key={i}
          type="button"
          className="group flex h-11 min-w-8 items-center justify-center"
          onClick={(e) => { e.stopPropagation(); onSelect(i); }}
          aria-label={`Go to photo ${i + 1}`}
          aria-current={i === index}
        >
          <span className={cn("block h-2 transition-all duration-150", i === index ? "w-4 bg-white" : "w-2 bg-white/40 group-hover:bg-white/70")} />
        </button>
      ))}
    </div>
  );
}
