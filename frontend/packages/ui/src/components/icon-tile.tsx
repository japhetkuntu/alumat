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
 *
 * Look: a white tile with a hairline border and a solid brand-colour glyph. The one element that matters most in
 * a view can be `filled`: solid brand colour, white glyph, and a soft shadow underneath.
 */
export interface IconTileProps {
  icon: React.ElementType;
  size?: "sm" | "default" | "lg" | "xl";
  tone?: "primary" | "accent" | "muted" | "destructive" | "success" | "warning";
  /** Solid gradient fill (for the one hero element per view) vs a tinted outline (everything else). */
  filled?: boolean;
  className?: string;
}

const SIZES = {
  sm: { box: "w-8 h-8", icon: 15, radius: "rounded-none" },
  default: { box: "w-9 h-9 sm:w-10 sm:h-10", icon: 17, radius: "rounded-none" },
  lg: { box: "w-11 h-11 sm:w-12 sm:h-12", icon: 19, radius: "rounded-none" },
  xl: { box: "w-16 h-16 sm:w-[72px] sm:h-[72px]", icon: 28, radius: "rounded-none" },
} as const;

const TONES: Record<NonNullable<IconTileProps["tone"]>, { bg: string; border: string; fg: string; fill: string }> = {
  // Note: intentionally the *brand* primary/accent tonal scale here, not
  // --color-background-info/--color-border-info — those are the fixed,
  // brand-independent "informational" semantic tokens (see theme.ts), never
  // tenant-colored, so using them for a "primary" icon tile would render a
  // fixed blue for every institution regardless of its actual brand color.
  // Primary and accent are deliberately identical: one icon colour across the product (the brand primary, on a
  // white tile with a hairline border), never alternating between two brand colours.
  primary: { bg: "var(--card)", border: "var(--border-emphasis, var(--border))", fg: "var(--primary)", fill: "var(--primary)" },
  accent: { bg: "var(--card)", border: "var(--border-emphasis, var(--border))", fg: "var(--primary)", fill: "var(--primary)" },
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
              background: t.fill,
              boxShadow: `0 10px 22px -10px color-mix(in oklch, ${t.fill} 55%, transparent)`,
            }
          : {
              background: t.bg,
              border: `1px solid ${t.border}`,
            }
      }
    >
      <Icon size={dims.icon} strokeWidth={2.25} style={{ color: filled ? "var(--primary-foreground)" : t.fg }} />
    </div>
  );
}
