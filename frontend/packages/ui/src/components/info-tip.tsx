"use client";

import * as React from "react";
import { HelpCircle } from "./icons";
import { Tooltip, TooltipContent, TooltipTrigger } from "./tooltip";
import { cn } from "../lib/utils";

interface InfoTipProps {
  /** Plain-language explanation, ideally under 12 words. */
  text: string;
  side?: "top" | "right" | "bottom" | "left";
  className?: string;
}

/**
 * A small "?" that explains an advanced setting or metric right where the
 * question comes up. Controlled so a tap opens it on touch screens, where
 * Radix's hover/focus-only tooltip would otherwise never show. Needs a
 * TooltipProvider above it (each app's Providers already has one).
 */
export function InfoTip({ text, side = "top", className }: InfoTipProps) {
  const [open, setOpen] = React.useState(false);

  return (
    <Tooltip open={open} onOpenChange={setOpen}>
      <TooltipTrigger asChild>
        <button
          type="button"
          aria-label={`What is this? ${text}`}
          onClick={(e) => {
            e.preventDefault();
            e.stopPropagation();
            setOpen(true);
          }}
          className={cn(
            "inline-flex h-4 w-4 shrink-0 items-center justify-center align-middle text-muted-foreground/70 transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary",
            className
          )}
        >
          <HelpCircle size={13} />
        </button>
      </TooltipTrigger>
      <TooltipContent side={side}>{text}</TooltipContent>
    </Tooltip>
  );
}
