import * as React from "react";
import { cn } from "@/lib/utils";

const Label = React.forwardRef<HTMLLabelElement, React.LabelHTMLAttributes<HTMLLabelElement> & { required?: boolean }>(
  ({ className, children, required, ...props }, ref) => (
    <label
      ref={ref}
      className={cn(
        "text-[12px] font-semibold leading-none uppercase tracking-[0.06em] text-muted-foreground",
        "peer-disabled:cursor-not-allowed peer-disabled:opacity-50",
        className
      )}
      {...props}
    >
      {children}
      {required && <span className="text-destructive ml-0.5">*</span>}
    </label>
  )
);
Label.displayName = "Label";

export { Label };
