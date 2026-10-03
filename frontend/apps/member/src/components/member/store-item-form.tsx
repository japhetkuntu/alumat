"use client";

import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import type { ServiceFieldDefinition, StoreProduct } from "@/types";

/** What a buyer has entered for one cart line. File keys are "details:{key}" / "delivery:{key}". */
export interface ItemFormState {
  answers: Record<string, string>;
  delivery: Record<string, string>;
  files: Record<string, File | undefined>;
}

export const emptyItemForm: ItemFormState = { answers: {}, delivery: {}, files: {} };

export function productNeedsForm(p: StoreProduct) {
  return (p.fields?.length ?? 0) > 0 || (p.deliveryFields?.length ?? 0) > 0;
}

/** Returns the first problem with the entered answers, or null when the line is ready to buy. */
export function validateItemForm(product: StoreProduct, form: ItemFormState): string | null {
  const sections: [ServiceFieldDefinition[], "details" | "delivery", Record<string, string>][] = [
    [product.fields ?? [], "details", form.answers],
    [product.deliveryFields ?? [], "delivery", form.delivery],
  ];
  for (const [fields, section, values] of sections) {
    for (const f of fields) {
      const missing = f.type === "File" ? !form.files[`${section}:${f.key}`] : !values[f.key]?.trim();
      if (f.required && missing) return `${product.name}: "${f.label}" is required.`;
    }
  }
  return null;
}

function FieldInput({
  field, value, file, onValue, onFile,
}: {
  field: ServiceFieldDefinition;
  value: string;
  file?: File;
  onValue: (v: string) => void;
  onFile: (f: File | undefined) => void;
}) {
  return (
    <div className="space-y-1.5">
      <Label>
        {field.label}{" "}
        {!field.required && <span className="font-normal text-muted-foreground">(optional)</span>}
      </Label>
      {field.helpText && <p className="text-[12px] text-muted-foreground -mt-1">{field.helpText}</p>}
      {field.type === "TextArea" ? (
        <Textarea rows={3} value={value} onChange={(e) => onValue(e.target.value)} />
      ) : field.type === "Select" ? (
        <FormSelect
          value={value}
          onValueChange={onValue}
          placeholder="Select"
          options={(field.options ?? []).map((o) => ({ value: o, label: o }))}
        />
      ) : field.type === "File" ? (
        <div className="flex items-center gap-2">
          <input type="file" className="text-[13px]" onChange={(e) => onFile(e.target.files?.[0])} />
          {file && <span className="text-[12px] text-muted-foreground truncate">{file.name}</span>}
        </div>
      ) : (
        <Input
          type={field.type === "Number" ? "number" : field.type === "Date" ? "date" : "text"}
          value={value}
          onChange={(e) => onValue(e.target.value)}
        />
      )}
    </div>
  );
}

export function StoreItemForm({
  product, form, onChange,
}: {
  product: StoreProduct;
  form: ItemFormState;
  onChange: (next: ItemFormState) => void;
}) {
  const groups: { title: string | null; fields: ServiceFieldDefinition[]; section: "details" | "delivery" }[] = [
    { title: (product.deliveryFields?.length ?? 0) > 0 ? "Your details" : null, fields: product.fields ?? [], section: "details" },
    { title: "Delivery details", fields: product.deliveryFields ?? [], section: "delivery" },
  ];

  return (
    <div className="space-y-4">
      {groups.filter((g) => g.fields.length > 0).map((g) => (
        <div key={g.section} className="space-y-3">
          {g.title && <p className="text-[12px] font-semibold uppercase tracking-wide text-muted-foreground">{g.title}</p>}
          {g.fields.map((f) => (
            <FieldInput
              key={f.key}
              field={f}
              value={(g.section === "details" ? form.answers : form.delivery)[f.key] ?? ""}
              file={form.files[`${g.section}:${f.key}`]}
              onValue={(v) =>
                onChange(g.section === "details"
                  ? { ...form, answers: { ...form.answers, [f.key]: v } }
                  : { ...form, delivery: { ...form.delivery, [f.key]: v } })
              }
              onFile={(file) => onChange({ ...form, files: { ...form.files, [`${g.section}:${f.key}`]: file } })}
            />
          ))}
        </div>
      ))}
    </div>
  );
}
