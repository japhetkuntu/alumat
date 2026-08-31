"use client";

import * as React from "react";
import { cn } from "../lib/utils";
import { IconTile } from "./icon-tile";

export interface StatCardProps {
  icon: React.ElementType;
  label: string;
  value: React.ReactNode;
  sub?: React.ReactNode;
  /** "accent" pulls from the institution's secondary/brand-accent color instead
   *  of primary — alternate tones across a stat row so both brand colors show. */
  tone?: "primary" | "accent";
  /** "hero" makes this the one dominant metric on a dashboard — larger value
   *  type, filled icon badge, emphasis border — everything else on the row
   *  should stay "default" so exactly one card wins the eye. */
  variant?: "default" | "hero";
  className?: string;
}

export function StatCard({ icon, label, value, sub, tone = "primary", variant = "default", className }: StatCardProps) {
  const isHero = variant === "hero";

  return (
    <div
      className={cn(
        "border bg-card flex flex-col gap-3 min-w-0 shadow-[var(--card-shadow)] transition-shadow hover:shadow-[var(--card-shadow-hover)]",
        isHero ? "border-border-emphasis p-5 sm:p-6" : "border-border p-4 sm:p-5",
        className
      )}
    >
      <IconTile icon={icon} tone={tone} size={isHero ? "lg" : "default"} filled={isHero} />

      <p
        className="font-[family-name:var(--font-display)] font-bold leading-none tabular-nums text-foreground"
        style={{
          fontSize: isHero ? "clamp(1.75rem, 4.6vw, 2.5rem)" : "clamp(1.15rem, 3.2vw, 1.6rem)",
          letterSpacing: "-0.02em",
          wordBreak: "break-word",
          overflowWrap: "anywhere",
        }}
      >
        {value}
      </p>

      <div className={cn("border-t", isHero ? "pt-3 border-border-emphasis" : "pt-2.5 border-border")}>
        <p className={cn("font-semibold leading-tight text-foreground line-clamp-2", isHero ? "text-[13.5px] sm:text-[14.5px]" : "text-[12.5px] sm:text-[13px]")}>{label}</p>
        {sub && <div className="text-[11px] sm:text-[11.5px] mt-1 text-muted-foreground leading-snug line-clamp-2">{sub}</div>}
      </div>
    </div>
  );
}

export function StatCardSkeleton({ variant = "default" }: { variant?: "default" | "hero" }) {
  const isHero = variant === "hero";
  return (
    <div className={cn("border bg-card flex flex-col gap-3", isHero ? "border-border-emphasis p-5 sm:p-6" : "border-border p-4 sm:p-5")}>
      <div className={cn("skeleton", isHero ? "w-11 h-11 sm:w-12 sm:h-12" : "w-9 h-9 sm:w-10 sm:h-10")} />
      <div className={cn("skeleton", isHero ? "h-9 w-28" : "h-6 w-16")} />
      <div className={cn("border-t space-y-1.5", isHero ? "pt-3 border-border-emphasis" : "pt-2.5 border-border")}>
        <div className="skeleton h-3.5 w-24" />
        <div className="skeleton h-3 w-32" />
      </div>
    </div>
  );
}
