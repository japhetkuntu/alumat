"use client";

import type { ReactNode } from "react";
import { Badge, cn, formatCurrency } from "@alumni/ui";
import type { ServiceFieldDefinition, StoreDetailItem, StoreProductVariant } from "@/types";

const TYPE_LABELS: Record<string, string> = {
  Text: "Short answer", TextArea: "Long answer", Number: "Number", Date: "Date", Select: "Dropdown", File: "File upload",
};

/** A titled block inside an opened item: what the admin set up, laid out to read rather than to edit. */
export function DetailSection({ title, hint, children }: { title: string; hint?: string; children: ReactNode }) {
  return (
    <section className="space-y-2">
      <div>
        <h4 className="text-[12px] font-bold uppercase tracking-wider text-muted-foreground">{title}</h4>
        {hint && <p className="text-[12px] text-muted-foreground">{hint}</p>}
      </div>
      {children}
    </section>
  );
}

/** The questions an item asks, in order, with the kind of answer, whether it is required, and the choices for a dropdown. */
export function QuestionView({ fields }: { fields: ServiceFieldDefinition[] }) {
  return (
    // A long list scrolls inside its own box, so one item with dozens of questions does not push everything else off the page.
    <ol className={cn("divide-y divide-border border border-border bg-card", fields.length > 8 && "max-h-[420px] overflow-y-auto")} tabIndex={fields.length > 8 ? 0 : undefined} aria-label={fields.length > 8 ? "Questions" : undefined}>
      {fields.map((f, i) => (
        <li key={f.key ?? `${f.label}-${i}`} className="flex gap-2.5 px-3 py-2.5 text-[13px]">
          <span className="w-5 shrink-0 pt-px tabular-nums text-muted-foreground">{i + 1}</span>
          <div className="min-w-0 flex-1 space-y-1.5">
            <p className="break-words font-medium">{f.label}</p>
            <div className="flex flex-wrap gap-1.5">
              <Badge variant="secondary" size="sm">{TYPE_LABELS[f.type] ?? f.type}</Badge>
              <Badge variant={f.required ? "warning" : "outline"} size="sm">{f.required ? "Required" : "Optional"}</Badge>
            </div>
            {f.helpText && <p className="break-words text-[12px] text-muted-foreground">{f.helpText}</p>}
            {f.type === "Select" && (f.options?.length ?? 0) > 0 && (
              <p className="break-words text-[12px] text-muted-foreground">Choices: {f.options!.join(", ")}</p>
            )}
          </div>
        </li>
      ))}
    </ol>
  );
}

/** Fulfilment stages as the path a request or order follows. */
export function StageList({ stages }: { stages: string[] }) {
  return (
    <ol className="flex flex-wrap items-center gap-x-1.5 gap-y-2 text-[13px]">
      {stages.map((s, i) => (
        <li key={`${s}-${i}`} className="flex items-center gap-1.5">
          <span className="border border-border bg-card px-2.5 py-1 font-medium">{s}</span>
          {i < stages.length - 1 && <span aria-hidden className="text-muted-foreground">→</span>}
        </li>
      ))}
    </ol>
  );
}

/** Label and value pairs, such as the specification lines of a store product. */
export function FactList({ items }: { items: StoreDetailItem[] }) {
  return (
    <dl className="divide-y divide-border border border-border bg-card text-[13px]">
      {items.map((d, i) => (
        <div key={`${d.label}-${i}`} className="grid grid-cols-[minmax(0,40%)_1fr] gap-3 px-3 py-2">
          <dt className="text-muted-foreground">{d.label}</dt>
          <dd className="font-medium">{d.value}</dd>
        </div>
      ))}
    </dl>
  );
}

/** Each option combination of a product with its price and what is left. */
export function VariantTable({ optionTypes, variants, trackStock }: { optionTypes: string[]; variants: StoreProductVariant[]; trackStock: boolean }) {
  return (
    <div className="overflow-x-auto border border-border bg-card">
      <table className="w-full text-left text-[13px]">
        <thead className="bg-muted/40 text-[12px] text-muted-foreground">
          <tr>
            {optionTypes.map((t) => <th key={t} className="px-3 py-2 font-semibold">{t}</th>)}
            <th className="px-3 py-2 font-semibold">Price</th>
            {trackStock && <th className="px-3 py-2 font-semibold">In stock</th>}
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {variants.map((v) => (
            <tr key={v.id}>
              {optionTypes.map((t) => <td key={t} className="whitespace-nowrap px-3 py-2">{v.options[t] ?? "—"}</td>)}
              <td className="whitespace-nowrap px-3 py-2 tabular-nums">{formatCurrency(v.price)}</td>
              {trackStock && (
                <td className={cn("px-3 py-2 tabular-nums", v.quantityAvailable === 0 && "font-semibold text-destructive")}>{v.quantityAvailable}</td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
