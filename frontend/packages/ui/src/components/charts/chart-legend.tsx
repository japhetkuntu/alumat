"use client";

import * as React from "react";

export interface ChartSeries<T = Record<string, unknown>> {
  key: keyof T & string;
  label: string;
  color: string;
}

/** A plain, static legend below the chart — not recharts' built-in <Legend>, so it reads consistently across line/area/bar and never fights the chart for vertical space. */
export function ChartLegend({ series }: { series: ChartSeries<any>[] }) {
  return (
    <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1.5">
      {series.map((s) => (
        <span key={s.key} className="flex items-center gap-1.5 text-[12px] text-muted-foreground">
          <span className="h-2 w-2 rounded-full shrink-0" style={{ background: s.color }} />
          {s.label}
        </span>
      ))}
    </div>
  );
}
