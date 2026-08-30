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

/* ─────────────────────────────────────────────────────────────────────────
   INTERACTION PRIMITIVES — small, purposeful motion for the marketing site
   only (scroll progress, count-up stats, cursor tilt/magnetic hover). Kept
   here rather than inline so the same feel is consistent across the
   homepage and its sub-pages.
   ───────────────────────────────────────────────────────────────────────── */

/** Continuous 0–100 scroll progress through the whole document. */
export function useScrollProgress() {
  const [progress, setProgress] = useState(0);
  useEffect(() => {
    const onScroll = () => {
      const scrollTop = window.scrollY;
      const docHeight = document.documentElement.scrollHeight - window.innerHeight;
      setProgress(docHeight > 0 ? Math.min(100, Math.max(0, (scrollTop / docHeight) * 100)) : 0);
    };
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);
  return progress;
}

/** Thin brand-colored bar pinned to the top of the viewport, tracking how far
 * down the page the visitor has read. */
export function ScrollProgressBar() {
  const progress = useScrollProgress();
  return (
    <div className="fixed top-0 left-0 right-0 h-[3px] z-[100] pointer-events-none">
      <div className="h-full" style={{ width: `${progress}%`, background: "var(--primary)", transition: "width 120ms ease-out" }} />
    </div>
  );
}

/** True once the page has scrolled past `threshold` — drives the sticky
 * header's shrink-on-scroll state. */
export function useScrolled(threshold = 20) {
  const [scrolled, setScrolled] = useState(false);
  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > threshold);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, [threshold]);
  return scrolled;
}

/** Animated count-up from 0 to `end`, firing once when scrolled into view.
 * Supports decimals so real sourced stats (e.g. "7.8%") animate cleanly
 * instead of only working for round numbers. */
export function useCountUp(end: number, decimals = 0, duration = 1600) {
  const { ref, visible } = useFadeUp(0.4);
  const [value, setValue] = useState(0);
  useEffect(() => {
    if (!visible) return;
    const start = performance.now();
    const tick = (now: number) => {
      const t = Math.min((now - start) / duration, 1);
      const eased = 1 - Math.pow(1 - t, 3);
      setValue(Number((eased * end).toFixed(decimals)));
      if (t < 1) requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  }, [visible, end, decimals, duration]);
  return { ref, value };
}

/** Subtle cursor-tracked 3D tilt for a card — perspective + rotateX/rotateY
 * while hovering, springing back flat on mouse leave. */
export function useTilt<T extends HTMLElement = HTMLDivElement>(maxDeg = 5) {
  const ref = useRef<T>(null);
  const [style, setStyle] = useState<React.CSSProperties>({});
  const onMouseMove = (e: React.MouseEvent<T>) => {
    const el = ref.current;
    if (!el) return;
    const rect = el.getBoundingClientRect();
    const px = (e.clientX - rect.left) / rect.width;
    const py = (e.clientY - rect.top) / rect.height;
    const rotateY = (px - 0.5) * maxDeg * 2;
    const rotateX = (0.5 - py) * maxDeg * 2;
    setStyle({ transform: `perspective(900px) rotateX(${rotateX}deg) rotateY(${rotateY}deg)`, transition: "transform 60ms linear" });
  };
  const onMouseLeave = () => {
    setStyle({ transform: "perspective(900px) rotateX(0deg) rotateY(0deg)", transition: "transform 450ms cubic-bezier(0.22, 1, 0.36, 1)" });
  };
  return { ref, style, onMouseMove, onMouseLeave };
}

/** A button/element that gently pulls toward the cursor while hovered,
 * springing back to rest on mouse leave. Reserved for the handful of
 * highest-intent CTAs — used everywhere it'd read as a gimmick, not a nicety. */
export function useMagnetic(strength = 0.3) {
  const ref = useRef<HTMLElement>(null);
  const [style, setStyle] = useState<React.CSSProperties>({});
  const onMouseMove = (e: React.MouseEvent) => {
    const el = ref.current;
    if (!el) return;
    const rect = el.getBoundingClientRect();
    const x = (e.clientX - rect.left - rect.width / 2) * strength;
    const y = (e.clientY - rect.top - rect.height / 2) * strength;
    setStyle({ transform: `translate(${x}px, ${y}px)`, transition: "transform 120ms ease-out" });
  };
  const onMouseLeave = () => {
    setStyle({ transform: "translate(0px, 0px)", transition: "transform 500ms cubic-bezier(0.34, 1.56, 0.64, 1)" });
  };
  return { ref, style, onMouseMove, onMouseLeave };
}
