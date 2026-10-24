"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ChevronDown, Plus, Pencil, Trash2, X } from "@alumni/ui";
import { Badge, Button, Card, CardContent, ConfirmModal, EmptyState, FormSection, FormSelect, FormSheet, Input, Label, Textarea, cn } from "@alumni/ui";
import { DetailSection, FactList, QuestionView, StageList } from "@/components/institution/config-details";
import { createStoreTemplate, deleteStoreTemplate, getStoreTemplates, updateStoreTemplate, type StoreTemplateBody } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import {
  DetailsEditor, QuestionList, StagesEditor, configFromTemplate, configToBody, emptyConfigDraft, summarizeTemplate, type ConfigDraft,
} from "@/components/institution/store-config-editor";
import type { StoreProductTemplate, StoreTemplateKind } from "@/types";

const KINDS: { value: StoreTemplateKind; label: string; hint: string }[] = [
  { value: "Setup", label: "Whole product setup", hint: "Details, questions, delivery details and stages together. The quickest way to start a similar product." },
  { value: "Details", label: "Product details", hint: "The price label, stock setting and details shown on the product page." },
  { value: "Questions", label: "Questions for the buyer", hint: "What the buyer answers before paying, such as dates, guests or a name." },
  { value: "DeliveryDetails", label: "Delivery or pickup details", hint: "What you need to deliver, such as an address or phone, and the instructions shown to buyers." },
  { value: "Stages", label: "Fulfilment stages", hint: "The steps each ordered item moves through." },
  { value: "Variants", label: "Variant options", hint: "Option types and their values, such as Size: Small, Medium, Large." },
];
const kindLabel = (k: StoreTemplateKind) => KINDS.find((x) => x.value === k)?.label ?? "Whole product setup";

interface VariantRow { name: string; valuesRaw: string }
interface Draft {
  name: string;
  description: string;
  kind: StoreTemplateKind;
  config: ConfigDraft;
  deliveryInfo: string;
  variantRows: VariantRow[];
}

const emptyDraft: Draft = { name: "", description: "", kind: "Setup", config: emptyConfigDraft, deliveryInfo: "", variantRows: [] };

const draftFromTemplate = (t: StoreProductTemplate): Draft => ({
  name: t.name,
  description: t.description ?? "",
  kind: t.kind ?? "Setup",
  config: configFromTemplate(t),
  deliveryInfo: t.deliveryInfo ?? "",
  variantRows: (t.variantOptions ?? []).map((o) => ({ name: o.name, valuesRaw: o.values.join(", ") })),
});

const parseValues = (raw: string) => Array.from(new Set(raw.split(",").map((v) => v.trim()).filter(Boolean)));

/** What would be saved is empty for the chosen kind, so the template would do nothing when used. */
function emptyReason(d: Draft): string | null {
  const c = configToBody(d.config);
  switch (d.kind) {
    case "Questions": return c.fields.length === 0 ? "Add at least one question." : null;
    case "DeliveryDetails": return c.deliveryFields.length === 0 && !d.deliveryInfo.trim() ? "Add a delivery detail or instructions for buyers." : null;
    case "Stages": return c.stages.length === 0 ? "Add at least one stage." : null;
    case "Details": return c.details.length === 0 && !c.priceLabel ? "Add a detail or a price label." : null;
    case "Variants": return d.variantRows.every((r) => !r.name.trim() || parseValues(r.valuesRaw).length === 0) ? "Add an option type with at least one value." : null;
    default: return c.details.length + c.fields.length + c.deliveryFields.length + c.stages.length === 0 && !d.deliveryInfo.trim() ? "Add something to the setup." : null;
  }
}

