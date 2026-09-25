import * as React from "react";
import { cn } from "../lib/utils";

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  error?: boolean;
  success?: boolean;
}

// Sign-in forms set autoComplete themselves. Any other password field is a new or confirm-password box,
// except one named "current"/"old", so password managers offer to save rather than fill.
function defaultAutoComplete(type: string | undefined, key: string | undefined) {
  if (type === "email") return "email";
  if (type === "tel") return "tel";
  if (type === "password") return /current|old/i.test(key ?? "") ? "current-password" : "new-password";
  return undefined;
}

const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ className, type, error, success, ...props }, ref) => {
    // Number fields get the phone's number pad: decimal for money-like steps, plain digits otherwise.
    const inputMode = props.inputMode ?? (type === "number" ? (props.step && String(props.step) !== "1" ? "decimal" : "numeric") : undefined);
    return (
      <input
        type={type}
        inputMode={inputMode}
        autoComplete={props.autoComplete ?? defaultAutoComplete(type, props.id ?? props.name)}
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
        aria-invalid={error || undefined}
        {...props}
      />
    );
  }
);
Input.displayName = "Input";

export { Input };
