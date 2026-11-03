"use client";

import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import { portalPath } from "./blocks";

export interface MenuItem { id: string; label: string }

interface HeaderProps {
  logoUrl?: string | null;
  mark: React.ReactNode;
  name: string;
  items: MenuItem[];
  overlay?: boolean;
  sticky?: boolean;
  /** In the editor the page scrolls inside a frame, so scrolling is done by the element rather than the browser's address. */
  inEditor?: boolean;
  /** "/" on pages other than the home page, so menu items lead back to their section on the home page. */
  base?: string;
}

/**
 * The top bar. It shows as many menu items as fit, and tucks the rest under "More", measured from the real width of the bar so it adapts
 * to a long name, a narrow window or a phone with nobody setting anything up. On a phone the Menu button opens the full list.
 */
export function SiteHeader({ logoUrl, mark, name, items, overlay, sticky, inEditor, base = "" }: HeaderProps) {
  const hdr = useRef<HTMLElement>(null), menu = useRef<HTMLElement>(null), toggle = useRef<HTMLButtonElement>(null), sign = useRef<HTMLAnchorElement>(null), more = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false), [popOpen, setPopOpen] = useState(false);
  const [tucked, setTucked] = useState<MenuItem[]>([]);
  const [active, setActive] = useState<string | null>(null);
  const [scrolled, setScrolled] = useState(false);

  const fit = useCallback(() => {
    const h = hdr.current, m = menu.current, t = toggle.current, s = sign.current, mo = more.current;
    if (!h || !m || !t || !s || !mo) return;
    const els = [...h.querySelectorAll<HTMLElement>(".mitems>.mi")];
    els.forEach((e) => { e.style.display = ""; });
    mo.hidden = true; setTucked([]);
    if (getComputedStyle(t).display !== "none") return; // phone: the whole list lives in the Menu panel
    const gap = 4, avail = m.clientWidth - s.offsetWidth - 16;
    const widths = els.map((e) => e.offsetWidth + gap);
    let total = widths.reduce((a, b) => a + b, 0);
    if (total <= avail) return;
    mo.hidden = false;
    const room = avail - (mo.offsetWidth + gap);
    const hide: number[] = [];
    for (let k = els.length - 1; k >= 1 && total > room; k--) { total -= widths[k]; hide.unshift(k); } // "Home" (index 0) always stays
    hide.forEach((k) => { els[k].style.display = "none"; });
    setTucked(hide.map((k) => items[k - 1]).filter(Boolean));
  }, [items]);

  useLayoutEffect(() => { fit(); }, [fit]);
  useEffect(() => {
    const h = hdr.current; if (!h) return;
    const ro = new ResizeObserver(() => { fit(); requestAnimationFrame(fit); });
    ro.observe(h);
    document.fonts?.ready.then(fit);
    return () => ro.disconnect();
  }, [fit]);

  // Which section is on screen, for the highlighted menu item; and a slightly tighter bar once the page has moved.
  useEffect(() => {
    let raf = 0;
    const scroller: HTMLElement | Window = inEditor ? (hdr.current?.closest("[data-site-scroller]") as HTMLElement) ?? window : window;
    const top = () => (scroller instanceof Window ? 0 : scroller.getBoundingClientRect().top);
    const spy = () => {
      const line = top() + (sticky ? (hdr.current?.offsetHeight ?? 0) : 0) + 80;
      let cur: string | null = null;
      for (const it of items) { const el = document.getElementById("sec-" + it.id); if (el && el.getBoundingClientRect().top <= line) cur = it.id; }
      setActive(cur);
      setScrolled((scroller instanceof Window ? window.scrollY : scroller.scrollTop) > 12);
    };
    const on = () => { cancelAnimationFrame(raf); raf = requestAnimationFrame(spy); };
    scroller.addEventListener("scroll", on, { passive: true }); spy();
    return () => { scroller.removeEventListener("scroll", on); cancelAnimationFrame(raf); };
  }, [items, sticky, inEditor]);

  // Close the "More" popover with Escape or a click outside it.
  useEffect(() => {
    if (!popOpen) return;
    const away = (e: MouseEvent) => { if (!more.current?.contains(e.target as Node)) setPopOpen(false); };
    const esc = (e: KeyboardEvent) => { if (e.key === "Escape") setPopOpen(false); };
    document.addEventListener("mousedown", away); document.addEventListener("keydown", esc);
    return () => { document.removeEventListener("mousedown", away); document.removeEventListener("keydown", esc); };
  }, [popOpen]);

  const go = (e: React.MouseEvent, id?: string) => {
    e.preventDefault(); setOpen(false); setPopOpen(false);
    const el = id ? document.getElementById("sec-" + id) : document.getElementById("alsite-top");
    if (!el && base) { window.location.href = id ? `${base}#sec-${id}` : base; return; }
    el?.scrollIntoView({ behavior: matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth", block: "start" });
  };

  return (
    <header ref={hdr} id="hdr" className={`hdr ${overlay ? "overlay" : ""} ${sticky && !overlay ? "sticky" : ""} ${scrolled ? "scrolled" : ""} ${open ? "open" : ""}`}>
      <a className="logo" href={base ? base : "#alsite-top"} onClick={(e) => go(e)} aria-label={`${name}, back to the top`}>
        {logoUrl ? <img src={logoUrl} alt={name} /> : <>{mark}<span className="lname">{name}</span></>}
      </a>
      <button ref={toggle} type="button" className="nav-toggle" aria-expanded={open} aria-controls="alsite-menu" onClick={() => setOpen((o) => !o)}>Menu</button>
      <nav ref={menu} id="alsite-menu" className="menu" aria-label="Main">
        <div className="mitems">
          <div className="mi"><a href={base ? base : "#alsite-top"} className={active === null ? "on" : ""} onClick={(e) => go(e)}>Home</a></div>
          {items.map((it) => (
            <div className="mi" key={it.id}><a href={`${base}#sec-${it.id}`} title={it.label} className={active === it.id ? "on" : ""} onClick={(e) => go(e, it.id)}>{it.label}</a></div>
          ))}
        </div>
        <div ref={more} className={`mi mmore ${popOpen ? "open" : ""}`} hidden>
          <button type="button" className="mg" aria-haspopup="true" aria-expanded={popOpen} onClick={() => setPopOpen((o) => !o)}>More</button>
          <div className="mpop">{tucked.map((it) => <a key={it.id} href={`${base}#sec-${it.id}`} className={active === it.id ? "on" : ""} onClick={(e) => go(e, it.id)}>{it.label}</a>)}</div>
        </div>
        <a ref={sign} className="signin" href={portalPath.signin}>Member sign in</a>
      </nav>
    </header>
  );
}
