"use client";

import * as React from "react";
import { cn } from "../lib/utils";

/**
 * The icon container used on stat cards, empty states, and any card that
 * leads with an icon. A flat 1px-outline square in a 0-radius design system
 * reads as an unstyled placeholder once there's an icon glyph floating
 * inside it — so this is one of the few places that intentionally breaks
 * from --card-radius: 0 with a small radius of its own, plus a soft inset
 * highlight for a touch of depth instead of a dead-flat tint.
 */
export interface IconTileProps {
  icon: React.ElementType;
  size?: "sm" | "default" | "lg";
  tone?: "primary" | "accent" | "muted" | "destructive" | "success" | "warning";
  /** Solid gradient fill (for the one hero element per view) vs a tinted outline (everything else). */
  filled?: boolean;
  className?: string;
}

const SIZES = {
  sm: { box: "w-8 h-8", icon: 15, radius: "rounded-[9px]" },
  default: { box: "w-9 h-9 sm:w-10 sm:h-10", icon: 17, radius: "rounded-[10px]" },
  lg: { box: "w-11 h-11 sm:w-12 sm:h-12", icon: 19, radius: "rounded-[12px]" },
} as const;

const TONES: Record<NonNullable<IconTileProps["tone"]>, { bg: string; border: string; fg: string; fill: string }> = {
  primary: { bg: "var(--color-background-info)", border: "var(--color-border-info)", fg: "var(--primary)", fill: "var(--primary)" },
  accent: { bg: "var(--brand-accent-light, var(--color-background-info))", border: "var(--brand-accent, var(--color-border-info))", fg: "var(--brand-accent-dark, var(--brand-accent, var(--primary)))", fill: "var(--brand-accent, var(--primary))" },
  muted: { bg: "var(--muted)", border: "var(--border)", fg: "var(--muted-foreground)", fill: "var(--muted-foreground)" },
  destructive: { bg: "color-mix(in oklch, var(--destructive) 10%, var(--card))", border: "color-mix(in oklch, var(--destructive) 28%, transparent)", fg: "var(--destructive)", fill: "var(--destructive)" },
  success: { bg: "color-mix(in oklch, var(--success) 12%, var(--card))", border: "color-mix(in oklch, var(--success) 28%, transparent)", fg: "var(--success)", fill: "var(--success)" },
  warning: { bg: "color-mix(in oklch, var(--warning) 14%, var(--card))", border: "color-mix(in oklch, var(--warning) 30%, transparent)", fg: "var(--warning)", fill: "var(--warning)" },
};

export function IconTile({ icon: Icon, size = "default", tone = "primary", filled = false, className }: IconTileProps) {
  const dims = SIZES[size];
  const t = TONES[tone];

  return (
    <div
      className={cn("flex items-center justify-center shrink-0", dims.box, dims.radius, className)}
      style={
        filled
          ? {
              background: `linear-gradient(155deg, color-mix(in oklch, ${t.fill} 82%, white), ${t.fill})`,
              boxShadow: "inset 0 1px 0 rgba(255,255,255,0.22), 0 1px 3px rgba(0,0,0,0.08)",
            }
          : {
              background: t.bg,
              border: `1px solid ${t.border}`,
              boxShadow: "inset 0 1px 0 rgba(255,255,255,0.35)",
            }
      }
    >
      <Icon size={dims.icon} strokeWidth={2.25} style={{ color: filled ? "var(--primary-foreground)" : t.fg }} />
    </div>
  );
}
