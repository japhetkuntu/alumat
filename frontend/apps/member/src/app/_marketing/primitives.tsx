"use client";

import { useEffect, useRef, useState } from "react";
import { cn } from "@alumni/ui";

/** Shared across the platform marketing homepage and its sub-pages (e.g.
 * /why-not-whatsapp) — extracted once a third page needed the same
 * fade-up-on-scroll section/eyebrow pattern. */

export function scrollToSection(id: string) {
  document.getElementById(id.replace("#", ""))?.scrollIntoView({ behavior: "smooth" });
}

export function useFadeUp(threshold = 0.1) {
  const ref = useRef<HTMLDivElement>(null);
  const [visible, setVisible] = useState(false);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const obs = new IntersectionObserver(
      ([e]) => { if (e.isIntersecting) { setVisible(true); obs.disconnect(); } },
      { threshold, rootMargin: "0px 0px -40px 0px" }
    );
    obs.observe(el);
    return () => obs.disconnect();
  }, [threshold]);
  return { ref, visible };
}

export function Eyebrow({ children }: { children: React.ReactNode }) {
  return (
    <div className="inline-flex items-center gap-2.5 mb-5">
      <span className="h-px w-6" style={{ background: "var(--primary)" }} />
      <span className="text-[12.5px] font-semibold tracking-wide" style={{ color: "var(--primary)" }}>{children}</span>
    </div>
  );
}

export function Section({ id, children, className, style }: {
  id?: string; children: React.ReactNode; className?: string; style?: React.CSSProperties;
}) {
  const { ref, visible } = useFadeUp();
  return (
    <section id={id} ref={ref}
      className={cn("transition-all duration-700 ease-out", visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-6", className)}
      style={style}>
      {children}
    </section>
  );
}
