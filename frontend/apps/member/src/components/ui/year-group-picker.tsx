"use client";

import { useMemo, useState } from "react";
import { Badge } from "./badge";
import { Input } from "./input";
import { X } from "lucide-react";
import { cn } from "@/lib/utils";

interface YearGroupPickerProps {
  value: number[];
  onChange: (years: number[]) => void;
  minYear?: number;
  maxYear?: number;
  disabled?: boolean;
  className?: string;
}

const DEFAULT_MIN_YEAR = 1952;

function normalizeYears(years: number[], minYear: number, maxYear: number) {
  return Array.from(new Set(years))
    .filter((y) => Number.isInteger(y) && y >= minYear && y <= maxYear)
    .sort((a, b) => a - b);
}

export function YearGroupPicker({
  value,
  onChange,
  minYear = DEFAULT_MIN_YEAR,
  maxYear = new Date().getFullYear(),
  disabled,
  className,
}: YearGroupPickerProps) {
  const [input, setInput] = useState("");
  const [error, setError] = useState<string | null>(null);

  const yearOptions = useMemo(() => {
    const years: number[] = [];
    for (let y = maxYear; y >= minYear; y -= 1) {
      years.push(y);
    }
    return years;
  }, [minYear, maxYear]);

  const addYearsFromString = (raw: string) => {
    const parsed = raw
      .split(/[,\s]+/)
      .map((p) => Number(p.trim()))
      .filter((y) => !Number.isNaN(y));

    if (parsed.length === 0) {
      setInput("");
      setError(null);
      return;
    }

    const badYears = parsed.filter((y) => y < minYear || y > maxYear || !Number.isInteger(y));
    if (badYears.length > 0) {
      setError(`Only whole years between ${minYear} and ${maxYear} are allowed.`);
      return;
    }

    const next = normalizeYears([...value, ...parsed], minYear, maxYear);
    onChange(next);
    setInput("");
    setError(null);
  };

  const removeYear = (year: number) => {
    onChange(value.filter((y) => y !== year));
  };

  const handleKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Enter" || event.key === ",") {
      event.preventDefault();
      addYearsFromString(input);
    }
  };

  const handleBlur = () => {
    if (!input.trim()) return;
    addYearsFromString(input);
  };

  return (
    <div className={cn("space-y-2", className)}>
      <div className="flex flex-wrap gap-2">
        {value.map((year) => (
          <Badge
            key={year}
            className="!px-2.5 !py-1 flex items-center gap-1"
            variant="secondary"
            size="sm"
          >
            <span className="font-semibold">{year}</span>
            <button
              type="button"
              className="flex h-5 w-5 items-center justify-center rounded-full hover:bg-muted/50"
              onClick={() => removeYear(year)}
              aria-label={`Remove ${year}`}
            >
              <X className="h-3 w-3" />
            </button>
          </Badge>
        ))}
      </div>

      <div className="flex items-center gap-2">
        <Input
          disabled={disabled}
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          onBlur={handleBlur}
          placeholder={`Type a year (e.g. ${new Date().getFullYear()}) and press Enter`}
          list="year-groups-suggestions"
          className="flex-1"
        />
        <button
          type="button"
          onClick={() => addYearsFromString(input)}
          disabled={disabled}
          className="rounded-lg border border-input bg-background px-3 py-2 text-sm font-medium text-foreground shadow-sm transition hover:bg-accent/40 disabled:cursor-not-allowed disabled:opacity-50"
        >
          Add
        </button>
      </div>

      <datalist id="year-groups-suggestions">
        {yearOptions.map((year) => (
          <option key={year} value={year} />
        ))}
      </datalist>

      <p className="text-xs text-muted-foreground">
        Add one or more graduation years (comma or space separated). Allowed range: {minYear}–{maxYear}.
      </p>
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  );
}
