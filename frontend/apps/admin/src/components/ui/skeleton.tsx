import { cn } from "@/lib/utils";

interface SkeletonProps extends React.HTMLAttributes<HTMLDivElement> {
  variant?: "text" | "circular" | "rectangular";
}

export function Skeleton({ className, variant = "rectangular", ...props }: SkeletonProps) {
  return (
    <div
      className={cn(
        "skeleton",
        variant === "circular" && "rounded-full",
        variant === "text" && "h-4 rounded-[6px]",
        variant === "rectangular" && "rounded-[10px]",
        className
      )}
      {...props}
    />
  );
}

export function CardSkeleton({ className }: { className?: string }) {
  return (
    <div className={cn("rounded-[16px] border border-border/40 bg-card p-5 space-y-3", className)}>
      <div className="flex items-center gap-3">
        <Skeleton className="h-10 w-10 rounded-full" variant="circular" />
        <div className="space-y-2 flex-1">
          <Skeleton className="h-4 w-1/3" variant="text" />
          <Skeleton className="h-3 w-1/2" variant="text" />
        </div>
      </div>
      <Skeleton className="h-3 w-full" variant="text" />
      <Skeleton className="h-3 w-4/5" variant="text" />
    </div>
  );
}

export function StatSkeleton() {
  return (
    <div className="rounded-[16px] border border-border/40 bg-card p-6">
      <div className="flex items-start justify-between">
        <div className="space-y-2 flex-1">
          <Skeleton className="h-3 w-20" variant="text" />
          <Skeleton className="h-7 w-16" variant="text" />
          <Skeleton className="h-3 w-24" variant="text" />
        </div>
        <Skeleton className="h-10 w-10 rounded-[10px]" />
      </div>
    </div>
  );
}

export function TableSkeleton({ rows = 5, cols = 4 }: { rows?: number; cols?: number }) {
  return (
    <>
      {Array.from({ length: rows }).map((_, r) => (
        <tr key={r} className="border-b border-border/40 last:border-0">
          {Array.from({ length: cols }).map((_, c) => (
            <td key={c} className="px-4 h-[52px]">
              <Skeleton className="h-3 w-full" variant="text" />
            </td>
          ))}
        </tr>
      ))}
    </>
  );
}
