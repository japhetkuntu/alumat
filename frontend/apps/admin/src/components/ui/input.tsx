import * as React from "react";
import { cn } from "@/lib/utils";

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  error?: boolean;
  success?: boolean;
}

const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ className, type, error, success, ...props }, ref) => {
    return (
      <input
        type={type}
        className={cn(
          "flex h-11 w-full rounded-[10px] border border-input bg-background px-3 text-[14px] text-foreground shadow-sm transition-all duration-150",
          "placeholder:text-muted-foreground/65",
          "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/20 focus-visible:border-ring/60",
          "disabled:cursor-not-allowed disabled:opacity-55 disabled:bg-muted/40",
          error && "border-destructive/60 focus-visible:border-destructive/60 focus-visible:ring-destructive/20",
          success && "border-success/65 focus-visible:border-success/65 focus-visible:ring-success/20",
          className
        )}
        ref={ref}
        {...props}
      />
    );
  }
);
Input.displayName = "Input";

export { Input };
