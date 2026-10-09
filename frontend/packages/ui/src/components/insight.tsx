import * as React from "react";
import { cn } from "../lib/utils";

/* ─────────────────────────────────────────────────────────────────────────
   Analytics building blocks, shared by the institution and platform portals.
   An analytics page here is a list of questions someone actually asks, each
   answered first in a sentence and then by a chart that backs the sentence
   up — never a chart left for the reader to interpret on their own.
   ───────────────────────────────────────────────────────────────────────── */

const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

/** "Mar" for a month in the current year, "Mar ’25" otherwise, so a 12-month axis shows where the year turned. */
export function monthLabel(year: number, month: number): string {
  const name = MONTHS[month - 1] ?? "";
  return year === new Date().getFullYear() ? name : `${name} ’${String(year).slice(2)}`;
}

/** A whole-number percentage, or "0%" when there is nothing to take a share of. */
export function percent(part: number, whole: number): string {
  return whole > 0 ? `${Math.round((part / whole) * 100)}%` : "0%";
}

interface InsightSectionProps {
  /** The question this section answers, as its heading. */
  question: string;
  /** The answer in a sentence or two — the thing to read if nothing else is. */
  answer: React.ReactNode;
  /** Shown instead of the answer while the figures load. */
  loading?: boolean;
  /** A link or small control aligned with the heading. */
  action?: React.ReactNode;
  children?: React.ReactNode;
  className?: string;
}

export function InsightSection({ question, answer, loading, action, children, className }: InsightSectionProps) {
  return (
    <section className={cn("border border-border bg-card p-5 sm:p-6", className)}>
      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
        <h2 className="text-[13px] font-semibold text-muted-foreground">{question}</h2>
        {action && <div className="text-[13px]">{action}</div>}
      </div>
      {loading ? (
        <div className="mt-3 space-y-2" aria-busy="true">
          <div className="skeleton h-5 w-4/5 rounded-none" />
          <div className="skeleton h-5 w-2/5 rounded-none" />
        </div>
      ) : (
        <p className="mt-2 max-w-3xl text-[17px] leading-snug text-foreground sm:text-[19px]">{answer}</p>
      )}
      {children && <div className="mt-5">{children}</div>}
    </section>
  );
}

/** A figure inside an answer sentence — heavier, so the numbers can be picked out of the sentence at a glance. */
export function Figure({ children }: { children: React.ReactNode }) {
  return <strong className="font-bold tabular-nums">{children}</strong>;
}

export interface ShareBarItem {
  label: string;
  value: number;
  /** A few words under the label. */
  note?: string;
}

/**
 * Bars measured against one base — the first item — so "of our approved members, how many did X" reads
 * straight down the list. Each bar's width is its share of the base.
 */
export function ShareBars({ items, color = "var(--brand-primary-500, var(--primary))" }: { items: ShareBarItem[]; color?: string }) {
  const base = items[0]?.value ?? 0;
  return (
    <ul className="space-y-3.5">
      {items.map((item, index) => {
        const share = base > 0 ? Math.min(100, (item.value / base) * 100) : 0;
        return (
          <li key={item.label}>
            <div className="flex items-baseline justify-between gap-3">
              <p className="text-[14px] text-foreground">
                {item.label}
                {item.note && <span className="text-muted-foreground"> · {item.note}</span>}
              </p>
              <p className="shrink-0 text-[14px] font-semibold tabular-nums text-foreground">
                {item.value.toLocaleString()}
                {index > 0 && <span className="ml-1.5 font-normal text-muted-foreground">{percent(item.value, base)}</span>}
              </p>
            </div>
            <div className="mt-1.5 h-2 bg-muted" role="presentation">
              <div className="h-full" style={{ width: `${share}%`, background: color, opacity: index === 0 ? 0.35 : 1 }} />
            </div>
          </li>
        );
      })}
    </ul>
  );
}

/** One count for the last 30 days, with how it compares to the 30 days before, in words. */
export function ChangeStat({ label, current, previous, period = "30 days" }: { label: string; current: number; previous: number; /** What the two numbers cover, e.g. "7 days": the comparison reads "…than the 7 days before". */ period?: string }) {
  const difference = current - previous;
  const comparison = previous === 0 && current === 0
    ? `None in the ${period} before either`
    : difference === 0
      ? `Same as the ${period} before`
      : `${Math.abs(difference).toLocaleString()} ${difference > 0 ? "more" : "fewer"} than the ${period} before`;
  return (
    <div className="border-l-2 border-border pl-4">
      <p className="text-[26px] font-bold leading-none tabular-nums text-foreground">{current.toLocaleString()}</p>
      <p className="mt-1.5 text-[14px] text-foreground">{label}</p>
      <p className={cn("mt-0.5 text-[12.5px]", difference > 0 ? "text-success" : "text-muted-foreground")}>{comparison}</p>
    </div>
  );
}
