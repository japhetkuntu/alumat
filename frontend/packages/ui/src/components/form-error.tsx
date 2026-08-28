import { AlertCircle } from "lucide-react";
import { cn } from "../lib/utils";

/** Persistent inline error for a form/dialog — shows until the user retries, unlike a toast which auto-dismisses. */
export function FormError({ message, className }: { message?: string | null; className?: string }) {
  if (!message) return null;
  return (
    <div
      role="alert"
      className={cn(
        "flex items-start gap-2 border border-destructive/30 bg-destructive/10 px-3 py-2 text-[13px] text-destructive",
        className
      )}
    >
      <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" />
      <span>{message}</span>
    </div>
  );
}
