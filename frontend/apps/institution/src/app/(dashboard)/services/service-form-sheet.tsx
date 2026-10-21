"use client";

import { memo, useCallback, useState } from "react";
import { toast } from "sonner";
import { RichTextEditor, Plus, X, ArrowUp, ArrowDown, FormSheet, FormSection, Input, Label, FormSelect, Button } from "@alumni/ui";
import type { ServiceFieldDefinitionBody, ServiceTypeBody } from "@/lib/institution-api";
import type { ServiceType, ServiceFieldType } from "@/types";

const FIELD_TYPES: { value: ServiceFieldType; label: string }[] = [
  { value: "Text", label: "Text" },
  { value: "TextArea", label: "Long text" },
  { value: "Number", label: "Number" },
  { value: "Date", label: "Date" },
  { value: "Select", label: "Dropdown" },
  { value: "File", label: "File upload" },
];

const STATUS_OPTIONS = [
  { value: "Active", label: "Active, members can request it" },
  { value: "Draft", label: "Draft, hidden" },
  { value: "Archived", label: "Archived" },
];

interface FieldFormRow extends ServiceFieldDefinitionBody {
  optionsRaw: string;
}

interface FormState {
  name: string;
  description: string;
  price: string;
  status: ServiceType["status"];
  fields: FieldFormRow[];
  stages: string[];
}

const emptyForm: FormState = {
  name: "", description: "", price: "", status: "Active",
  fields: [], stages: ["Submitted"],
};

function toFormState(s: ServiceType): FormState {
  return {
    name: s.name,
    description: s.description ?? "",
    price: String(s.price),
    status: s.status,
    fields: s.fields.map((f) => ({ key: f.key, label: f.label, type: f.type, required: f.required, options: f.options, optionsRaw: (f.options ?? []).join(", ") })),
    stages: s.stages.length > 0 ? [...s.stages] : ["Submitted"],
  };
}

interface FieldRowProps {
  field: FieldFormRow;
  index: number;
  isLast: boolean;
  onChange: (index: number, patch: Partial<FieldFormRow>) => void;
  onMove: (index: number, dir: -1 | 1) => void;
  onRemove: (index: number) => void;
}

// One question. Memoised so typing in a single question, or in the name or description above it, redraws only the row
// being typed in rather than every question and the rich text editor with it.
const FieldRow = memo(function FieldRow({ field: fl, index: i, isLast, onChange, onMove, onRemove }: FieldRowProps) {
  return (
    <div className="space-y-3 border border-border bg-card p-3.5">
      <div className="flex items-start gap-2">
        <div className="min-w-0 flex-1 space-y-1.5">
          <Label className="text-[12.5px]">Question {i + 1}</Label>
          <Input placeholder="e.g. Index number" value={fl.label} onChange={(e) => onChange(i, { label: e.target.value })} />
        </div>
        <div className="flex shrink-0 gap-0.5 pt-6">
          <button type="button" onClick={() => onMove(i, -1)} disabled={i === 0} className="flex size-9 items-center justify-center text-muted-foreground hover:bg-muted hover:text-foreground disabled:cursor-not-allowed disabled:opacity-30" aria-label="Move question up"><ArrowUp size={14} /></button>
          <button type="button" onClick={() => onMove(i, 1)} disabled={isLast} className="flex size-9 items-center justify-center text-muted-foreground hover:bg-muted hover:text-foreground disabled:cursor-not-allowed disabled:opacity-30" aria-label="Move question down"><ArrowDown size={14} /></button>
          <button type="button" onClick={() => onRemove(i)} className="flex size-9 items-center justify-center text-destructive hover:bg-destructive/10" aria-label={`Remove ${fl.label || "question"}`}><X size={15} /></button>
        </div>
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 sm:items-end">
        <div className="space-y-1.5">
          <Label className="text-[12.5px]">Answer type</Label>
          <FormSelect className="w-full" value={fl.type} onValueChange={(v) => onChange(i, { type: v as ServiceFieldType })} options={FIELD_TYPES} />
        </div>
        <label className="flex h-10 cursor-pointer items-center gap-2.5 text-[13.5px]">
          <input type="checkbox" checked={fl.required} onChange={(e) => onChange(i, { required: e.target.checked })} className="size-4 accent-[var(--primary)]" />
          Required
        </label>
      </div>
      {fl.type === "Select" && (
        <div className="space-y-1.5">
          <Label className="text-[12.5px]">Choices</Label>
          <Input placeholder="Separate with commas, e.g. Local, International" value={fl.optionsRaw} onChange={(e) => onChange(i, { optionsRaw: e.target.value })} />
        </div>
      )}
    </div>
  );
});

