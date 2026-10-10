"use client";

import { createContext, useContext, useMemo, useState } from "react";
import { Input } from "./input";
import { X } from "./icons";
import { cn } from "../lib/utils";

/** One graduating class an institution has set up, e.g. { year: 2019, label: "Batch of 2019" }. */
export interface YearGroupOption {
  year: number;
  label?: string;
}

const YearGroupOptionsContext = createContext<YearGroupOption[]>([]);

/**
 * Supplies the institution's own batches to every <YearGroupPicker> underneath, so admins pick from the classes that exist
 * instead of typing years from memory. Without a provider the picker falls back to the most recent years.
 */
export function YearGroupOptionsProvider({ options, children }: { options: YearGroupOption[]; children: React.ReactNode }) {
  return <YearGroupOptionsContext.Provider value={options}>{children}</YearGroupOptionsContext.Provider>;
}

interface YearGroupPickerProps {
  value: number[];
  onChange: (years: number[]) => void;
  minYear?: number;
  maxYear?: number;
  disabled?: boolean;
  className?: string;
}

const DEFAULT_MIN_YEAR = 1952;
const MAX_RANGE = 60;

function normalize(years: number[], min: number, max: number) {
  return Array.from(new Set(years)).filter((y) => Number.isInteger(y) && y >= min && y <= max).sort((a, b) => b - a);
}

/** "2019", "2015-2018", "2010 2012, 2014" → the years they name, or an error message. */
function parseYears(raw: string, min: number, max: number): { years: number[] } | { error: string } {
  const years: number[] = [];
  for (const part of raw.split(/[,\s]+/).filter(Boolean)) {
    const range = part.match(/^(\d{4})\s*[-–]\s*(\d{4})$/);
    if (range) {
      const a = Number(range[1]), b = Number(range[2]);
      const [from, to] = a <= b ? [a, b] : [b, a];
      if (to - from + 1 > MAX_RANGE) return { error: `A range can cover at most ${MAX_RANGE} years.` };
      for (let y = from; y <= to; y += 1) years.push(y);
    } else if (/^\d{4}$/.test(part)) {
      years.push(Number(part));
    } else {
      return { error: `"${part}" is not a year. Use a year like ${max}, or a range like ${max - 4}-${max}.` };
    }
  }
  if (years.length === 0) return { error: "Type a year or a range first." };
  const bad = years.find((y) => y < min || y > max);
  if (bad !== undefined) return { error: `Years must be between ${min} and ${max}.` };
  return { years };
}

