"use client";

import { Plus, X, ArrowUp, ArrowDown } from "@alumni/ui";
import { Button, Input, Label, FormSelect } from "@alumni/ui";
import { TemplatePicker } from "@/components/institution/template-picker";
import type { StoreProductConfigBody } from "@/lib/institution-api";
import type { ServiceFieldDefinition, StoreDetailItem, StoreProduct, StoreProductTemplate } from "@/types";

type FieldType = ServiceFieldDefinition["type"];

/** One editable question row. `key` is kept from the saved field so past orders stay matched to it. */
export interface FieldRow {
  key?: string;
  label: string;
  type: FieldType;
  required: boolean;
  optionsRaw: string;
  helpText: string;
}

/** Everything an institution can configure about one product, in the shape the form edits. */
export interface ConfigDraft {
  priceLabel: string;
  trackStock: boolean;
  details: StoreDetailItem[];
  fields: FieldRow[];
  deliveryFields: FieldRow[];
  stages: string[];
}

export const emptyConfigDraft: ConfigDraft = { priceLabel: "", trackStock: true, details: [], fields: [], deliveryFields: [], stages: [] };

const FIELD_TYPES: { value: FieldType; label: string }[] = [
  { value: "Text", label: "Short answer" },
  { value: "TextArea", label: "Long answer" },
  { value: "Number", label: "Number" },
  { value: "Date", label: "Date" },
  { value: "Select", label: "Choose one" },
  { value: "File", label: "File upload" },
];

const toRows = (fields?: ServiceFieldDefinition[]): FieldRow[] =>
  (fields ?? []).map((f) => ({ key: f.key, label: f.label, type: f.type, required: f.required, optionsRaw: (f.options ?? []).join(", "), helpText: f.helpText ?? "" }));

export function configFromProduct(p: StoreProduct): ConfigDraft {
  return {
    priceLabel: p.priceLabel ?? "",
    trackStock: p.trackStock ?? true,
    details: (p.details ?? []).map((d) => ({ ...d })),
    fields: toRows(p.fields),
    deliveryFields: toRows(p.deliveryFields),
    stages: [...(p.stages ?? [])],
  };
}

/** Copy of a saved setup. Keys are dropped so each product's questions get fresh keys of their own. */
export function configFromTemplate(t: StoreProductTemplate): ConfigDraft {
  const strip = (rows: FieldRow[]) => rows.map(({ key: _key, ...rest }) => rest);
  return {
    priceLabel: t.priceLabel ?? "",
    trackStock: t.trackStock,
    details: t.details.map((d) => ({ ...d })),
    fields: strip(toRows(t.fields)),
    deliveryFields: strip(toRows(t.deliveryFields)),
    stages: [...t.stages],
  };
}

const rowsToBody = (rows: FieldRow[]) =>
  rows
    .filter((r) => r.label.trim() !== "")
    .map((r) => ({
      key: r.key,
      label: r.label.trim(),
      type: r.type,
      required: r.required,
      options: r.type === "Select" ? r.optionsRaw.split(",").map((o) => o.trim()).filter(Boolean) : undefined,
      helpText: r.helpText.trim() || undefined,
    }));

export function configToBody(c: ConfigDraft): StoreProductConfigBody {
  return {
    priceLabel: c.priceLabel.trim() || undefined,
    trackStock: c.trackStock,
    details: c.details.filter((d) => d.label.trim() && d.value.trim()),
    fields: rowsToBody(c.fields),
    deliveryFields: rowsToBody(c.deliveryFields),
    stages: c.stages.map((s) => s.trim()).filter(Boolean),
  };
}

function move<T>(list: T[], i: number, dir: -1 | 1): T[] {
  const j = i + dir;
  if (j < 0 || j >= list.length) return list;
  const next = [...list];
  [next[i], next[j]] = [next[j], next[i]];
  return next;
}

