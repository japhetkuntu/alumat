"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Trash2 } from "@alumni/ui";
import { Button, FormSelect, Input } from "@alumni/ui";
import { createStoreTemplate, deleteStoreTemplate, getStoreTemplates, updateStoreTemplate, type StoreTemplateBody } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import type { StoreProductTemplate, StoreTemplateKind } from "@/types";

/**
 * Saved sets for one part of a product (its questions, delivery details, stages, details or variants, or the whole setup).
 * Use one to fill in that part, then change it freely for this product. Save what is in the section as a new set, or update a
 * set with it. Changing or deleting a set never touches products that already used it.
 */
export function SavedSets({
  kind, noun, hasContent, summary, buildBody, onUse,
}: {
  kind: StoreTemplateKind;
  /** What to call a set of this kind in sentences, for example "question set". */
  noun: string;
  /** Whether the section already has something in it, so using a set would replace it. */
  hasContent: boolean;
  /** A short description of what a set holds, shown once one is chosen. */
  summary: (t: StoreProductTemplate) => string;
  /** The current content of the section, in the shape the server stores. The kind and name are added here. */
  buildBody: () => Omit<StoreTemplateBody, "kind" | "name">;
  /** Fill in the section from a set. */
  onUse: (t: StoreProductTemplate) => void;
}) {
  const qc = useQueryClient();
  const [pickedId, setPickedId] = useState("");
  const [name, setName] = useState("");
  const [confirming, setConfirming] = useState<"use" | "delete" | null>(null);

  const { data: all = [] } = useQuery({ queryKey: ["admin-store-templates"], queryFn: getStoreTemplates });
  // A set saved before kinds existed has no kind field on old cached data, and was always a whole setup.
  const sets = all.filter((t) => (t.kind ?? "Setup") === kind);
  const picked = sets.find((t) => t.id === pickedId) ?? null;
  const refresh = () => qc.invalidateQueries({ queryKey: ["admin-store-templates"] });

  const saveNew = useMutation({
    mutationFn: () => createStoreTemplate({ ...buildBody(), kind, name: name.trim() }),
    onSuccess: (t) => { refresh(); setName(""); setPickedId(t.id); toast.success(`Saved as "${t.name}". You can use it in any product.`); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const update = useMutation({
    mutationFn: () => updateStoreTemplate(picked!.id, { ...buildBody(), kind, name: picked!.name, description: picked!.description }),
    onSuccess: (t) => { refresh(); toast.success(`Updated "${t.name}". Products that used it are unchanged.`); },
    onError: (e) => toast.error(handleApiError(e)),
  });
  const remove = useMutation({
    mutationFn: () => deleteStoreTemplate(picked!.id),
    onSuccess: () => { refresh(); setPickedId(""); setConfirming(null); toast.success("Deleted. Products that used it are unchanged."); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const use = () => {
    if (!picked) return;
    onUse(picked);
    setConfirming(null);
    toast.success(`Filled in from "${picked.name}". Change anything for this product.`);
  };

  return (
    <div className="space-y-3 border border-dashed border-border bg-muted/30 p-3">
      <p className="text-[12.5px] font-semibold">Saved {noun}s</p>

      {sets.length > 0 ? (
        <div className="space-y-2.5">
          <div className="flex flex-col gap-2 sm:flex-row">
            <FormSelect
              className="w-full sm:flex-1"
              value={pickedId}
              onValueChange={(v) => { setPickedId(v); setConfirming(null); }}
              placeholder={`Choose a saved ${noun}`}
              options={sets.map((t) => ({ value: t.id, label: t.name }))}
            />
            {confirming === "use" ? (
              <div className="flex gap-2">
                <Button type="button" onClick={use}>Replace what&apos;s here</Button>
                <Button type="button" variant="outline" onClick={() => setConfirming(null)}>Keep mine</Button>
              </div>
            ) : (
              <Button type="button" variant="outline" disabled={!picked} onClick={() => (hasContent ? setConfirming("use") : use())}>Use this {noun}</Button>
            )}
          </div>
          {picked && (
            <div className="flex flex-wrap items-center justify-between gap-2">
              <p className="text-[12.5px] text-muted-foreground">{summary(picked)}</p>
              <div className="flex flex-wrap items-center gap-2">
                {hasContent && (
                  <Button type="button" size="sm" variant="outline" isLoading={update.isPending} loadingText="Updating" onClick={() => update.mutate()}>
                    Update this {noun} with what&apos;s here
                  </Button>
                )}
                {confirming === "delete" ? (
                  <>
                    <Button type="button" size="sm" variant="destructive" isLoading={remove.isPending} onClick={() => remove.mutate()}>Delete it</Button>
                    <Button type="button" size="sm" variant="outline" onClick={() => setConfirming(null)}>Cancel</Button>
                  </>
                ) : (
                  <Button type="button" size="sm" variant="ghost" aria-label={`Delete ${picked.name}`} onClick={() => setConfirming("delete")}><Trash2 size={13} /></Button>
                )}
              </div>
            </div>
          )}
        </div>
      ) : (
        <p className="text-[12.5px] text-muted-foreground">
          None yet. Set this part up once, save it, and reuse it on your other products.
        </p>
      )}

      {hasContent && (
        <div className="flex flex-col gap-2 sm:flex-row">
          <Input
            aria-label={`Name for a new ${noun}`}
            placeholder={`Save what's here as a new ${noun}, e.g. Room booking`}
            value={name}
            maxLength={80}
            onChange={(e) => setName(e.target.value)}
          />
          <Button type="button" variant="outline" disabled={!name.trim()} isLoading={saveNew.isPending} loadingText="Saving" onClick={() => saveNew.mutate()}>Save as new</Button>
        </div>
      )}
    </div>
  );
}
