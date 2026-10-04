"use client";

import * as React from "react";
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Cell } from "recharts";
import { ChartContainer } from "./chart-container";
import { ChartTooltip } from "./chart-tooltip";
import { cn } from "../../lib/utils";

export interface RankingBarDatum {
  label: string;
  value: number;
  color?: string;
}

interface RankingBarChartProps {
  data: RankingBarDatum[];
  /** Default series color when a datum doesn't specify its own. */
  color?: string;
  /** Horizontal bars read long category names far better than vertical ones — prefer this once there are more than ~6 categories. */
  orientation?: "vertical" | "horizontal";
  height?: number;
  loading?: boolean;
  emptyMessage?: string;
  valueFormatter?: (value: number) => string;
  className?: string;
}

/**
 * A comparison/ranking chart — one bar per category, sized for whichever
 * axis holds the category labels. `orientation="horizontal"` swaps X/Y so
 * long labels (institution names, job titles) stay legible instead of
 * rotating or truncating.
 */
export function RankingBarChart({
  data,
  color = "var(--brand-primary-500, var(--primary))",
  orientation = "vertical",
  height = 240,
  loading,
  emptyMessage,
  valueFormatter,
  className,
}: RankingBarChartProps) {
  const isEmpty = !loading && (data.length === 0 || data.every((d) => !d.value));
  const isHorizontal = orientation === "horizontal";

  // Horizontal bars are plain HTML, not a recharts chart: the label sits on its own line above the bar, so a
  // long category name ("KNUST Engineering Alumni Association") is read in full instead of being squeezed
  // into a fixed-width axis, and the value is real text beside it rather than something found by hovering.
  if (isHorizontal && !loading && !isEmpty) {
    const max = Math.max(...data.map((d) => d.value), 0);
    return (
      <ul className={cn("w-full space-y-3", className)}>
        {data.map((d) => (
          <li key={d.label}>
            <div className="flex items-baseline justify-between gap-3">
              <span className="min-w-0 truncate text-[13.5px] text-foreground" title={d.label}>{d.label}</span>
              <span className="shrink-0 text-[13px] font-semibold tabular-nums text-foreground">
                {valueFormatter ? valueFormatter(d.value) : d.value.toLocaleString()}
              </span>
            </div>
            <div className="mt-1 h-2 bg-muted" role="presentation">
              <div className="h-full" style={{ width: `${max > 0 ? (d.value / max) * 100 : 0}%`, background: d.color ?? color }} />
            </div>
          </li>
        ))}
      </ul>
    );
  }

  // From here on the chart is vertical (or a loading/empty state, which ChartContainer draws). The axes are
  // direct children of BarChart on purpose: recharts doesn't look inside a fragment for them, so wrapping
  // the pair in <>…</> draws the bars with no axis labels at all.
  return (
    <ChartContainer height={height} loading={loading} isEmpty={isEmpty} emptyMessage={emptyMessage} className={className}>
      <BarChart data={data} margin={{ top: 8, right: 12, left: 0, bottom: 0 }} barCategoryGap="24%">
        <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 3" opacity={0.6} />
        <XAxis dataKey="label" axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: "var(--muted-foreground)" }} dy={8} />
        <YAxis axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: "var(--muted-foreground)" }} width={40} tickFormatter={valueFormatter ? (v) => valueFormatter(Number(v)) : undefined} />
        <Tooltip cursor={{ fill: "var(--muted)", opacity: 0.5 }} content={<ChartTooltip formatValue={valueFormatter} />} />
        <Bar dataKey="value" radius={[4, 4, 0, 0]} maxBarSize={36}>
          {data.map((d, i) => (
            <Cell key={i} fill={d.color ?? color} />
          ))}
        </Bar>
      </BarChart>
    </ChartContainer>
  );
}