export function QuestionList({ rows, onChange, addLabel, emptyHint }: { rows: FieldRow[]; onChange: (rows: FieldRow[]) => void; addLabel: string; emptyHint: string }) {
  const patch = (i: number, p: Partial<FieldRow>) => onChange(rows.map((r, idx) => (idx === i ? { ...r, ...p } : r)));
  return (
    <div className="space-y-3">
      {rows.length === 0 && <p className="text-[12px] text-muted-foreground">{emptyHint}</p>}
      {rows.map((r, i) => (
        <div key={i} className="border border-border/60 p-3 space-y-2.5">
          <div className="grid grid-cols-1 sm:grid-cols-[1fr_11rem] gap-2.5">
            <Input aria-label="Question" placeholder="Question, e.g. Check-in date" value={r.label} onChange={(e) => patch(i, { label: e.target.value })} />
            <FormSelect value={r.type} onValueChange={(v) => patch(i, { type: v as FieldType })} options={FIELD_TYPES} />
          </div>
          {r.type === "Select" && (
            <Input aria-label="Choices" placeholder="Choices, comma-separated, e.g. Single room, Double room" value={r.optionsRaw} onChange={(e) => patch(i, { optionsRaw: e.target.value })} />
          )}
          <Input aria-label="Help text" placeholder="Help text (optional)" value={r.helpText} onChange={(e) => patch(i, { helpText: e.target.value })} />
          <div className="flex items-center justify-between gap-2">
            <label className="flex items-center gap-2 text-[12.5px]">
              <input type="checkbox" checked={r.required} onChange={(e) => patch(i, { required: e.target.checked })} />
              Required
            </label>
            <div className="flex items-center gap-1">
              <Button type="button" size="sm" variant="ghost" disabled={i === 0} onClick={() => onChange(move(rows, i, -1))} aria-label="Move up"><ArrowUp size={13} /></Button>
              <Button type="button" size="sm" variant="ghost" disabled={i === rows.length - 1} onClick={() => onChange(move(rows, i, 1))} aria-label="Move down"><ArrowDown size={13} /></Button>
              <Button type="button" size="sm" variant="destructive" onClick={() => onChange(rows.filter((_, idx) => idx !== i))} aria-label="Remove question"><X size={13} /></Button>
            </div>
          </div>
        </div>
      ))}
      <Button type="button" size="sm" variant="outline" onClick={() => onChange([...rows, { label: "", type: "Text", required: false, optionsRaw: "", helpText: "" }])}>
        <Plus size={13} />{addLabel}
      </Button>
    </div>
  );
}

/** The price label, the stock setting and the list of details shown on the product page. */
export function DetailsEditor({ value, onChange }: { value: ConfigDraft; onChange: (next: ConfigDraft) => void }) {
  const set = <K extends keyof ConfigDraft>(k: K, v: ConfigDraft[K]) => onChange({ ...value, [k]: v });
  return (
    <>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label className="text-[12.5px]">Price label</Label>
          <Input placeholder="e.g. per night, per person" value={value.priceLabel} onChange={(e) => set("priceLabel", e.target.value)} />
        </div>
        <label className="flex items-center gap-2 text-[13px] self-end pb-2.5">
          <input type="checkbox" checked={!value.trackStock} onChange={(e) => set("trackStock", !e.target.checked)} />
          Unlimited availability (no stock count)
        </label>
      </div>
      <div className="space-y-2">
        <Label className="text-[12.5px]">Details</Label>
        {value.details.map((d, i) => (
          <div key={i} className="flex items-center gap-2">
            <Input aria-label="Detail name" placeholder="e.g. Location" value={d.label} onChange={(e) => set("details", value.details.map((x, idx) => (idx === i ? { ...x, label: e.target.value } : x)))} />
            <Input aria-label="Detail value" placeholder="e.g. Block A, ground floor" value={d.value} onChange={(e) => set("details", value.details.map((x, idx) => (idx === i ? { ...x, value: e.target.value } : x)))} />
            <Button type="button" size="sm" variant="destructive" aria-label="Remove detail" onClick={() => set("details", value.details.filter((_, idx) => idx !== i))}><X size={13} /></Button>
          </div>
        ))}
        <Button type="button" size="sm" variant="outline" onClick={() => set("details", [...value.details, { label: "", value: "" }])}><Plus size={13} />Add detail</Button>
      </div>
    </>
  );
}

/** An ordered list of fulfilment stages. */
export function StagesEditor({ stages, onChange }: { stages: string[]; onChange: (stages: string[]) => void }) {
  return (
    <div className="space-y-2">
      {stages.map((stage, i) => (
        <div key={i} className="flex items-center gap-2">
          <Input aria-label={`Stage ${i + 1}`} placeholder={`Stage ${i + 1}`} value={stage} onChange={(e) => onChange(stages.map((x, idx) => (idx === i ? e.target.value : x)))} />
          <Button type="button" size="sm" variant="ghost" disabled={i === 0} onClick={() => onChange(move(stages, i, -1))} aria-label="Move up"><ArrowUp size={13} /></Button>
          <Button type="button" size="sm" variant="ghost" disabled={i === stages.length - 1} onClick={() => onChange(move(stages, i, 1))} aria-label="Move down"><ArrowDown size={13} /></Button>
          <Button type="button" size="sm" variant="destructive" aria-label="Remove stage" onClick={() => onChange(stages.filter((_, idx) => idx !== i))}><X size={13} /></Button>
        </div>
      ))}
      <Button type="button" size="sm" variant="outline" onClick={() => onChange([...stages, ""])}><Plus size={13} />Add stage</Button>
    </div>
  );
}

function Section({ title, hint, children }: { title: string; hint: string; children: React.ReactNode }) {
  return (
    <div className="space-y-3 pt-4 border-t border-border/40">
      <div>
        <Label>{title}</Label>
        <p className="text-[12px] text-muted-foreground mt-0.5">{hint}</p>
      </div>
      {children}
    </div>
  );
}

/**
 * What to show, what to ask and how to fulfil one product. Used by the product form. A template is a copy of part of this
 * same configuration, so choosing one fills a section in and the product can then be changed freely. Templates themselves
 * are made and edited in the store's Templates tab, not here.
 */
