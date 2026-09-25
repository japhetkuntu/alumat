"use client";

import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from "./dialog";
import { Button } from "./button";
import { AlertTriangle } from "./icons";

interface ConfirmModalProps {
  open: boolean;
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: "destructive" | "default";
  /** Which button is the main one. Use "cancel" when backing out is the safe choice, e.g. giving up a place. */
  emphasis?: "confirm" | "cancel";
  isLoading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
  children?: React.ReactNode;
}

export function ConfirmModal({
  open, title, message, confirmLabel = "Confirm", cancelLabel = "Cancel",
  variant = "destructive", emphasis = "confirm", isLoading = false, onConfirm, onCancel, children,
}: ConfirmModalProps) {
  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) onCancel(); }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <div className="flex items-start gap-3">
            {variant === "destructive" && (
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-destructive/10">
                <AlertTriangle className="h-5 w-5 text-destructive" />
              </div>
            )}
            <div className="space-y-1.5">
              <DialogTitle>{title}</DialogTitle>
              <DialogDescription>{message}</DialogDescription>
            </div>
          </div>
        </DialogHeader>
        {children && <div className="py-1">{children}</div>}
        <DialogFooter>
          <Button variant={emphasis === "cancel" ? "default" : "outline"} onClick={onCancel} disabled={isLoading}>{cancelLabel}</Button>
          <Button variant={emphasis === "cancel" ? "outline" : variant} className={emphasis === "cancel" && variant === "destructive" ? "text-destructive" : undefined} onClick={onConfirm} isLoading={isLoading} loadingText={confirmLabel}>
            {confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
