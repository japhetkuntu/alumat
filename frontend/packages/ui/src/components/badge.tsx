import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "../lib/utils";

const badgeVariants = cva(
  "inline-flex items-center border px-2 py-1 text-[10px] font-bold uppercase tracking-[.08em] transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2",
  {
    variants: {
      variant: {
        default: "border-primary/40 text-primary hover:bg-primary/5",
        secondary: "border-border text-secondary-foreground hover:bg-secondary/40",
        destructive: "border-destructive/40 text-destructive hover:bg-destructive/10",
        outline: "text-foreground border-border/60 hover:bg-accent hover:text-accent-foreground",
        success: "border-success/40 text-success hover:bg-success/10",
        warning: "border-warning/40 text-warning hover:bg-warning/10",
        info: "border-info/40 text-info hover:bg-info/10",
        neutral: "border-border text-muted-foreground hover:bg-muted/40",
      },
      size: {
        sm: "px-1.5 py-0 text-[9px] h-[18px]",
        default: "px-2 py-1 text-[10px] h-[22px]",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, size, ...props }: BadgeProps) {
  // Renders as <span> (not <div>) so it stays valid nested inside <p>/<h1>-<h6>,
  // which are phrasing-content-only elements — a common spot to want a badge.
  return <span className={cn(badgeVariants({ variant, size }), className)} {...props} />;
}

export { Badge, badgeVariants };