export function StoreConfigEditor({ value, onChange, deliveryInfo, onApplyTemplate, manageTemplatesHref }: {
  value: ConfigDraft;
  onChange: (next: ConfigDraft) => void;
  deliveryInfo: string;
  /** Called when a template is used, so the parent can also take over its delivery instructions. */
  onApplyTemplate: (t: StoreProductTemplate) => void;
  /** Where the Templates tab is, opened in a new browser tab so a half-filled product is kept. */
  manageTemplatesHref?: string;
}) {
  const set = <K extends keyof ConfigDraft>(k: K, v: ConfigDraft[K]) => onChange({ ...value, [k]: v });
  // A template is a copy: the product keeps nothing linking it back, so it can be changed freely.
  const stripKeys = (rows: ServiceFieldDefinition[]) => toRows(rows).map(({ key: _key, ...rest }) => rest);

  return (
    <div className="space-y-1">
      <Section title="Start from a template" hint="A whole setup in one go: details, questions, delivery details and stages. Use one, then change anything for this product.">
        <TemplatePicker
          kind="Setup"
          noun="product template"
          hasContent={value.details.length + value.fields.length + value.deliveryFields.length + value.stages.length > 0}
          summary={(t) => summarizeTemplate(t)}
          onUse={(t) => { onChange(configFromTemplate(t)); onApplyTemplate(t); }}
          manageHref={manageTemplatesHref}
        />
      </Section>

      <Section title="What members see" hint="Shown on the product page, beside the description.">
        <TemplatePicker
          kind="Details"
          noun="details template"
          hasContent={value.details.length > 0 || value.priceLabel.trim() !== ""}
          summary={(t) => summarizeTemplate(t)}
          onUse={(t) => onChange({ ...value, priceLabel: t.priceLabel ?? "", trackStock: t.trackStock, details: t.details.map((d) => ({ ...d })) })}
          manageHref={manageTemplatesHref}
        />
        <DetailsEditor value={value} onChange={onChange} />
      </Section>

      <Section title="What you ask the buyer" hint="Questions the buyer answers for this item before paying, such as dates, guests, size or a name for the order.">
        <TemplatePicker
          kind="Questions"
          noun="question template"
          hasContent={value.fields.some((f) => f.label.trim() !== "")}
          summary={(t) => summarizeTemplate(t)}
          onUse={(t) => set("fields", stripKeys(t.fields))}
          manageHref={manageTemplatesHref}
        />
        <QuestionList rows={value.fields} onChange={(rows) => set("fields", rows)} addLabel="Add question" emptyHint="No questions. Buyers just pick a quantity." />
      </Section>

      <Section title="Delivery or pickup details" hint="What you need to deliver this item, such as an address, phone number or preferred time.">
        <TemplatePicker
          kind="DeliveryDetails"
          noun="delivery template"
          hasContent={value.deliveryFields.some((f) => f.label.trim() !== "") || deliveryInfo.trim() !== ""}
          summary={(t) => summarizeTemplate(t)}
          onUse={(t) => { set("deliveryFields", stripKeys(t.deliveryFields)); onApplyTemplate(t); }}
          manageHref={manageTemplatesHref}
        />
        <QuestionList rows={value.deliveryFields} onChange={(rows) => set("deliveryFields", rows)} addLabel="Add delivery detail" emptyHint="No delivery details are collected." />
      </Section>

      <Section title="Fulfilment stages" hint="The steps each ordered item moves through, in order (for example Confirmed, Preparing, Ready, Delivered). Leave empty to use your store's default delivery stages.">
        <TemplatePicker
          kind="Stages"
          noun="stages template"
          hasContent={value.stages.some((x) => x.trim() !== "")}
          summary={(t) => summarizeTemplate(t)}
          onUse={(t) => set("stages", [...t.stages])}
          manageHref={manageTemplatesHref}
        />
        <StagesEditor stages={value.stages} onChange={(stages) => set("stages", stages)} />
      </Section>
    </div>
  );
}

const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? "" : "s"}`;

/** One line saying what a template holds, shown in the Templates tab and when one is picked on a product. */
export function summarizeTemplate(t: StoreProductTemplate): string {
  switch (t.kind ?? "Setup") {
    case "Questions": return `${plural(t.fields.length, "question")}: ${t.fields.slice(0, 3).map((f) => f.label).join(", ")}${t.fields.length > 3 ? "…" : ""}`;
    case "DeliveryDetails": return `${plural(t.deliveryFields.length, "delivery detail")}${t.deliveryInfo ? " and instructions for buyers" : ""}`;
    case "Stages": return t.stages.join(" → ");
    case "Details": return `${plural(t.details.length, "detail")}${t.priceLabel ? ` · price label "${t.priceLabel}"` : ""}${t.trackStock ? "" : " · unlimited availability"}`;
    case "Variants": return (t.variantOptions ?? []).map((o) => `${o.name}: ${o.values.join(", ")}`).join(" · ");
    default: return `${plural(t.details.length, "detail")} · ${plural(t.fields.length, "question")} · ${plural(t.deliveryFields.length, "delivery detail")} · ${plural(t.stages.length, "stage")}`;
  }
}
