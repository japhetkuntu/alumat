import * as React from "react";
import { cn } from "../lib/utils";

interface EmptyStateProps {
  icon?: React.ReactNode;
  title: string;
  description?: string;
  action?: React.ReactNode;
  className?: string;
}

export function EmptyState({ icon, title, description, action, className }: EmptyStateProps) {
  return (
    <div className={cn("flex flex-col items-center justify-center py-16 px-6 text-center", className)}>
      {icon && (
        <div
          className="w-16 h-16 rounded-2xl flex items-center justify-center mb-5 [&>svg]:text-primary"
          style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
        >
          {icon}
        </div>
      )}
      <h3 className="font-[family-name:var(--font-display)] text-[16px] font-semibold" style={{ color: "var(--foreground)" }}>
        {title}
      </h3>
      {description && (
        <p className="mt-2 text-[13.5px] max-w-[340px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
          {description}
        </p>
      )}
      {action && <div className="mt-5">{action}</div>}
    </div>
  );
}
