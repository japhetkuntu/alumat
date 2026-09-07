"use client";

import * as React from "react";
import { cn } from "../lib/utils";

export interface StatCardProps {
  /** Accepted for backward compatibility with existing call sites — no
   *  longer rendered. Dashboard stat cards show no icon (see the dashboard
   *  redesign that removed them to reclaim space for long amounts/labels). */
  icon?: React.ElementType;
  label: string;
  value: React.ReactNode;
  sub?: React.ReactNode;
  /** "accent" pulls from the institution's secondary/brand-accent color instead
   *  of primary — alternate tones across a stat row so both brand colors show.
   *  Now only affects the label's small accent rule, since the icon (the
   *  previous carrier of tone) is gone. */
  tone?: "primary" | "accent";
  /** "hero" makes this the one dominant metric on a dashboard — larger value
   *  type, emphasis border — everything else on the row should stay
   *  "default" so exactly one card wins the eye. */
  variant?: "default" | "hero";
  className?: string;
}

export function StatCard({ label, value, sub, tone = "primary", variant = "default", className }: StatCardProps) {
  const isHero = variant === "hero";

  return (
    <div
      className={cn(
        "border bg-card flex flex-col gap-2 min-w-0 shadow-[var(--card-shadow)] transition-shadow hover:shadow-[var(--card-shadow-hover)]",
        isHero ? "border-border-emphasis p-5 sm:p-6" : "border-border p-4 sm:p-5",
        className
      )}
    >
      <p
        className="font-[family-name:var(--font-display)] font-bold text-foreground min-w-0"
        style={{
          fontSize: isHero ? "clamp(1.4rem, 3.6vw, 2.1rem)" : "clamp(1rem, 2.6vw, 1.35rem)",
          lineHeight: 1.15,
          letterSpacing: "-0.02em",
          wordBreak: "break-word",
          overflowWrap: "anywhere",
        }}
      >
        {value}
      </p>

      <div className={cn("border-t min-w-0", isHero ? "pt-3 border-border-emphasis" : "pt-2.5 border-border")}>
        <p
          className={cn("font-semibold leading-tight text-foreground line-clamp-2 break-words", isHero ? "text-[13.5px] sm:text-[14.5px]" : "text-[12.5px] sm:text-[13px]")}
          style={tone === "accent" ? { color: "var(--brand-accent-dark, var(--brand-accent, var(--foreground)))" } : undefined}
        >
          {label}
        </p>
        {sub && <div className="text-[11px] sm:text-[11.5px] mt-1 text-muted-foreground leading-snug line-clamp-2 break-words">{sub}</div>}
      </div>
    </div>
  );
}

export function StatCardSkeleton({ variant = "default" }: { variant?: "default" | "hero" }) {
  const isHero = variant === "hero";
  return (
    <div className={cn("border bg-card flex flex-col gap-2", isHero ? "border-border-emphasis p-5 sm:p-6" : "border-border p-4 sm:p-5")}>
      <div className={cn("skeleton", isHero ? "h-9 w-28" : "h-6 w-16")} />
      <div className={cn("border-t space-y-1.5", isHero ? "pt-3 border-border-emphasis" : "pt-2.5 border-border")}>
        <div className="skeleton h-3.5 w-24" />
        <div className="skeleton h-3 w-32" />
      </div>
    </div>
  );
}
