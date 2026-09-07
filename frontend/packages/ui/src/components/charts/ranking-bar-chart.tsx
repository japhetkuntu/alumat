"use client";

import * as React from "react";
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Cell } from "recharts";
import { ChartContainer } from "./chart-container";
import { ChartTooltip } from "./chart-tooltip";

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

  return (
    <ChartContainer height={height} loading={loading} isEmpty={isEmpty} emptyMessage={emptyMessage} className={className}>
      <BarChart
        data={data}
        layout={isHorizontal ? "vertical" : "horizontal"}
        margin={{ top: 8, right: 12, left: isHorizontal ? 8 : 0, bottom: 0 }}
        barCategoryGap={isHorizontal ? "28%" : "24%"}
      >
        <CartesianGrid horizontal={!isHorizontal} vertical={isHorizontal} stroke="var(--border)" strokeDasharray="3 3" opacity={0.6} />
        {isHorizontal ? (
          <>
            <XAxis type="number" axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: "var(--muted-foreground)" }} tickFormatter={valueFormatter ? (v) => valueFormatter(Number(v)) : undefined} />
            <YAxis
              type="category"
              dataKey="label"
              axisLine={false}
              tickLine={false}
              tick={{ fontSize: 11.5, fill: "var(--foreground)" }}
              width={110}
            />
          </>
        ) : (
          <>
            <XAxis dataKey="label" axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: "var(--muted-foreground)" }} dy={8} />
            <YAxis axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: "var(--muted-foreground)" }} width={40} tickFormatter={valueFormatter ? (v) => valueFormatter(Number(v)) : undefined} />
          </>
        )}
        <Tooltip cursor={{ fill: "var(--muted)", opacity: 0.5 }} content={<ChartTooltip formatValue={valueFormatter} />} />
        <Bar dataKey="value" radius={isHorizontal ? [0, 4, 4, 0] : [4, 4, 0, 0]} maxBarSize={36}>
          {data.map((d, i) => (
            <Cell key={i} fill={d.color ?? color} />
          ))}
        </Bar>
      </BarChart>
    </ChartContainer>
  );
}
