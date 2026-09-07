"use client";

import * as React from "react";
import {
  LineChart, Line, AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip,
} from "recharts";
import { ChartContainer } from "./chart-container";
import { ChartTooltip } from "./chart-tooltip";
import { ChartLegend, type ChartSeries } from "./chart-legend";

export interface TrendChartProps<T extends Record<string, unknown>> {
  data: T[];
  xKey: keyof T & string;
  series: ChartSeries<T>[];
  /** "line" for a pure trend, "area" when the series' combined magnitude (e.g. stacked revenue sources) matters as much as the trend. */
  variant?: "line" | "area";
  stacked?: boolean;
  height?: number;
  loading?: boolean;
  emptyMessage?: string;
  valueFormatter?: (value: number) => string;
  className?: string;
}

/**
 * A trend-over-time chart — the shape called for by revenue, growth, and
 * performance metrics. Deliberately restrained: thin lines, a very subtle
 * area fill, sparse gridlines, and dots only on hover — this reads as data,
 * not decoration.
 */
export function TrendChart<T extends Record<string, unknown>>({
  data,
  xKey,
  series,
  variant = "line",
  stacked = false,
  height = 240,
  loading,
  emptyMessage,
  valueFormatter,
  className,
}: TrendChartProps<T>) {
  const isEmpty = !loading && (data.length === 0 || series.every((s) => data.every((d) => !d[s.key])));

  const Chart = variant === "area" ? AreaChart : LineChart;

  return (
    <div className={className}>
      <ChartContainer height={height} loading={loading} isEmpty={isEmpty} emptyMessage={emptyMessage}>
      <Chart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
        {variant === "area" && (
          <defs>
            {series.map((s) => (
              <linearGradient key={s.key} id={`trend-fill-${s.key}`} x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor={s.color} stopOpacity={0.16} />
                <stop offset="100%" stopColor={s.color} stopOpacity={0.01} />
              </linearGradient>
            ))}
          </defs>
        )}
        <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 3" opacity={0.6} />
        <XAxis
          dataKey={xKey}
          axisLine={false}
          tickLine={false}
          tick={{ fontSize: 11, fill: "var(--muted-foreground)" }}
          dy={8}
        />
        <YAxis
          axisLine={false}
          tickLine={false}
          tick={{ fontSize: 11, fill: "var(--muted-foreground)" }}
          width={40}
          tickFormatter={valueFormatter ? (v) => valueFormatter(Number(v)) : undefined}
        />
        <Tooltip
          cursor={{ stroke: "var(--border)", strokeWidth: 1 }}
          content={<ChartTooltip formatValue={valueFormatter} />}
        />
        {series.map((s) =>
          variant === "area" ? (
            <Area
              key={s.key}
              type="monotone"
              dataKey={s.key}
              name={s.label}
              stackId={stacked ? "trend" : undefined}
              stroke={s.color}
              strokeWidth={2}
              fill={`url(#trend-fill-${s.key})`}
              dot={false}
              activeDot={{ r: 4, strokeWidth: 0 }}
            />
          ) : (
            <Line
              key={s.key}
              type="monotone"
              dataKey={s.key}
              name={s.label}
              stroke={s.color}
              strokeWidth={2}
              dot={false}
              activeDot={{ r: 4, strokeWidth: 0 }}
            />
          )
        )}
      </Chart>
      </ChartContainer>
      {!loading && !isEmpty && series.length > 1 && <ChartLegend series={series} />}
    </div>
  );
}