function TemplateForm({ id, draft, setDraft, editing, onSubmit }: { id: string; draft: Draft; setDraft: (d: Draft) => void; editing: boolean; onSubmit: () => void }) {
  const { kind, config } = draft;
  const setConfig = (c: ConfigDraft) => setDraft({ ...draft, config: c });
  const shows = (...kinds: StoreTemplateKind[]) => kinds.includes(kind);

  return (
    <form id={id} className="space-y-5" onSubmit={(e) => { e.preventDefault(); onSubmit(); }}>
      <FormSection title="The template">
        <div className="space-y-2">
          <Label>Name</Label>
          <Input value={draft.name} maxLength={80} onChange={(e) => setDraft({ ...draft, name: e.target.value })} placeholder="e.g. Room booking" />
        </div>
        <div className="space-y-2">
          <Label>What does it hold?</Label>
          <FormSelect className="w-full" value={kind} disabled={editing} onValueChange={(v) => setDraft({ ...draft, kind: v as StoreTemplateKind })} options={KINDS.map((k) => ({ value: k.value, label: k.label }))} />
          <p className="text-[12.5px] text-muted-foreground">{KINDS.find((k) => k.value === kind)?.hint}{editing && " The type can't be changed after saving."}</p>
        </div>
      </FormSection>

      {shows("Setup", "Details") && (
        <FormSection title="Product details" hint="Shown on the product page, beside the description.">
          <DetailsEditor value={config} onChange={setConfig} />
        </FormSection>
      )}
      {shows("Setup", "Questions") && (
        <FormSection title="Questions for the buyer" hint="Answered before paying, such as dates, guests, size or a name for the order.">
          <QuestionList rows={config.fields} onChange={(rows) => setConfig({ ...config, fields: rows })} addLabel="Add question" emptyHint="No questions yet." />
        </FormSection>
      )}
      {shows("Setup", "DeliveryDetails") && (
        <FormSection title="Delivery or pickup" hint="What you need to deliver this item, and what buyers are told about delivery.">
          <QuestionList rows={config.deliveryFields} onChange={(rows) => setConfig({ ...config, deliveryFields: rows })} addLabel="Add delivery detail" emptyHint="No delivery details are collected." />
          <div className="space-y-2">
            <Label className="text-[12.5px]">Instructions for buyers <span className="font-normal text-muted-foreground">(optional)</span></Label>
            <Textarea rows={2} value={draft.deliveryInfo} onChange={(e) => setDraft({ ...draft, deliveryInfo: e.target.value })} placeholder="Pickup at the community office, Mon–Fri 9am–5pm." />
          </div>
        </FormSection>
      )}
      {shows("Setup", "Stages") && (
        <FormSection title="Fulfilment stages" hint="In order, for example Confirmed, Preparing, Ready, Delivered.">
          <StagesEditor stages={config.stages} onChange={(stages) => setConfig({ ...config, stages })} />
        </FormSection>
      )}
      {shows("Variants") && (
        <FormSection title="Variant options" hint="Each option type and its values. Products using this get one variant for every combination, and you set the stock for each on the product.">
          <div className="space-y-3">
            {draft.variantRows.length === 0 && <p className="text-[12.5px] text-muted-foreground">No options yet.</p>}
            {draft.variantRows.map((r, i) => (
              <div key={i} className="grid grid-cols-1 gap-2 sm:grid-cols-[10rem_1fr_auto] sm:items-center">
                <Input aria-label="Option type" placeholder="e.g. Size" value={r.name} onChange={(e) => setDraft({ ...draft, variantRows: draft.variantRows.map((x, idx) => (idx === i ? { ...x, name: e.target.value } : x)) })} />
                <Input aria-label="Values" placeholder="Comma-separated, e.g. Small, Medium, Large" value={r.valuesRaw} onChange={(e) => setDraft({ ...draft, variantRows: draft.variantRows.map((x, idx) => (idx === i ? { ...x, valuesRaw: e.target.value } : x)) })} />
                <Button type="button" size="sm" variant="destructive" aria-label="Remove option" onClick={() => setDraft({ ...draft, variantRows: draft.variantRows.filter((_, idx) => idx !== i) })}><X size={13} /></Button>
              </div>
            ))}
            <Button type="button" size="sm" variant="outline" onClick={() => setDraft({ ...draft, variantRows: [...draft.variantRows, { name: "", valuesRaw: "" }] })}><Plus size={13} />Add option</Button>
          </div>
        </FormSection>
      )}
    </form>
  );
}

/**
 * The store's Templates tab: make, change and delete reusable setups in one place. Products only choose from them. A template
 * is copied into a product when used, so changing or deleting one never touches products that already used it.
 */
