"use client";

import * as React from "react";
import { PieChart, Pie, Cell, Tooltip } from "recharts";
import { ChartContainer } from "./chart-container";
import { ChartTooltip } from "./chart-tooltip";
import { ChartLegend } from "./chart-legend";

export interface DonutDatum {
  label: string;
  value: number;
  color: string;
}

interface DonutChartProps {
  data: DonutDatum[];
  /** Shown in the empty center — the total, or any single figure the segments break down. Omit for a plain ring. */
  centerValue?: React.ReactNode;
  centerLabel?: string;
  height?: number;
  loading?: boolean;
  emptyMessage?: string;
  valueFormatter?: (value: number) => string;
  className?: string;
}

/**
 * Part-to-whole breakdown — used sparingly, for a small number of segments.
 * The center is free real estate for the total, which is usually the first
 * thing a reader wants next to the breakdown itself.
 */
export function DonutChart({
  data,
  centerValue,
  centerLabel,
  height = 220,
  loading,
  emptyMessage,
  valueFormatter,
  className,
}: DonutChartProps) {
  const isEmpty = !loading && (data.length === 0 || data.every((d) => !d.value));
  const radius = Math.min(height, 220) / 2;

  return (
    <div className={className}>
      <div className="relative">
        <ChartContainer height={height} loading={loading} isEmpty={isEmpty} emptyMessage={emptyMessage}>
          <PieChart>
            <Pie
              data={data}
              dataKey="value"
              nameKey="label"
              innerRadius={radius * 0.62}
              outerRadius={radius * 0.92}
              paddingAngle={data.length > 1 ? 2 : 0}
              strokeWidth={0}
            >
              {data.map((d) => (
                <Cell key={d.label} fill={d.color} />
              ))}
            </Pie>
            <Tooltip content={<ChartTooltip formatValue={valueFormatter} />} />
          </PieChart>
        </ChartContainer>
        {!loading && !isEmpty && centerValue !== undefined && (
          <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
            <span className="text-[18px] font-semibold tabular-nums text-foreground">{centerValue}</span>
            {centerLabel && <span className="text-[11px] text-muted-foreground">{centerLabel}</span>}
          </div>
        )}
      </div>
      {!loading && !isEmpty && (
        <ChartLegend series={data.map((d) => ({ key: d.label, label: d.label, color: d.color }))} />
      )}
    </div>
  );
}