const StageList = memo(function StageList({ stages, onMove, onRemove }: { stages: string[]; onMove: (i: number, dir: -1 | 1) => void; onRemove: (i: number) => void }) {
  return (
    <ol className="space-y-1.5">
      {stages.map((s, i) => (
        <li key={i} className="flex items-center gap-2 border border-border bg-card px-3 py-2 text-[13.5px]">
          <span className="w-5 tabular-nums text-muted-foreground">{i + 1}</span>
          <span className="flex-1">{s}</span>
          <button type="button" onClick={() => onMove(i, -1)} disabled={i === 0} className="flex size-8 items-center justify-center text-muted-foreground hover:bg-muted disabled:opacity-30" aria-label="Move stage up"><ArrowUp size={13} /></button>
          <button type="button" onClick={() => onMove(i, 1)} disabled={i === stages.length - 1} className="flex size-8 items-center justify-center text-muted-foreground hover:bg-muted disabled:opacity-30" aria-label="Move stage down"><ArrowDown size={13} /></button>
          <button type="button" onClick={() => onRemove(i)} className="flex size-8 items-center justify-center text-destructive hover:bg-destructive/10" aria-label={`Remove stage ${s}`}><X size={14} /></button>
        </li>
      ))}
    </ol>
  );
});

interface ServiceFormSheetProps {
  open: boolean;
  /** The service being edited; null when creating. Read once per mount, so the page re-keys this to reset it. */
  editing: ServiceType | null;
  saving: boolean;
  error: string | null;
  onClose: () => void;
  onSubmit: (body: ServiceTypeBody) => void;
}

/**
 * The create/edit form for a service. It owns its own draft, so keystrokes redraw only this sheet and not the whole
 * services page behind it (tables, requests, dialogs).
 */
