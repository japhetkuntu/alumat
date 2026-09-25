"use client";

import * as React from "react";
import { cn } from "../lib/utils";

const CONTROL = "input:not([type=hidden]), textarea, select, [role=combobox]";

/**
 * A field label. Without any extra wiring it links itself to the control that follows it, so tapping
 * the label focuses the field and screen readers announce it, and it shows a red asterisk when that
 * control is `required`. Pass `htmlFor` yourself to override the automatic link.
 */
const Label = React.forwardRef<HTMLLabelElement, React.LabelHTMLAttributes<HTMLLabelElement> & { required?: boolean }>(
  ({ className, children, required, htmlFor, ...props }, forwardedRef) => {
    const generatedId = React.useId();
    const inner = React.useRef<HTMLLabelElement | null>(null);
    const [controlRequired, setControlRequired] = React.useState(false);

    // Runs after every render on purpose: the control may mount after the label (dialogs, conditional fields).
    React.useEffect(() => {
      const label = inner.current;
      if (!label) return;
      let control: HTMLElement | null = null;
      if (htmlFor) {
        control = document.getElementById(htmlFor);
      } else {
        const next = label.nextElementSibling as HTMLElement | null;
        if (next) control = next.matches(CONTROL) ? next : next.querySelector<HTMLElement>(CONTROL);
        // Label wrapped in a row with the control as a later sibling of an icon/hint element.
        if (!control && label.parentElement) {
          const all = label.parentElement.querySelectorAll<HTMLElement>(CONTROL);
          if (all.length === 1) control = all[0];
        }
        if (control && !control.closest("label")) {
          if (!control.id) control.id = generatedId;
          if (label.htmlFor !== control.id) label.htmlFor = control.id;
        }
      }
      const isRequired = !!control && (control as HTMLInputElement).required === true;
      if (isRequired !== controlRequired) setControlRequired(isRequired);
    });

    return (
      <label
        ref={(node) => {
          inner.current = node;
          if (typeof forwardedRef === "function") forwardedRef(node);
          else if (forwardedRef) forwardedRef.current = node;
        }}
        htmlFor={htmlFor}
        className={cn(
          "mb-1.5 block text-[13px] font-semibold leading-none text-foreground",
          "peer-disabled:cursor-not-allowed peer-disabled:opacity-50",
          className
        )}
        {...props}
      >
        {children}
        {(required || controlRequired) && <span aria-hidden="true" className="text-destructive ml-0.5">*</span>}
      </label>
    );
  }
);
Label.displayName = "Label";

export { Label };
