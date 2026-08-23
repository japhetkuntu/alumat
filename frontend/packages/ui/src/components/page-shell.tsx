"use client";

import { cn } from "../lib/utils";

interface PageHeaderProps {
  /** Small muted label above the title, e.g. "Connect in person", "Account". */
  eyebrow?: string;
  title: string;
  description?: string;
  /** Right-aligned action(s), e.g. a button. */
  children?: React.ReactNode;
  className?: string;
}

export function PageHeader({ eyebrow, title, description, children, className }: PageHeaderProps) {
  return (
    <div className={cn("flex flex-wrap items-start justify-between gap-4", className)}>
      <div>
        {eyebrow && (
          <p className="text-[12.5px] font-medium text-muted-foreground mb-1">{eyebrow}</p>
        )}
        <h1
          className="font-[family-name:var(--font-display)] tracking-tight"
          style={{ fontSize: "clamp(1.5rem, 3vw, 2rem)", fontWeight: 700, color: "var(--foreground)" }}
        >
          {title}
        </h1>
        {description && (
          <p className="mt-1 text-[14px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
            {description}
          </p>
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