export function StoreTemplatesPanel() {
  const qc = useQueryClient();
  const { data: templates = [], isLoading } = useQuery({ queryKey: ["admin-store-templates"], queryFn: getStoreTemplates });
  // The templates opened to read what is in them, without going into edit.
  const [openIds, setOpenIds] = useState<Set<string>>(new Set());
  const toggleOpen = (tid: string) => setOpenIds((cur) => { const next = new Set(cur); if (next.has(tid)) next.delete(tid); else next.add(tid); return next; });
  const [editing, setEditing] = useState<StoreProductTemplate | null>(null);
  const [creating, setCreating] = useState(false);
  const [draft, setDraft] = useState<Draft>(emptyDraft);
  const [error, setError] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<StoreProductTemplate | null>(null);

  const open = creating || editing !== null;
  const close = () => { setCreating(false); setEditing(null); setError(null); };
  const refresh = () => qc.invalidateQueries({ queryKey: ["admin-store-templates"] });

  const save = useMutation({
    mutationFn: () => {
      const body: StoreTemplateBody = {
        ...configToBody(draft.config),
        kind: draft.kind, name: draft.name.trim(), description: draft.description.trim() || undefined,
        deliveryInfo: draft.deliveryInfo.trim() || undefined,
        variantOptions: draft.variantRows.map((r) => ({ name: r.name.trim(), values: parseValues(r.valuesRaw) })).filter((o) => o.name && o.values.length > 0),
      };
      return editing ? updateStoreTemplate(editing.id, body) : createStoreTemplate(body);
    },
    onSuccess: (t) => { refresh(); close(); toast.success(editing ? `Updated "${t.name}". Products that used it are unchanged.` : `Saved "${t.name}". You can use it on any product.`); },
    onError: (e) => { const m = handleApiError(e); setError(m); toast.error(m); },
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteStoreTemplate(id),
    onSuccess: () => { refresh(); setDeleteTarget(null); toast.success("Deleted. Products that used it are unchanged."); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const submit = () => {
    if (!draft.name.trim()) { setError("Give the template a name."); return; }
    const problem = emptyReason(draft);
    if (problem) { setError(problem); return; }
    setError(null);
    save.mutate();
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <p className="max-w-[60ch] text-[13px] leading-relaxed text-muted-foreground">
          Set up a part of a product once, then use it on any product. Templates are copied, so changing or deleting one never changes products that already used it.
        </p>
        <Button onClick={() => { setDraft(emptyDraft); setError(null); setCreating(true); }}><Plus size={15} />New template</Button>
      </div>

      {isLoading ? null : templates.length === 0 ? (
        <EmptyState
          title="No templates yet"
          description="A template saves time when many products share the same questions, delivery details, stages or variant options, such as every room you rent or every item of merchandise."
          action={<Button onClick={() => { setDraft(emptyDraft); setError(null); setCreating(true); }}><Plus size={15} />Create your first template</Button>}
        />
      ) : (
        <Card className="border-border/60">
          <CardContent className="divide-y divide-border p-0">
            {templates.map((t) => (
              <div key={t.id}>
                <div className="flex flex-wrap items-center gap-3 p-4">
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="text-[14.5px] font-semibold">{t.name}</p>
                      <Badge variant="secondary" size="sm">{kindLabel(t.kind ?? "Setup")}</Badge>
                    </div>
                    <p className="mt-0.5 truncate text-[12.5px] text-muted-foreground">{summarizeTemplate(t) || "Empty"}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <Button
                      size="sm" variant="ghost" className="gap-1 px-2 text-[12.5px] font-semibold"
                      aria-expanded={openIds.has(t.id)} aria-controls={`template-${t.id}`} onClick={() => toggleOpen(t.id)}
                    >
                      {openIds.has(t.id) ? "Hide" : "Details"}
                      <ChevronDown size={14} className={cn("transition-transform", openIds.has(t.id) && "rotate-180")} />
                    </Button>
                    <Button size="sm" variant="outline" onClick={() => { setDraft(draftFromTemplate(t)); setError(null); setEditing(t); }}><Pencil size={13} />Edit</Button>
                    <Button size="sm" variant="ghost" className="text-destructive hover:bg-destructive/10" aria-label={`Delete ${t.name}`} onClick={() => setDeleteTarget(t)}><Trash2 size={14} /></Button>
                  </div>
                </div>
                {openIds.has(t.id) && (
                  <div id={`template-${t.id}`} className="space-y-4 border-t border-border/60 bg-muted/20 p-4">
                    {t.description && <p className="text-[13px] text-muted-foreground">{t.description}</p>}
                    {(t.variantOptions?.length ?? 0) > 0 && (
                      <DetailSection title="Options">
                        <ul className="space-y-1.5 text-[13px]">
                          {t.variantOptions.map((o) => <li key={o.name}><span className="font-semibold">{o.name}:</span> {o.values.join(", ")}</li>)}
                        </ul>
                      </DetailSection>
                    )}
                    {(t.details?.length ?? 0) > 0 && <DetailSection title="Details shown to buyers"><FactList items={t.details} /></DetailSection>}
                    {(t.fields?.length ?? 0) > 0 && <DetailSection title="What buyers are asked"><QuestionView fields={t.fields} /></DetailSection>}
                    {(t.deliveryFields?.length ?? 0) > 0 && <DetailSection title="Delivery or pickup details collected"><QuestionView fields={t.deliveryFields} /></DetailSection>}
                    {t.deliveryInfo && <DetailSection title="Delivery or pickup instructions"><p className="whitespace-pre-line text-[13px] text-muted-foreground">{t.deliveryInfo}</p></DetailSection>}
                    {(t.stages?.length ?? 0) > 0 && <DetailSection title="How an order moves"><StageList stages={t.stages} /></DetailSection>}
                    {t.priceLabel && <p className="text-[13px]"><span className="font-semibold">Price label:</span> {t.priceLabel}</p>}
                  </div>
                )}
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      <FormSheet
        open={open}
        onClose={close}
        title={editing ? "Edit template" : "New template"}
        description={editing ? editing.name : "Set up the part you want to reuse. You choose it from a product when you add one."}
        formId="store-template-form"
        submitLabel={editing ? "Save changes" : "Save template"}
        saving={save.isPending}
        error={error}
        size="lg"
      >
        <TemplateForm id="store-template-form" draft={draft} setDraft={setDraft} editing={editing !== null} onSubmit={submit} />
      </FormSheet>

      <ConfirmModal
        open={!!deleteTarget}
        title="Delete template?"
        message={`"${deleteTarget?.name}" will be deleted. Products that already used it keep what they copied.`}
        confirmLabel="Delete"
        variant="destructive"
        isLoading={remove.isPending}
        onConfirm={() => deleteTarget && remove.mutate(deleteTarget.id)}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}
