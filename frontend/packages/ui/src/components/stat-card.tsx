"use client";

import * as React from "react";
import { cn } from "../lib/utils";

export interface StatCardProps {
  icon: React.ElementType;
  label: string;
  value: React.ReactNode;
  sub?: React.ReactNode;
  /** "accent" pulls from the institution's secondary/brand-accent color instead
   *  of primary — alternate tones across a stat row so both brand colors show. */
  tone?: "primary" | "accent";
  className?: string;
}

export function StatCard({ icon: Icon, label, value, sub, tone = "primary", className }: StatCardProps) {
  const iconBg = tone === "accent" ? "var(--brand-accent-light, var(--color-background-info))" : "var(--color-background-info)";
  const iconBorder = tone === "accent" ? "var(--brand-accent, var(--color-border-info))" : "var(--color-border-info)";
  const iconColor = tone === "accent" ? "var(--brand-accent-dark, var(--brand-accent, var(--primary)))" : "var(--primary)";

  return (
    <div
      className={cn(
        "rounded-2xl border border-border bg-card p-4 sm:p-5 flex flex-col gap-3 min-w-0 shadow-[var(--card-shadow)] transition-shadow hover:shadow-[var(--card-shadow-hover)]",
        className
      )}
    >
      <div
        className="w-9 h-9 sm:w-10 sm:h-10 rounded-xl flex items-center justify-center shrink-0"
        style={{ background: iconBg, border: `1px solid ${iconBorder}` }}
      >
        <Icon size={16} style={{ color: iconColor }} />
      </div>

      <p
        className="font-[family-name:var(--font-display)] font-bold leading-none tabular-nums text-foreground"
        style={{
          fontSize: "clamp(1.15rem, 3.2vw, 1.6rem)",
          letterSpacing: "-0.02em",
          wordBreak: "break-word",
          overflowWrap: "anywhere",
        }}
      >
        {value}
      </p>

      <div className="pt-2.5 border-t border-border">
        <p className="text-[12.5px] sm:text-[13px] font-semibold leading-tight text-foreground line-clamp-2">{label}</p>
        {sub && <div className="text-[11px] sm:text-[11.5px] mt-1 text-muted-foreground leading-snug line-clamp-2">{sub}</div>}
      </div>
    </div>
  );
}

export function StatCardSkeleton() {
  return (
    <div className="rounded-2xl border border-border bg-card p-4 sm:p-5 flex flex-col gap-3">
      <div className="skeleton w-9 h-9 sm:w-10 sm:h-10 rounded-xl" />
      <div className="skeleton h-6 w-16 rounded-[8px]" />
      <div className="pt-2.5 border-t border-border space-y-1.5">
        <div className="skeleton h-3.5 w-24 rounded-[6px]" />
        <div className="skeleton h-3 w-32 rounded-[6px]" />
      </div>
    </div>
  );
}
