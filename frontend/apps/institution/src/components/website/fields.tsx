"use client";

import { cn } from "@alumni/ui";
import type { ReactNode } from "react";

export const inputCls = "h-9 w-full border border-input bg-background px-2.5 text-[13px] outline-none focus:border-primary focus:ring-1 focus:ring-primary/30 disabled:opacity-50";

export function Field({ label, hint, children, className }: { label?: string; hint?: ReactNode; children: ReactNode; className?: string }) {
  return (
    <div className={cn("space-y-1.5", className)}>
      {label && <div className="text-[12px] font-semibold text-muted-foreground">{label}</div>}
      {children}
      {hint && <p className="m-0 text-[12px] leading-snug text-muted-foreground">{hint}</p>}
    </div>
  );
}

export function Segmented<T extends string>({ value, onChange, options, label }: { value: T; onChange: (v: T) => void; options: { value: T; label: string }[]; label: string }) {
  return (
    <div role="group" aria-label={label} className="flex border border-border">
      {options.map((o, i) => (
        <button key={o.value} type="button" aria-pressed={value === o.value} onClick={() => onChange(o.value)}
          className={cn("min-h-9 flex-1 px-2 text-[12.5px] font-medium transition-colors", i > 0 && "border-l border-border", value === o.value ? "bg-foreground text-background" : "bg-background hover:bg-muted")}>
          {o.label}
        </button>
      ))}
    </div>
  );
}

export function Check({ checked, onChange, children, hint, disabled }: { checked: boolean; onChange: (v: boolean) => void; children: ReactNode; hint?: ReactNode; disabled?: boolean }) {
  return (
    <label className={cn("flex items-start gap-2.5 text-[13px]", disabled && "opacity-55")}>
      <input type="checkbox" className="mt-0.5 h-4 w-4 accent-[var(--primary)]" checked={checked} disabled={disabled} onChange={(e) => onChange(e.target.checked)} />
      <span className="min-w-0"><span className="font-medium">{children}</span>{hint && <span className="mt-0.5 block text-[12px] leading-snug text-muted-foreground">{hint}</span>}</span>
    </label>
  );
}

export function Group({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn("space-y-3 border border-border bg-muted/30 p-3", className)}>{children}</div>;
}

export function PanelTitle({ children }: { children: ReactNode }) {
  return <h2 className="m-0 mb-2 text-[11px] font-bold uppercase tracking-[0.08em] text-muted-foreground">{children}</h2>;
}
