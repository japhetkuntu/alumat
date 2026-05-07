"use client";

import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { AlertTriangle } from "lucide-react";

interface ConfirmModalProps {
  open: boolean;
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: "destructive" | "default";
  isLoading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
  children?: React.ReactNode;
}

export function ConfirmModal({
  open, title, message, confirmLabel = "Confirm", cancelLabel = "Cancel",
  variant = "destructive", isLoading = false, onConfirm, onCancel, children,
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
          <Button variant="outline" onClick={onCancel} disabled={isLoading}>{cancelLabel}</Button>
          <Button variant={variant} onClick={onConfirm} isLoading={isLoading} loadingText={confirmLabel}>
            {confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
