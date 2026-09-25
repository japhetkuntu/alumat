"use client";

import * as React from "react";
import { cn } from "../lib/utils";

interface ChipRowProps {
  children: React.ReactNode;
  /** Accessible name for the group, e.g. "Filter by category". */
  label?: string;
  /** Changes whenever the selected chip changes, so the row can bring the new one into view. */
  activeKey?: string | number | null;
  className?: string;
}

/**
 * A row of filter chips or tabs. On a phone it is one swipeable line (chips never wrap onto three
 * or four rows and push the content down); from `sm` up it wraps like a normal flex row. The
 * chosen chip is scrolled into view when it changes, so it is never hidden off-screen.
 *
 * Children are ordinary buttons. Mark the selected one with aria-pressed, aria-selected or
 * data-active so the row can find it. Bleeds to the screen edge inside a page padded with p-4.
 */
export function ChipRow({ children, label, activeKey, className }: ChipRowProps) {
  const ref = React.useRef<HTMLDivElement>(null);
  // On a phone, fade the right edge while more chips are hidden off-screen, so it reads as swipeable.
  const [moreRight, setMoreRight] = React.useState(false);

  const updateFade = React.useCallback(() => {
    const row = ref.current;
    if (row) setMoreRight(row.scrollWidth - row.clientWidth - row.scrollLeft > 4);
  }, []);

  React.useEffect(() => {
    updateFade();
    const row = ref.current;
    if (!row) return;
    const observer = new ResizeObserver(updateFade);
    observer.observe(row);
    return () => observer.disconnect();
  }, [updateFade]);

  React.useEffect(() => {
    const row = ref.current;
    const chip = row?.querySelector<HTMLElement>('[aria-pressed="true"],[aria-selected="true"],[data-active="true"]');
    if (!row || !chip) return;
    const r = row.getBoundingClientRect();
    const c = chip.getBoundingClientRect();
    row.scrollLeft += c.left - r.left - (r.width - c.width) / 2;
    updateFade();
  }, [activeKey, updateFade]);

  return (
    <div
      ref={ref}
      role="group"
      aria-label={label}
      onScroll={updateFade}
      className={cn(
        moreRight && "[mask-image:linear-gradient(to_right,black_calc(100%-32px),transparent)] sm:[mask-image:none]",
        "-mx-4 flex snap-x scroll-pl-4 items-center gap-2 overflow-x-auto px-4 pb-1",
        "sm:mx-0 sm:flex-wrap sm:overflow-visible sm:scroll-pl-0 sm:px-0 sm:pb-0",
        "[scrollbar-width:none] [&::-webkit-scrollbar]:hidden",
        "[&>button]:shrink-0 [&>button]:snap-start [&>button]:whitespace-nowrap",
        className
      )}
    >
      {children}
    </div>
  );
}

interface SegmentedControlProps<T extends string> {
  options: readonly { value: T; label: string }[];
  value: T;
  onChange: (value: T) => void;
  label: string;
  className?: string;
}

/** A short set of mutually exclusive choices (up to about four): equal-width across a phone, compact from `sm` up. */
export function SegmentedControl<T extends string>({ options, value, onChange, label, className }: SegmentedControlProps<T>) {
  return (
    <div role="tablist" aria-label={label} className={cn("flex max-w-full overflow-x-auto border border-border bg-background [scrollbar-width:none] [&::-webkit-scrollbar]:hidden sm:inline-flex", className)}>
      {options.map((o) => (
        <button
          key={o.value}
          role="tab"
          aria-selected={value === o.value}
          onClick={() => onChange(o.value)}
          className={cn(
            "flex-1 shrink-0 whitespace-nowrap px-2 py-2.5 text-[12px] font-semibold transition-colors sm:flex-none sm:px-5 sm:text-[12.5px]",
            value === o.value ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:bg-muted/60 hover:text-foreground"
          )}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}
