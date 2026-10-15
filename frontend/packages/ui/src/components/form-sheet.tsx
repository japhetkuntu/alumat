"use client";

import * as React from "react";
import * as DialogPrimitive from "@radix-ui/react-dialog";
import { X } from "./icons";
import { Button } from "./button";
import { DialogOverlay, DialogPortal } from "./dialog";
import { cn } from "../lib/utils";

export interface FormSheetProps {
  open: boolean;
  /** Called when the person closes the sheet and has confirmed they are happy to lose any changes. */
  onClose: () => void;
  title: string;
  description?: string;
  /** The id of the `<form>` inside, so the pinned save button can submit it. */
  formId: string;
  submitLabel?: string;
  /** Shown on the save button while saving. */
  savingLabel?: string;
  saving?: boolean;
  submitDisabled?: boolean;
  /** A message from the server about what went wrong, shown above the save bar so it is never off-screen. */
  error?: string | null;
  size?: "md" | "lg";
  children: React.ReactNode;
}

/**
 * Where someone adds or edits something. It opens over the page rather than pushing the page down, so the form is
 * always in view no matter how far down the list they were: a panel from the right on a computer, a tall sheet from
 * the bottom on a phone. The save bar is pinned, so the main action never scrolls away, and closing with unsaved
 * changes asks first.
 */
export function FormSheet({
  open, onClose, title, description, formId, submitLabel = "Save", savingLabel = "Saving", saving, submitDisabled, error, size = "md", children,
}: FormSheetProps) {
  const [dirty, setDirty] = React.useState(false);
  const [confirming, setConfirming] = React.useState(false);

  // A fresh start each time it opens.
  React.useEffect(() => {
    if (open) { setDirty(false); setConfirming(false); }
  }, [open]);

  const requestClose = () => {
    if (saving) return;
    if (dirty) setConfirming(true);
    else onClose();
  };

  return (
    <DialogPrimitive.Root open={open} onOpenChange={(v) => { if (!v) requestClose(); }}>
      <DialogPortal>
        <DialogOverlay />
        <DialogPrimitive.Content
          onInteractOutside={(e) => { e.preventDefault(); requestClose(); }}
          className={cn(
            "fixed z-[10000] flex flex-col bg-background outline-none",
            // Phone: nearly the full height, rising from the bottom edge.
            "inset-x-0 bottom-0 top-6 border-t border-border shadow-[0_-8px_32px_rgba(0,0,0,0.16)]",
            "data-[state=open]:animate-in data-[state=closed]:animate-out",
            "data-[state=open]:slide-in-from-bottom data-[state=closed]:slide-out-to-bottom",
            // From sm: a panel on the right, full height.
            "sm:inset-y-0 sm:left-auto sm:right-0 sm:top-0 sm:w-full sm:border-l sm:border-t-0 sm:shadow-[-8px_0_32px_rgba(0,0,0,0.12)]",
            "sm:data-[state=open]:slide-in-from-right sm:data-[state=closed]:slide-out-to-right",
            size === "md" ? "sm:max-w-[540px]" : "sm:max-w-[760px]",
            "duration-250",
          )}
        >
          <span aria-hidden className="mx-auto mt-2 block h-1 w-10 shrink-0 bg-border sm:hidden" />

          <header className="flex shrink-0 items-start justify-between gap-3 border-b border-border px-5 py-4 sm:px-6">
            <div className="min-w-0">
              <DialogPrimitive.Title className="text-[17px] font-bold leading-tight">{title}</DialogPrimitive.Title>
              {description ? (
                <DialogPrimitive.Description className="mt-1 text-[13.5px] leading-relaxed text-muted-foreground">{description}</DialogPrimitive.Description>
              ) : (
                <DialogPrimitive.Description className="sr-only">{title}</DialogPrimitive.Description>
              )}
            </div>
            <button
              type="button"
              onClick={requestClose}
              className="-mr-2 flex size-10 shrink-0 items-center justify-center text-muted-foreground transition-colors hover:bg-muted hover:text-foreground focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary"
            >
              <X className="size-4" />
              <span className="sr-only">Close</span>
            </button>
          </header>

          <div
            className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-5 py-5 sm:px-6"
            onInputCapture={() => setDirty(true)}
            onChangeCapture={() => setDirty(true)}
          >
            {children}
          </div>

          <footer className="shrink-0 border-t border-border bg-background px-5 py-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] sm:px-6">
            {error && <p role="alert" className="mb-2 text-[13.5px] font-medium text-destructive">{error}</p>}
            {confirming ? (
              <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                <p className="text-[13.5px] font-semibold">Discard what you have entered?</p>
                <div className="flex gap-2">
                  <Button type="button" variant="outline" className="flex-1 sm:flex-none" onClick={() => setConfirming(false)}>Keep editing</Button>
                  <Button type="button" variant="destructive" className="flex-1 sm:flex-none" onClick={onClose}>Discard</Button>
                </div>
              </div>
            ) : (
              <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end sm:gap-3">
                <Button type="button" variant="outline" onClick={requestClose} disabled={saving}>Cancel</Button>
                <Button type="submit" form={formId} isLoading={saving} loadingText={savingLabel} disabled={submitDisabled}>{submitLabel}</Button>
              </div>
            )}
          </footer>
        </DialogPrimitive.Content>
      </DialogPortal>
    </DialogPrimitive.Root>
  );
}
