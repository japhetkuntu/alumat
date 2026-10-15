"use client";

import * as React from "react";
import { cn } from "../lib/utils";

/** A titled group of fields inside a long form, so a form with many fields reads as a few clear parts. */
export function FormSection({
  title, hint, children, className,
}: { title: string; hint?: string; children: React.ReactNode; className?: string }) {
  return (
    <section className={cn("space-y-4 border-t border-border pt-5 first:border-t-0 first:pt-0", className)}>
      <div>
        <h3 className="text-[14px] font-bold leading-tight">{title}</h3>
        {hint && <p className="mt-0.5 text-[13px] leading-relaxed text-muted-foreground">{hint}</p>}
      </div>
      {children}
    </section>
  );
}

/** One yes/no choice: the whole row is the tap target, with the effect spelled out underneath. */
export function CheckOption({
  id, checked, onChange, label, hint,
}: { id: string; checked: boolean; onChange: (v: boolean) => void; label: string; hint?: string }) {
  return (
    <label htmlFor={id} className="flex cursor-pointer items-start gap-3 border border-border bg-card p-3 transition-colors hover:bg-muted/40 has-[:checked]:border-primary/50 has-[:checked]:bg-primary/5">
      <input
        id={id}
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="mt-0.5 size-4 shrink-0 accent-[var(--primary)]"
      />
      <span className="min-w-0">
        <span className="block text-[14px] font-semibold leading-snug">{label}</span>
        {hint && <span className="mt-0.5 block text-[13px] leading-relaxed text-muted-foreground">{hint}</span>}
      </span>
    </label>
  );
}