export function ServiceFormSheet({ open, editing, saving, error, onClose, onSubmit }: ServiceFormSheetProps) {
  const [form, setForm] = useState<FormState>(() => (editing ? toFormState(editing) : emptyForm));
  const [newStage, setNewStage] = useState("");

  const updateField = useCallback((i: number, patch: Partial<FieldFormRow>) => {
    setForm((f) => ({ ...f, fields: f.fields.map((fl, idx) => (idx === i ? { ...fl, ...patch } : fl)) }));
  }, []);
  const removeField = useCallback((i: number) => {
    setForm((f) => ({ ...f, fields: f.fields.filter((_, idx) => idx !== i) }));
  }, []);
  const moveField = useCallback((i: number, dir: -1 | 1) => {
    setForm((f) => {
      const fields = [...f.fields];
      const j = i + dir;
      if (j < 0 || j >= fields.length) return f;
      [fields[i], fields[j]] = [fields[j], fields[i]];
      return { ...f, fields };
    });
  }, []);
  const addField = () => setForm((f) => ({ ...f, fields: [...f.fields, { label: "", type: "Text", required: false, optionsRaw: "" }] }));

  const removeStage = useCallback((i: number) => {
    setForm((f) => ({ ...f, stages: f.stages.filter((_, idx) => idx !== i) }));
  }, []);
  const moveStage = useCallback((i: number, dir: -1 | 1) => {
    setForm((f) => {
      const stages = [...f.stages];
      const j = i + dir;
      if (j < 0 || j >= stages.length) return f;
      [stages[i], stages[j]] = [stages[j], stages[i]];
      return { ...f, stages };
    });
  }, []);
  function addStage() {
    const v = newStage.trim();
    if (!v || form.stages.some((s) => s.toLowerCase() === v.toLowerCase())) return;
    setForm((f) => ({ ...f, stages: [...f.stages, v] }));
    setNewStage("");
  }

  const setDescription = useCallback((v: string) => setForm((f) => ({ ...f, description: v })), []);

  function submit() {
    if (!form.name.trim()) { toast.error("Name is required"); return; }
    const price = Number(form.price || 0);
    if (Number.isNaN(price) || price <= 0) { toast.error("Enter a price greater than zero — services can't be free"); return; }
    if (form.fields.some((fl) => !fl.label.trim())) { toast.error("Every field needs a label"); return; }
    if (form.stages.length === 0) { toast.error("Add at least one stage"); return; }

    onSubmit({
      name: form.name.trim(),
      description: form.description.trim() || undefined,
      price,
      status: form.status,
      stages: form.stages,
      fields: form.fields.map((fl) => ({
        key: fl.key,
        label: fl.label.trim(),
        type: fl.type,
        required: fl.required,
        helpText: fl.helpText?.trim() || undefined,
        options: fl.type === "Select" ? fl.optionsRaw.split(",").map((o) => o.trim()).filter(Boolean) : undefined,
      })),
    });
  }

  return (
    <FormSheet
      open={open}
      onClose={onClose}
      title={editing ? "Edit service" : "New service"}
      description="Members request it from their portal. You see each request in your queue and move it through your stages."
      formId="service-form"
      submitLabel={editing ? "Save changes" : "Create service"}
      savingLabel="Saving"
      saving={saving}
      error={error}
      size="lg"
    >
      <form id="service-form" className="space-y-5" onSubmit={(e) => { e.preventDefault(); submit(); }}>
        <FormSection title="The service">
          <div className="space-y-2">
            <Label>Name</Label>
            <Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} placeholder="e.g. Academic transcript request" />
          </div>
          <div className="space-y-2">
            <Label>Description <span className="font-normal text-muted-foreground">(optional)</span></Label>
            <RichTextEditor variant="compact" value={form.description} onChange={setDescription} minHeight={110} placeholder="What the service is and how long it usually takes." />
          </div>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>Price (GHS)</Label>
              <Input type="number" min={0.01} step="0.01" placeholder="e.g. 25.00" value={form.price} onChange={(e) => setForm((f) => ({ ...f, price: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Status</Label>
              <FormSelect
                className="w-full"
                value={form.status}
                onValueChange={(v) => setForm((f) => ({ ...f, status: v as ServiceType["status"] }))}
                options={STATUS_OPTIONS}
              />
            </div>
          </div>
        </FormSection>

        <FormSection title="What you ask the member" hint="The details you need to fulfil a request. Members fill these in when they request it, in this order.">
          {form.fields.length === 0 && (
            <p className="border border-dashed border-border p-3 text-[13px] text-muted-foreground">No questions yet. Add the details you need, such as an index number or a graduation year.</p>
          )}
          {form.fields.map((fl, i) => (
            <FieldRow key={i} field={fl} index={i} isLast={i === form.fields.length - 1} onChange={updateField} onMove={moveField} onRemove={removeField} />
          ))}
          <Button type="button" variant="outline" onClick={addField}><Plus size={14} className="mr-1.5" /> Add a question</Button>
        </FormSection>

        <FormSection title="Fulfilment stages" hint="Your own pipeline. Every new request starts at the first stage, and members follow it as it moves.">
          <div className="flex gap-2">
            <Input
              value={newStage}
              onChange={(e) => setNewStage(e.target.value)}
              onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); addStage(); } }}
              placeholder="e.g. Under review"
            />
            <Button type="button" variant="outline" onClick={addStage} disabled={!newStage.trim()}><Plus size={14} className="mr-1.5" /> Add</Button>
          </div>
          <StageList stages={form.stages} onMove={moveStage} onRemove={removeStage} />
        </FormSection>
      </form>
    </FormSheet>
  );
}
