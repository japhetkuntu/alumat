"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button, FormSelect } from "@alumni/ui";
import { getStoreTemplates } from "@/lib/institution-api";
import type { StoreProductTemplate, StoreTemplateKind } from "@/types";

/**
 * Fills one part of the product form from a template. It only uses templates: they are created and changed in the store's
 * Templates tab, so nothing here saves, updates or deletes. A template is a copy, so the product can be changed freely.
 */
export function TemplatePicker({ kind, noun, hasContent, summary, onUse, manageHref }: {
  kind: StoreTemplateKind;
  /** What to call a template of this kind in a sentence, for example "question template". */
  noun: string;
  /** Whether the section already has something in it, so using a template would replace it. */
  hasContent: boolean;
  summary: (t: StoreProductTemplate) => string;
  onUse: (t: StoreProductTemplate) => void;
  /** Opens the Templates tab in a new browser tab, so a half-filled product is not lost. */
  manageHref?: string;
}) {
  const [pickedId, setPickedId] = useState("");
  const [confirming, setConfirming] = useState(false);
  const { data: all = [] } = useQuery({ queryKey: ["admin-store-templates"], queryFn: getStoreTemplates });
  const sets = all.filter((t) => (t.kind ?? "Setup") === kind);
  const picked = sets.find((t) => t.id === pickedId) ?? null;

  const use = () => {
    if (!picked) return;
    onUse(picked);
    setConfirming(false);
    toast.success(`Filled in from "${picked.name}". Change anything for this product.`);
  };

  const manage = manageHref && (
    <a href={manageHref} target="_blank" rel="noopener" className="text-[12.5px] font-semibold text-primary hover:underline">
      {sets.length > 0 ? "Manage templates" : "Create a template"} (opens in a new tab)
    </a>
  );

  if (sets.length === 0) {
    // Only the top picker speaks, and only when the store has no templates at all. The sections stay quiet until one of their own kind exists.
    if (kind !== "Setup" || all.length > 0) return null;
    return (
      <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-[12.5px] text-muted-foreground">
        No templates yet. Make one in the Templates tab and reuse it on every product. {manage}
      </p>
    );
  }

  return (
    <div className="space-y-2 border border-dashed border-border bg-muted/30 p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-[12.5px] font-semibold">Use a {noun}</p>
        {manage}
      </div>
      <div className="flex flex-col gap-2 sm:flex-row">
        <FormSelect
          className="w-full sm:flex-1"
          value={pickedId}
          onValueChange={(v) => { setPickedId(v); setConfirming(false); }}
          placeholder={`Choose a ${noun}`}
          options={sets.map((t) => ({ value: t.id, label: t.name }))}
        />
        {confirming ? (
          <div className="flex gap-2">
            <Button type="button" onClick={use}>Replace what&apos;s here</Button>
            <Button type="button" variant="outline" onClick={() => setConfirming(false)}>Keep mine</Button>
          </div>
        ) : (
          <Button type="button" variant="outline" disabled={!picked} onClick={() => (hasContent ? setConfirming(true) : use())}>Use it</Button>
        )}
      </div>
      {picked && <p className="text-[12.5px] text-muted-foreground">{summary(picked)}</p>}
    </div>
  );
}
