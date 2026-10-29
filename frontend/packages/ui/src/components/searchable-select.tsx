"use client";

import * as React from "react";
import { Check, ChevronDown, Search } from "./icons";
import { cn } from "../lib/utils";

export interface SearchableSelectOption {
  value: string;
  label: string;
  /** Kept at the bottom whatever the search says, e.g. "Not listed, let me type it". */
  pinned?: boolean;
}

interface SearchableSelectProps {
  value: string;
  onValueChange: (value: string) => void;
  options: SearchableSelectOption[];
  placeholder?: string;
  searchPlaceholder?: string;
  error?: boolean;
  disabled?: boolean;
  className?: string;
  id?: string;
}

/**
 * A select you can type into. For lists that grow long (programs, chapters), where scrolling to find one is slow:
 * the list narrows as you type, the arrow keys and Enter pick, Escape closes. Entries marked `pinned` always stay at the
 * bottom, so "Not listed" is never lost behind a search.
 */
export function SearchableSelect({ value, onValueChange, options, placeholder = "Select", searchPlaceholder = "Search", error, disabled, className, id }: SearchableSelectProps) {
  const [open, setOpen] = React.useState(false);
  const [query, setQuery] = React.useState("");
  const [active, setActive] = React.useState(0);
  const rootRef = React.useRef<HTMLDivElement>(null);
  const inputRef = React.useRef<HTMLInputElement>(null);
  const listRef = React.useRef<HTMLUListElement>(null);
  const listId = React.useId();

  const q = query.trim().toLowerCase();
  const shown = React.useMemo(() => {
    const matches = options.filter((o) => o.pinned || !q || o.label.toLowerCase().includes(q));
    // Pinned entries last; the rest keep their order.
    return [...matches.filter((o) => !o.pinned), ...matches.filter((o) => o.pinned)];
  }, [options, q]);
  const realMatches = shown.filter((o) => !o.pinned).length;
  const selected = options.find((o) => o.value === value);

  const close = React.useCallback(() => { setOpen(false); setQuery(""); }, []);

  React.useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent | TouchEvent) => { if (!rootRef.current?.contains(e.target as Node)) close(); };
    document.addEventListener("mousedown", onDown);
    document.addEventListener("touchstart", onDown);
    return () => { document.removeEventListener("mousedown", onDown); document.removeEventListener("touchstart", onDown); };
  }, [open, close]);

  React.useEffect(() => { if (open) inputRef.current?.focus(); }, [open]);
  React.useEffect(() => { setActive(0); }, [q, open]);
  React.useEffect(() => {
    listRef.current?.querySelector<HTMLElement>(`[data-index="${active}"]`)?.scrollIntoView({ block: "nearest" });
  }, [active]);

  function pick(o: SearchableSelectOption) { onValueChange(o.value); close(); }

  function onKeyDown(e: React.KeyboardEvent) {
    if (e.key === "ArrowDown") { e.preventDefault(); setActive((i) => Math.min(i + 1, shown.length - 1)); }
    else if (e.key === "ArrowUp") { e.preventDefault(); setActive((i) => Math.max(i - 1, 0)); }
    else if (e.key === "Enter") { e.preventDefault(); const o = shown[active]; if (o) pick(o); }
    else if (e.key === "Escape") { e.preventDefault(); close(); }
  }

  return (
    <div ref={rootRef} className={cn("relative w-full", className)}>
      <button
        type="button"
        id={id}
        disabled={disabled}
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-haspopup="listbox"
        onClick={() => (open ? close() : setOpen(true))}
        className={cn(
          "flex h-11 w-full items-center justify-between gap-2 border border-input bg-background px-3 py-2 text-left text-[14px] text-foreground",
          "transition-all duration-150 ease-out focus:outline-none focus:border-ring focus:ring-2 focus:ring-ring/25",
          "disabled:cursor-not-allowed disabled:opacity-55 disabled:bg-muted/40",
          error && "border-destructive/70 focus:border-destructive/70 focus:ring-destructive/20",
        )}
      >
        <span className={cn("truncate", !selected && "text-muted-foreground/65")}>{selected?.label ?? placeholder}</span>
        <ChevronDown size={15} className="shrink-0 opacity-60" />
      </button>

      {open && (
        <div className="absolute left-0 right-0 z-50 mt-1 border border-border bg-popover text-popover-foreground shadow-lg">
          <div className="relative border-b border-border">
            <Search size={14} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <input
              ref={inputRef}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              onKeyDown={onKeyDown}
              placeholder={searchPlaceholder}
              aria-label={searchPlaceholder}
              aria-controls={listId}
              autoComplete="off"
              className="h-10 w-full bg-transparent pl-9 pr-3 text-[14px] outline-none placeholder:text-muted-foreground/65"
            />
          </div>
          <ul ref={listRef} id={listId} role="listbox" className="max-h-64 overflow-y-auto py-1">
            {realMatches === 0 && q && <li className="px-3 py-2 text-[13px] text-muted-foreground">Nothing matches &ldquo;{query.trim()}&rdquo;</li>}
            {shown.map((o, i) => (
              <li
                key={o.value}
                data-index={i}
                role="option"
                aria-selected={o.value === value}
                onMouseEnter={() => setActive(i)}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => pick(o)}
                className={cn(
                  "flex cursor-pointer items-center justify-between gap-2 px-3 py-2 text-[14px]",
                  i === active && "bg-muted",
                  o.pinned && "border-t border-border text-muted-foreground",
                )}
              >
                <span className="min-w-0 flex-1">{o.label}</span>
                {o.value === value && <Check size={14} className="shrink-0 text-primary" />}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
