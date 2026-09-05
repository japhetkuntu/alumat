import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { Loader2 } from "./icons";
import { cn } from "../lib/utils";

const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap border text-sm font-semibold transition-all duration-200 ease-[cubic-bezier(0.2,0,0,1)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-45 active:scale-[0.98]",
  {
    variants: {
      variant: {
        default: "border-primary bg-primary text-primary-foreground hover:brightness-110",
        destructive:
          "border-destructive/50 bg-destructive/10 text-destructive hover:bg-destructive/15 hover:border-destructive/70",
        outline: "border-primary bg-background text-primary hover:bg-primary/5",
        secondary: "border-border bg-muted/45 text-foreground hover:bg-muted",
        ghost: "border-transparent bg-transparent text-foreground hover:bg-muted",
        link: "h-auto border-transparent bg-transparent p-0 text-sm font-semibold text-primary underline-offset-4 hover:underline",
      },
      size: {
        default: "h-11 px-5",
        sm: "h-9 px-3.5 text-[13px]",
        lg: "h-12 px-7 text-base",
        icon: "h-10 w-10 p-0",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  isLoading?: boolean;
  loadingText?: string;
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, isLoading, loadingText, children, disabled, ...props }, ref) => {
    return (
      <button
        className={cn(buttonVariants({ variant, size, className }))}
        ref={ref}
        disabled={disabled || isLoading}
        style={isLoading ? { cursor: "wait" } : undefined}
        {...props}
      >
        {isLoading ? (
          <>
            <Loader2 className="h-4 w-4 animate-spin" />
            {loadingText ?? children}
          </>
        ) : (
          children
        )}
      </button>
    );
  }
);
Button.displayName = "Button";

export { Button, buttonVariants };
