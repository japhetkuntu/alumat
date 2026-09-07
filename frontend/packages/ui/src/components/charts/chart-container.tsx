"use client";

import * as React from "react";
import { ResponsiveContainer } from "recharts";
import { Skeleton } from "../skeleton";
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

/**
 * Shared height/loading/empty/error shell for every chart. Charts render
 * inside real DOM (not a canvas), so a plain Skeleton and centered message
 * over the same fixed height keeps layout stable across the three states —
 * no jank when data resolves.
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
    return <Skeleton className={cn("w-full", className)} style={{ height }} />;
  }

  if (error) {
    return (
      <div className={cn("flex items-center justify-center", className)} style={{ height }}>
        <p className="text-[13px] text-destructive">{error}</p>
      </div>
    );
  }

  if (isEmpty) {
    return (
      <div className={cn("flex items-center justify-center", className)} style={{ height }}>
        <p className="text-[13px] text-muted-foreground">{emptyMessage}</p>
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
