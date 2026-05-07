import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex items-center rounded-full border px-2.5 py-0.5 text-[11px] font-semibold tracking-wide transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2",
  {
    variants: {
      variant: {
        default:
          "border-transparent bg-primary/10 text-primary hover:bg-primary/20",
        secondary:
          "border-transparent bg-secondary text-secondary-foreground hover:bg-secondary/80",
        destructive:
          "border-transparent bg-destructive/10 text-destructive hover:bg-destructive/20",
        outline: "text-foreground border-border/60 hover:bg-accent hover:text-accent-foreground",
        success: 
          "border-transparent bg-success/10 text-success hover:bg-success/20",
        warning: 
          "border-transparent bg-warning/10 text-warning hover:bg-warning/20",
        info: 
          "border-transparent bg-info/10 text-info hover:bg-info/20",
        neutral: 
          "border-transparent bg-muted text-muted-foreground hover:bg-muted/80",
      },
      size: {
        sm: "px-2 py-0 text-[10px] h-[18px]",
        default: "px-2.5 py-0.5 text-[11px] h-[22px]",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, size, ...props }: BadgeProps) {
  return <div className={cn(badgeVariants({ variant, size }), className)} {...props} />;
}

export { Badge, badgeVariants };
