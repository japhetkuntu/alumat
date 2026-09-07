import * as React from "react";
import { cn } from "../lib/utils";

interface ProgressProps extends React.HTMLAttributes<HTMLDivElement> {
  value?: number;
  colorByValue?: boolean;
  /** "primary" (default) for a goal directly tied to the main CTA (e.g. a
   *  fundraising target); "accent" for a supporting/secondary metric (e.g.
   *  profile completeness, engagement) — gives progress bars the same
   *  primary-vs-accent choice as the rest of the system instead of every
   *  meter defaulting to primary regardless of what it measures. */
  tone?: "primary" | "accent";
}

function getBarColor(value: number, colorByValue: boolean, tone: "primary" | "accent") {
  if (!colorByValue) return tone === "accent" ? "bg-accent" : "bg-primary";
  if (value >= 66) return "bg-emerald-500";
  if (value >= 33) return "bg-amber-500";
  return "bg-destructive";
}

const Progress = React.forwardRef<HTMLDivElement, ProgressProps>(
  ({ className, value = 0, colorByValue = false, tone = "primary", ...props }, ref) => {
    const clamped = Math.min(Math.max(value, 0), 100);
    return (
      <div ref={ref} className={cn("relative h-2 w-full overflow-hidden", tone === "accent" ? "bg-accent/10" : "bg-primary/10", className)} {...props}>
        <div
          className={cn("h-full transition-all duration-500 ease-out", getBarColor(clamped, colorByValue, tone))}
          style={{ width: `${clamped}%` }}
        />
      </div>
    );
  }
);
Progress.displayName = "Progress";

export { Progress };
