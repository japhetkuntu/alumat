"use client";

import { useEffect } from "react";

const GRIDS = ".steps,.bal,.gal,.ppl,.stats,.news-list,.ev-list,.rowlist,.split,.contact,.strip,.cols2";

/**
 * Sections rise in as they are reached, and numbers count up once. Everything is visible without JavaScript and for anyone who asks for
 * less motion; sections already on screen when the page opens are simply shown, so nothing flashes.
 */
export function Motion({ rootId, enabled = true }: { rootId: string; enabled?: boolean }) {
  useEffect(() => {
    const root = document.getElementById(rootId);
    if (!root || !enabled) return;
    const calm = !("IntersectionObserver" in window) || matchMedia("(prefers-reduced-motion: reduce)").matches;
    root.classList.add("as-js");
    const secs = [...root.querySelectorAll<HTMLElement>(".sec")];
    secs.forEach((sec) => {
      let i = 0;
      sec.querySelectorAll<HTMLElement>(".wrap").forEach((w) => [...w.children].forEach((ch) => {
        if ((ch as HTMLElement).matches(".itadd,.itx,.emptybox")) return;
        if ((ch as HTMLElement).matches(GRIDS)) [...ch.children].forEach((it) => { it.classList.add("rv"); (it as HTMLElement).style.setProperty("--i", String(i++)); });
        else { ch.classList.add("rv"); (ch as HTMLElement).style.setProperty("--i", String(i++)); }
      }));
    });
    const count = (el: HTMLElement) => {
      const to = Number(el.dataset.count); const raw = el.textContent ?? "";
      if (!to || calm) return;
      const t0 = performance.now();
      const tick = (t: number) => { const k = Math.min(1, (t - t0) / 1000), e = 1 - Math.pow(1 - k, 3); el.textContent = Math.round(to * e).toLocaleString("en-GB"); if (k < 1) requestAnimationFrame(tick); else el.textContent = raw; };
      requestAnimationFrame(tick);
    };
    const show = (sec: HTMLElement, instant: boolean) => {
      sec.classList.add("in"); if (instant) sec.classList.add("settled");
      else setTimeout(() => sec.classList.add("settled"), 1800);
      sec.querySelectorAll<HTMLElement>("[data-count]").forEach(count);
    };
    if (calm) { secs.forEach((s) => show(s, true)); return; }
    const vh = window.innerHeight;
    const io = new IntersectionObserver((es) => es.forEach((e) => { if (e.isIntersecting) { io.unobserve(e.target); show(e.target as HTMLElement, false); } }), { threshold: 0.1, rootMargin: "0px 0px -5% 0px" });
    secs.forEach((s) => { if (s.getBoundingClientRect().top < vh * 0.9) show(s, true); else io.observe(s); });
    return () => { io.disconnect(); root.classList.remove("as-js"); };
  }, [rootId, enabled]);
  return null;
}
