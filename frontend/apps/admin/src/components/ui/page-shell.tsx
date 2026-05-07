"use client";

import { cn } from "@/lib/utils";

interface PageHeaderProps {
  title: string;
  description?: string;
  children?: React.ReactNode;
  className?: string;
}

export function PageHeader({ title, description, children, className }: PageHeaderProps) {
  return (
    <div className={cn("flex items-center justify-between gap-4", className)}>
      <div>
        <h1 className="text-[22px] font-bold tracking-tight text-foreground">{title}</h1>
        {description && (
          <p className="text-[13px] text-muted-foreground mt-1 leading-relaxed">{description}</p>
        )}
      </div>
      {children && <div className="flex items-center gap-2 shrink-0">{children}</div>}
    </div>
  );
}

interface PageShellProps {
  children: React.ReactNode;
  className?: string;
}

export function PageShell({ children, className }: PageShellProps) {
  return (
    <div className={cn("p-6 lg:p-8 space-y-6 page-enter", className)}>
      {children}
    </div>
  );
}
