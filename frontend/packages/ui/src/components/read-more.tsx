"use client";

import { useEffect, useRef, useState } from "react";
import { cn } from "../lib/utils";

// Spelled out so the class names are found in the source and kept in the stylesheet.
const CLAMP: Record<number, string> = { 1: "line-clamp-1", 2: "line-clamp-2", 3: "line-clamp-3", 4: "line-clamp-4", 5: "line-clamp-5" };

interface ReadMoreProps {
  children: string;
  /** How many lines show before the rest is tucked away. */
  lines?: 1 | 2 | 3 | 4 | 5;
  className?: string;
}

/**
 * Text that stops after a few lines, with a "Read more" that opens the rest in place. The toggle only appears when the text is actually
 * cut off, so short text looks exactly as it would without this. Line breaks in the text are kept.
 */
export function ReadMore({ children, lines = 2, className }: ReadMoreProps) {
  const ref = useRef<HTMLParagraphElement>(null);
  const [open, setOpen] = useState(false);
  const [cut, setCut] = useState(false);

  useEffect(() => {
    const el = ref.current;
    if (!el || open) return;
    const measure = () => setCut(el.scrollHeight > el.clientHeight + 1);
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(el);
    return () => observer.disconnect();
  }, [children, lines, open]);

  return (
    <div>
      <p ref={ref} className={cn("whitespace-pre-line break-words", !open && CLAMP[lines], className)}>{children}</p>
      {(cut || open) && (
        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          aria-expanded={open}
          className="mt-1 text-[12px] font-semibold text-primary hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary"
        >
          {open ? "Show less" : "Read more"}
        </button>
      )}
    </div>
  );
}
