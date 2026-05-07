"use client";

import * as React from "react";
import * as SelectPrimitive from "@radix-ui/react-select";
import { Check, ChevronDown, ChevronUp } from "lucide-react";
import { cn } from "@/lib/utils";

const Select = SelectPrimitive.Root;
const SelectGroup = SelectPrimitive.Group;
const SelectValue = SelectPrimitive.Value;

const SelectTrigger = React.forwardRef<
  React.ElementRef<typeof SelectPrimitive.Trigger>,
  React.ComponentPropsWithoutRef<typeof SelectPrimitive.Trigger> & { error?: boolean }
>(({ className, children, error, ...props }, ref) => (
  <SelectPrimitive.Trigger
    ref={ref}
    className={cn(
      "flex h-11 w-auto max-w-[22rem] min-w-[10rem] items-center justify-between rounded-[10px] border border-input bg-background px-3 py-2 text-[14px] text-foreground shadow-sm",
      "transition-all duration-150 ease-out cursor-pointer",
      "placeholder:text-muted-foreground/65",
      "focus:outline-none focus:border-ring/60 focus:ring-2 focus:ring-ring/20",
      "disabled:cursor-not-allowed disabled:opacity-55 disabled:bg-muted/40",
      error && "border-destructive/60 focus:border-destructive/60 focus:ring-destructive/20",
      "[&>span]:truncate",
      className
    )}
    {...props}
  >
    {children}
    <SelectPrimitive.Icon asChild>
      <ChevronDown className="h-4 w-4 text-muted-foreground/60 shrink-0" />
    </SelectPrimitive.Icon>
  </SelectPrimitive.Trigger>
));
SelectTrigger.displayName = SelectPrimitive.Trigger.displayName;

const SelectContent = React.forwardRef<
  React.ElementRef<typeof SelectPrimitive.Content>,
  React.ComponentPropsWithoutRef<typeof SelectPrimitive.Content>
>(({ className, children, position = "popper", ...props }, ref) => (
  <SelectPrimitive.Portal>
    <SelectPrimitive.Content
      ref={ref}
      className={cn(
        "relative z-[9999] max-h-[280px] min-w-[8rem] overflow-hidden rounded-[12px] border border-border/60 bg-popover text-popover-foreground",
        "shadow-[0_8px_32px_rgba(0,0,0,0.12),0_4px_8px_rgba(0,0,0,0.06)]",
        "data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95",
        "data-[side=bottom]:slide-in-from-top-2 data-[side=top]:slide-in-from-bottom-2",
        position === "popper" && "data-[side=bottom]:translate-y-1 data-[side=top]:-translate-y-1",
        className
      )}
      position={position}
      {...props}
    >
      <SelectScrollUpButton />
      <SelectPrimitive.Viewport
        className={cn(
          "p-1",
          position === "popper" && "h-[var(--radix-select-trigger-height)] w-full min-w-[var(--radix-select-trigger-width)]"
        )}
      >
        {children}
      </SelectPrimitive.Viewport>
      <SelectScrollDownButton />
    </SelectPrimitive.Content>
  </SelectPrimitive.Portal>
));
SelectContent.displayName = SelectPrimitive.Content.displayName;

const SelectItem = React.forwardRef<
  React.ElementRef<typeof SelectPrimitive.Item>,
  React.ComponentPropsWithoutRef<typeof SelectPrimitive.Item>
>(({ className, children, ...props }, ref) => (
  <SelectPrimitive.Item
    ref={ref}
    className={cn(
      "relative flex w-full cursor-pointer select-none items-center rounded-[8px] py-2 pl-3 pr-8 text-[14px] outline-none",
      "transition-colors duration-75",
      "hover:bg-accent/60 focus:bg-accent/60",
      "data-[highlighted]:bg-accent/60",
      "data-[disabled]:pointer-events-none data-[disabled]:opacity-45",
      className
    )}
    {...props}
  >
    <span className="absolute right-2 flex h-4 w-4 items-center justify-center">
      <SelectPrimitive.ItemIndicator>
        <Check className="h-4 w-4 text-primary" />
      </SelectPrimitive.ItemIndicator>
    </span>
    <SelectPrimitive.ItemText>{children}</SelectPrimitive.ItemText>
  </SelectPrimitive.Item>
));
SelectItem.displayName = SelectPrimitive.Item.displayName;

const SelectLabel = React.forwardRef<
  React.ElementRef<typeof SelectPrimitive.Label>,
  React.ComponentPropsWithoutRef<typeof SelectPrimitive.Label>
>(({ className, ...props }, ref) => (
  <SelectPrimitive.Label
    ref={ref}
    className={cn("px-3 py-1.5 text-[11px] font-semibold uppercase tracking-[0.05em] text-muted-foreground", className)}
    {...props}
  />
));
SelectLabel.displayName = SelectPrimitive.Label.displayName;

const SelectSeparator = React.forwardRef<
  React.ElementRef<typeof SelectPrimitive.Separator>,
  React.ComponentPropsWithoutRef<typeof SelectPrimitive.Separator>
>(({ className, ...props }, ref) => (
  <SelectPrimitive.Separator ref={ref} className={cn("my-1 h-px bg-foreground/8", className)} {...props} />
));
SelectSeparator.displayName = SelectPrimitive.Separator.displayName;

function SelectScrollUpButton() {
  return (
    <SelectPrimitive.ScrollUpButton className="flex cursor-default items-center justify-center py-1">
      <ChevronUp className="h-4 w-4 text-muted-foreground" />
    </SelectPrimitive.ScrollUpButton>
  );
}

function SelectScrollDownButton() {
  return (
    <SelectPrimitive.ScrollDownButton className="flex cursor-default items-center justify-center py-1">
      <ChevronDown className="h-4 w-4 text-muted-foreground" />
    </SelectPrimitive.ScrollDownButton>
  );
}

export { Select, SelectGroup, SelectValue, SelectTrigger, SelectContent, SelectItem, SelectLabel, SelectSeparator };

/* ─── Convenience wrapper for simple use-cases ──────────────────────── */

interface FormSelectProps {
  value: string;
  onValueChange: (value: string) => void;
  placeholder?: string;
  options: { value: string; label: string }[];
  error?: boolean;
  disabled?: boolean;
  className?: string;
}

export function FormSelect({ value, onValueChange, placeholder, options, error, disabled, className }: FormSelectProps) {
  return (
    <Select value={value} onValueChange={onValueChange} disabled={disabled}>
      <SelectTrigger error={error} className={className}>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {options.map((opt) => (
          <SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
