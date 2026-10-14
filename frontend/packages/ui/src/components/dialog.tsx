"use client";

import * as React from "react";
import * as DialogPrimitive from "@radix-ui/react-dialog";
import { X } from "./icons";
import { cn } from "../lib/utils";

const Dialog = DialogPrimitive.Root;
const DialogTrigger = DialogPrimitive.Trigger;
const DialogPortal = DialogPrimitive.Portal;
const DialogClose = DialogPrimitive.Close;

// Forces the overlay onto its own GPU compositor layer. WebKit (Safari, and
// Chrome on iOS) can otherwise fail to repaint OTHER fixed/sticky +
// backdrop-filter elements on the page (e.g. an app's sticky header) after
// this blur layer unmounts, leaving them invisible until a manual
// scroll/refresh forces a repaint.
const GPU_LAYER_STYLE: React.CSSProperties = {
  transform: "translateZ(0)",
  WebkitBackfaceVisibility: "hidden",
  backfaceVisibility: "hidden",
};

const DialogOverlay = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Overlay>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Overlay>
>(({ className, style, ...props }, ref) => (
  <DialogPrimitive.Overlay
    ref={ref}
    className={cn(
      "fixed inset-0 z-[9999] bg-black/40 backdrop-blur-[4px]",
      "data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0",
      className
    )}
    style={{ ...GPU_LAYER_STYLE, ...style }}
    {...props}
  />
));
DialogOverlay.displayName = DialogPrimitive.Overlay.displayName;

const DialogContent = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Content>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content> & { size?: "default" | "lg" }
>(({ className, children, size = "default", ...props }, ref) => (
  <DialogPortal>
    <DialogOverlay />
    <DialogPrimitive.Content
      ref={ref}
      className={cn(
        // Phones: a sheet that rises from the bottom edge, full width, within reach of a thumb, and never taller than what is
        // actually visible (dvh follows the browser bars and the keyboard). It scrolls inside itself; the footer stays put.
        "fixed inset-x-0 bottom-0 z-[10000] mx-auto grid w-full",
        "border border-border/60 bg-background p-5 pb-[max(1.25rem,env(safe-area-inset-bottom))] shadow-[0_-8px_32px_rgba(0,0,0,0.14)]",
        "max-h-[calc(100dvh-1.5rem)] overflow-y-auto overscroll-contain",
        "data-[state=open]:animate-in data-[state=closed]:animate-out",
        "data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0",
        "data-[state=closed]:slide-out-to-bottom data-[state=open]:slide-in-from-bottom",
        // From sm: the centred card it always was.
        "sm:inset-x-auto sm:bottom-auto sm:left-1/2 sm:top-1/2 sm:translate-x-[-50%] sm:translate-y-[-50%]",
        "sm:p-7 sm:max-h-[85vh] sm:shadow-[0_8px_32px_rgba(0,0,0,0.12)]",
        "sm:data-[state=closed]:zoom-out-95 sm:data-[state=open]:zoom-in-95",
        "sm:data-[state=closed]:slide-out-to-left-1/2 sm:data-[state=closed]:slide-out-to-top-[48%]",
        "sm:data-[state=open]:slide-in-from-left-1/2 sm:data-[state=open]:slide-in-from-top-[48%]",
        "duration-250",
        size === "default" && "sm:max-w-[520px]",
        size === "lg" && "sm:max-w-[720px]",
        className
      )}
      {...props}
    >
      {/* A small grip on the sheet so it reads as something that came up from the bottom. Phones only. */}
      <span aria-hidden className="mx-auto -mt-1 mb-3 block h-1 w-10 bg-border sm:hidden" />
      {children}
      <DialogPrimitive.Close className="absolute right-3 top-3 flex h-10 w-10 items-center justify-center text-muted-foreground/70 transition-all duration-150 hover:bg-muted hover:text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-ring/40 sm:right-4 sm:top-4 sm:h-8 sm:w-8">
        <X className="h-4 w-4" />
        <span className="sr-only">Close</span>
      </DialogPrimitive.Close>
    </DialogPrimitive.Content>
  </DialogPortal>
));
DialogContent.displayName = DialogPrimitive.Content.displayName;

function DialogHeader({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  // pr-9 keeps a long title clear of the close button.
  return <div className={cn("flex flex-col space-y-2 pb-3 pr-9 text-left", className)} {...props} />;
}

function DialogFooter({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  // On a phone the actions stay pinned to the bottom of the sheet while the content scrolls, full width and easy to hit; the
  // main action sits on top. From sm they are the familiar right-aligned row.
  return (
    <div
      className={cn(
        "mt-6 flex flex-col-reverse gap-2",
        "sticky bottom-0 -mx-5 border-t border-border/60 bg-background px-5 py-3",
        "sm:static sm:mx-0 sm:flex-row sm:justify-end sm:gap-3 sm:border-0 sm:bg-transparent sm:p-0",
        className,
      )}
      {...props}
    />
  );
}

const DialogTitle = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Title>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Title>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Title ref={ref} className={cn("text-[16px] font-semibold leading-none tracking-tight", className)} {...props} />
));
DialogTitle.displayName = DialogPrimitive.Title.displayName;

const DialogDescription = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Description>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Description>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Description ref={ref} className={cn("text-[14px] text-muted-foreground leading-relaxed", className)} {...props} />
));
DialogDescription.displayName = DialogPrimitive.Description.displayName;

export { Dialog, DialogTrigger, DialogPortal, DialogOverlay, DialogClose, DialogContent, DialogHeader, DialogFooter, DialogTitle, DialogDescription };
