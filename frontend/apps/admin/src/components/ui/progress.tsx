import * as React from "react";
import { cn } from "@/lib/utils";

interface ProgressProps extends React.HTMLAttributes<HTMLDivElement> {
  value?: number;
  colorByValue?: boolean;
}

function getBarColor(value: number, colorByValue: boolean) {
  if (!colorByValue) return "bg-primary";
  if (value >= 66) return "bg-emerald-500";
  if (value >= 33) return "bg-amber-500";
  return "bg-destructive";
}

const Progress = React.forwardRef<HTMLDivElement, ProgressProps>(
  ({ className, value = 0, colorByValue = false, ...props }, ref) => {
    const clamped = Math.min(Math.max(value, 0), 100);
    return (
      <div ref={ref} className={cn("relative h-2 w-full overflow-hidden rounded-full bg-primary/10", className)} {...props}>
        <div
          className={cn("h-full rounded-full transition-all duration-500 ease-out", getBarColor(clamped, colorByValue))}
          style={{ width: `${clamped}%` }}
        />
      </div>
    );
  }
);
Progress.displayName = "Progress";

export { Progress };
