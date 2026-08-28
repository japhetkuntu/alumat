import * as React from "react";
import { cn } from "../lib/utils";

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
          // 16px below md: iOS Safari auto-zooms the whole page on focus for
          // any input with a computed font-size under 16px, forcing the user
          // to manually zoom back out afterward — 14px only kicks in once a
          // pointer-driven layout (md+) makes that non-issue.
          "flex h-11 w-full border border-input bg-background px-3 text-[16px] md:text-[14px] text-foreground transition-all duration-150",
          "placeholder:text-muted-foreground/65",
          "focus-visible:outline-none focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/25",
          "disabled:cursor-not-allowed disabled:opacity-55 disabled:bg-muted/40",
          error && "border-destructive/70 focus-visible:border-destructive/70 focus-visible:ring-destructive/20",
          success && "border-success/70 focus-visible:border-success/70 focus-visible:ring-success/20",
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
