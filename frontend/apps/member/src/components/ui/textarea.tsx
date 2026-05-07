"use client";

import * as React from "react";
import { cn } from "@/lib/utils";

export interface TextareaProps extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {
  error?: boolean;
  autoResize?: boolean;
}

const Textarea = React.forwardRef<HTMLTextAreaElement, TextareaProps>(
  ({ className, error, autoResize = true, onChange, ...props }, ref) => {
    const internalRef = React.useRef<HTMLTextAreaElement | null>(null);

    const resize = React.useCallback(() => {
      const el = internalRef.current;
      if (!el || !autoResize) return;
      el.style.height = "auto";
      el.style.height = `${el.scrollHeight}px`;
    }, [autoResize]);

    React.useEffect(() => {
      resize();
    }, [resize, props.value]);

    return (
      <textarea
        className={cn(
          "flex min-h-[96px] w-full rounded-[10px] border border-input bg-background px-3 py-2.5 text-[14px] text-foreground shadow-sm transition-all duration-150 ease-out",
          "placeholder:text-muted-foreground/65",
          "focus-visible:outline-none focus-visible:border-ring/60 focus-visible:ring-2 focus-visible:ring-ring/20",
          "disabled:cursor-not-allowed disabled:opacity-55 disabled:bg-muted/40",
          autoResize ? "resize-none overflow-hidden" : "resize-y",
          error && "border-destructive/60 focus-visible:border-destructive/60 focus-visible:ring-destructive/20",
          className
        )}
        ref={(el) => {
          internalRef.current = el;
          if (typeof ref === "function") ref(el);
          else if (ref) ref.current = el;
        }}
        rows={props.rows ?? 3}
        onChange={(e) => {
          onChange?.(e);
          resize();
        }}
        {...props}
      />
    );
  }
);
Textarea.displayName = "Textarea";

export { Textarea };
