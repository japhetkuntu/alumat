import * as React from "react";
import { cn } from "@/lib/utils";

interface EmptyStateProps {
  icon?: React.ReactNode;
  title: string;
  description?: string;
  action?: React.ReactNode;
  className?: string;
}

export function EmptyState({ icon, title, description, action, className }: EmptyStateProps) {
  return (
    <div className={cn("flex flex-col items-center justify-center py-16 px-4 text-center", className)}>
      {icon && <div className="mb-4 text-muted-foreground/40">{icon}</div>}
      <h3 className="text-[14px] font-medium text-muted-foreground">{title}</h3>
      {description && (
        <p className="mt-1.5 text-[13px] text-muted-foreground/70 max-w-[320px] leading-relaxed">{description}</p>
      )}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}
