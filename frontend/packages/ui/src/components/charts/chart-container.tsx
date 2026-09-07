"use client";

import * as React from "react";
import { ResponsiveContainer } from "recharts";
import { BarChart3, AlertCircle } from "../icons";
import { cn } from "../../lib/utils";

interface ChartContainerProps {
  height?: number;
  loading?: boolean;
  isEmpty?: boolean;
  emptyMessage?: string;
  error?: string | null;
  children: React.ReactElement;
  className?: string;
}

/** A chart-shaped skeleton — staggered bars of random height plus a baseline
 *  — reads immediately as "a chart is loading" rather than a generic gray
 *  rectangle, without pulling in any extra dependency. */
function ChartSkeleton({ height }: { height: number }) {
  const bars = React.useMemo(() => Array.from({ length: 12 }, () => 28 + Math.random() * 62), []);
  return (
    <div className="w-full flex items-end gap-2 sm:gap-3 px-1" style={{ height }}>
      {bars.map((h, i) => (
        <div
          key={i}
          className="flex-1 rounded-t-sm bg-muted animate-pulse"
          style={{ height: `${h}%`, animationDelay: `${i * 60}ms` }}
        />
      ))}
    </div>
  );
}

/**
 * Shared height/loading/empty/error shell for every chart. Charts render
 * inside real DOM (not a canvas), so a fixed height across all four states
 * keeps layout stable — no jank when data resolves.
 */
export function ChartContainer({
  height = 220,
  loading,
  isEmpty,
  emptyMessage = "No data for this period",
  error,
  children,
  className,
}: ChartContainerProps) {
  if (loading) {
    return <div className={cn("w-full", className)}><ChartSkeleton height={height} /></div>;
  }

  if (error) {
    return (
      <div className={cn("flex flex-col items-center justify-center gap-2 text-center px-4", className)} style={{ height }}>
        <AlertCircle size={20} className="text-destructive/70" />
        <p className="text-[13px] text-destructive">{error}</p>
      </div>
    );
  }

  if (isEmpty) {
    return (
      <div className={cn("flex flex-col items-center justify-center gap-2.5 text-center px-4", className)} style={{ height }}>
        <div className="h-9 w-9 rounded-full flex items-center justify-center bg-muted">
          <BarChart3 size={16} className="text-muted-foreground/60" />
        </div>
        <p className="text-[13px] text-muted-foreground max-w-[220px]">{emptyMessage}</p>
      </div>
    );
  }

  return (
    <div className={cn("w-full", className)} style={{ height }}>
      <ResponsiveContainer width="100%" height="100%">
        {children}
      </ResponsiveContainer>
    </div>
  );
}
