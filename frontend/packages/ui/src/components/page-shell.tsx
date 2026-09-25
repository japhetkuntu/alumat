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
    <div className={cn("space-y-1.5", className)}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          {eyebrow && (
            <p className="text-[12.5px] font-medium text-muted-foreground mb-1">{eyebrow}</p>
          )}
          <h1
            className="tracking-tight"
            style={{ fontSize: "clamp(1.5rem, 3vw, 2rem)", fontWeight: 700, color: "var(--foreground)" }}
          >
            {title}
          </h1>
        </div>
        {/* Stays on the title row, even on a phone, instead of dropping under a long description. */}
        {children && <div className="flex shrink-0 flex-wrap items-center justify-end gap-2">{children}</div>}
      </div>
      {description && (
        <p className="text-[14px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
          {description}
        </p>
      )}
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
