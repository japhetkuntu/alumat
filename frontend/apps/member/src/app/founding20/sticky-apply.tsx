"use client";

import { useEffect, useState } from "react";
import { ArrowRight } from "@alumni/ui";

/** Phone-only call to action: shows after the hero scrolls away and steps aside once the form is on screen. */
export function StickyApply() {
  const [show, setShow] = useState(false);

  useEffect(() => {
    const hero = document.getElementById("f20-hero");
    const form = document.getElementById("apply");
    const footer = document.querySelector("footer");
    if (!hero || !form || !footer || typeof IntersectionObserver === "undefined") return;
    let heroGone = false;
    let formSeen = false;
    let footerSeen = false;
    const update = () => setShow(heroGone && !formSeen && !footerSeen);
    const heroObs = new IntersectionObserver(([e]) => { heroGone = !e.isIntersecting; update(); });
    const formObs = new IntersectionObserver(([e]) => { formSeen = e.isIntersecting; update(); }, { threshold: 0.05 });
    const footerObs = new IntersectionObserver(([e]) => { footerSeen = e.isIntersecting; update(); });
    heroObs.observe(hero);
    footerObs.observe(footer);
    formObs.observe(form);
    return () => { heroObs.disconnect(); formObs.disconnect(); footerObs.disconnect(); };
  }, []);

  return (
    <div
      aria-hidden={!show}
      className={`fixed inset-x-0 bottom-0 z-40 border-t border-border bg-background/95 px-4 py-3 backdrop-blur transition-transform duration-300 sm:hidden ${show ? "translate-y-0" : "translate-y-full"}`}
      style={{ paddingBottom: "max(0.75rem, env(safe-area-inset-bottom))" }}
    >
      <a href="#apply" tabIndex={show ? 0 : -1} className="flex min-h-11 w-full items-center justify-center gap-2 bg-primary text-[15px] font-semibold text-primary-foreground">
        Apply for Founding 20 <ArrowRight size={16} />
      </a>
    </div>
  );
}