export function YearGroupPicker({ value, onChange, minYear = DEFAULT_MIN_YEAR, maxYear = new Date().getFullYear(), disabled, className }: YearGroupPickerProps) {
  const provided = useContext(YearGroupOptionsContext);
  const [search, setSearch] = useState("");
  const [extra, setExtra] = useState("");
  const [error, setError] = useState<string | null>(null);

  // The institution's batches (newest first), or the latest dozen years when none have been set up yet.
  const options = useMemo<YearGroupOption[]>(() => {
    if (provided.length > 0) return [...provided].sort((a, b) => b.year - a.year);
    return Array.from({ length: 12 }, (_, i) => ({ year: maxYear - i })).filter((o) => o.year >= minYear);
  }, [provided, minYear, maxYear]);
  const usingBatches = provided.length > 0;

  const selected = useMemo(() => new Set(value), [value]);
  const visible = useMemo(() => {
    const q = search.trim().toLowerCase();
    return q ? options.filter((o) => String(o.year).includes(q) || (o.label ?? "").toLowerCase().includes(q)) : options;
  }, [options, search]);
  // Years already chosen that are not in the list above (typed in, or from a batch since removed).
  const outside = value.filter((y) => !options.some((o) => o.year === y)).sort((a, b) => b - a);

  const toggle = (year: number) => onChange(selected.has(year) ? value.filter((y) => y !== year) : normalize([...value, year], minYear, maxYear));
  const allVisibleSelected = visible.length > 0 && visible.every((o) => selected.has(o.year));
  const toggleVisible = () => onChange(allVisibleSelected
    ? value.filter((y) => !visible.some((o) => o.year === y))
    : normalize([...value, ...visible.map((o) => o.year)], minYear, maxYear));

  const addExtra = () => {
    const parsed = parseYears(extra, minYear, maxYear);
    if ("error" in parsed) { setError(parsed.error); return; }
    onChange(normalize([...value, ...parsed.years], minYear, maxYear));
    setExtra("");
    setError(null);
  };

  return (
    <div className={cn("space-y-3 border border-border bg-card p-3 sm:p-4", disabled && "pointer-events-none opacity-60", className)}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-[13px] font-semibold">
          {value.length === 0 ? "No year groups chosen yet" : `${value.length} year group${value.length === 1 ? "" : "s"} chosen`}
        </p>
        {value.length > 0 && (
          <button type="button" onClick={() => onChange([])} className="min-h-9 px-1 text-[12.5px] font-semibold text-muted-foreground underline underline-offset-4 hover:text-foreground">
            Clear all
          </button>
        )}
      </div>

      {value.length > 0 && (
        <ul className="flex flex-wrap gap-1.5" aria-label="Chosen year groups">
          {[...value].sort((a, b) => b - a).map((year) => (
            <li key={year}>
              <button
                type="button"
                onClick={() => toggle(year)}
                aria-label={`Remove ${year}`}
                className="inline-flex min-h-9 items-center gap-1.5 border border-primary/30 bg-primary/10 pl-2.5 pr-1.5 text-[13px] font-semibold text-primary hover:bg-primary/15"
              >
                {year}
                <X className="h-3.5 w-3.5" />
              </button>
            </li>
          ))}
        </ul>
      )}

      <div className="space-y-2">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-[12.5px] font-semibold text-muted-foreground">{usingBatches ? "Your batches" : "Recent years"}</p>
          {visible.length > 1 && (
            <button type="button" onClick={toggleVisible} className="min-h-9 px-1 text-[12.5px] font-semibold text-primary underline underline-offset-4">
              {allVisibleSelected ? "Unselect these" : search.trim() ? "Select all matches" : "Select all"}
            </button>
          )}
        </div>
        {options.length > 8 && (
          <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search by year or name" aria-label="Search batches" className="h-10" />
        )}
        {visible.length === 0 ? (
          <p className="py-2 text-[13px] text-muted-foreground">Nothing matches &ldquo;{search}&rdquo;.</p>
        ) : (
          <div className="grid max-h-52 grid-cols-2 gap-2 overflow-y-auto sm:grid-cols-3">
            {visible.map((o) => {
              const on = selected.has(o.year);
              return (
                <button
                  key={o.year}
                  type="button"
                  aria-pressed={on}
                  onClick={() => toggle(o.year)}
                  className={cn(
                    "flex min-h-12 flex-col items-start justify-center border px-3 py-1.5 text-left transition-colors",
                    on ? "border-primary bg-primary/10 text-primary" : "border-border hover:border-primary/40 hover:bg-muted/50",
                  )}
                >
                  <span className="text-[14px] font-bold leading-tight tabular-nums">{o.year}</span>
                  {o.label && o.label !== String(o.year) && <span className="w-full truncate text-[12px] leading-tight text-muted-foreground">{o.label}</span>}
                </button>
              );
            })}
          </div>
        )}
        {outside.length > 0 && usingBatches && (
          <p className="text-[12.5px] text-muted-foreground">Also chosen, without a batch set up: {outside.join(", ")}.</p>
        )}
      </div>

      <div className="space-y-1.5 border-t border-border pt-3">
        <label htmlFor="ygp-extra" className="text-[12.5px] font-semibold text-muted-foreground">Add other years or a range</label>
        <div className="flex gap-2">
          <Input
            id="ygp-extra"
            value={extra}
            inputMode="numeric"
            onChange={(e) => { setExtra(e.target.value); setError(null); }}
            onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); addExtra(); } }}
            placeholder={`e.g. ${maxYear - 9}-${maxYear - 5}`}
            className="h-10 flex-1"
          />
          <button type="button" onClick={addExtra} className="min-h-10 border border-input bg-background px-4 text-[13px] font-semibold hover:bg-accent/40">Add</button>
        </div>
        {error && <p role="alert" className="text-[12.5px] text-destructive">{error}</p>}
      </div>
    </div>
  );
}
