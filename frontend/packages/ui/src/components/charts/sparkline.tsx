"use client";

import * as React from "react";
import { AreaChart, Area, ResponsiveContainer } from "recharts";
import { cn } from "../../lib/utils";

interface SparklineProps {
  data: number[];
  color?: string;
  height?: number;
  className?: string;
}

/**
 * A compact inline KPI trend — no axes, no grid, no tooltip. Meant to sit
 * beside a stat card's headline number, not to be read precisely; it exists
 * so every KPI doesn't render as an identical flat number-plus-label block.
 */
export function Sparkline({ data, color = "var(--brand-primary-500, var(--primary))", height = 32, className }: SparklineProps) {
  if (data.length < 2) return null;

  const points = data.map((value, i) => ({ i, value }));
  const gradientId = React.useId();

  return (
    <div className={cn("w-full", className)} style={{ height }}>
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart data={points} margin={{ top: 2, right: 0, left: 0, bottom: 0 }}>
          <defs>
            <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={color} stopOpacity={0.25} />
              <stop offset="100%" stopColor={color} stopOpacity={0} />
            </linearGradient>
          </defs>
          <Area type="monotone" dataKey="value" stroke={color} strokeWidth={1.75} fill={`url(#${gradientId})`} dot={false} isAnimationActive={false} />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  );
}
