"use client";

import * as React from "react";

interface ChartTooltipProps {
  active?: boolean;
  label?: string | number;
  payload?: Array<{
    name?: string;
    value?: number | string;
    color?: string;
    dataKey?: string | number;
  }>;
  formatValue?: (value: number) => string;
  labelFormatter?: (label: string | number) => string;
}

/**
 * Recharts custom tooltip — consistent card styling across every chart
 * instead of each chart hand-rolling contentStyle. Recharts calls this with
 * `active`/`payload`/`label` regardless of chart type, so one component
 * covers line/area/bar/donut.
 */
export function ChartTooltip({ active, label, payload, formatValue, labelFormatter }: ChartTooltipProps) {
  if (!active || !payload || payload.length === 0) return null;

  const format = formatValue ?? ((v: number) => v.toLocaleString());

  return (
    <div className="min-w-[140px] rounded-lg border border-border bg-popover px-3 py-2 shadow-[0_8px_24px_rgba(0,0,0,0.1)]">
      {label !== undefined && (
        <p className="mb-1.5 text-[11px] font-medium text-muted-foreground">
          {labelFormatter ? labelFormatter(label) : label}
        </p>
      )}
      <div className="space-y-1">
        {payload.map((entry, i) => (
          <div key={`${entry.dataKey}-${i}`} className="flex items-center justify-between gap-4 text-[12.5px]">
            <span className="flex items-center gap-1.5 text-foreground/80">
              <span className="h-2 w-2 rounded-full shrink-0" style={{ background: entry.color }} />
              {entry.name}
            </span>
            <span className="font-semibold tabular-nums text-foreground">
              {typeof entry.value === "number" ? format(entry.value) : entry.value}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}
