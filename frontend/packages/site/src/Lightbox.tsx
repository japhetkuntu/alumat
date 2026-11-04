"use client";

import { useCallback, useEffect, useRef, useState } from "react";

/** The photos a visitor may open larger: the page's own photographs, not logos, icons or pictures that are themselves links. */
const OPENABLE = ".img > img, img.readhero, .readgallery img";
const SKIP = "a, button, [class*='hero-'], .herophoto, .hdr, .ft, .logoimg, .mark, .lbox";

interface Shot { src: string; alt: string }

/** The large version of a photo: the same picture asked for at screen size where the host allows it. */
const full = (src: string) => {
  try {
    const u = new URL(src);
    if (u.hostname !== "images.unsplash.com") return src;
    u.searchParams.set("w", "2000"); u.searchParams.set("q", "80"); u.searchParams.set("auto", "format"); u.searchParams.delete("fit");
    return u.toString();
  } catch { return src; }
};

/**
 * Click a photo on the page to see it large, as in the member portal. Arrow keys and the side buttons move through the photos of the
 * same section, Escape or a click outside closes it, and focus returns to where the visitor was.
 */
export function Lightbox({ rootId, enabled = true }: { rootId: string; enabled?: boolean }) {
  const [open, setOpen] = useState<{ shots: Shot[]; i: number } | null>(null);
  const back = useRef<HTMLElement | null>(null);
  const closeBtn = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    const root = document.getElementById(rootId);
    if (!root || !enabled) return;
    const eligible = () => [...root.querySelectorAll<HTMLImageElement>(OPENABLE)].filter((im) => !im.closest(SKIP));
    const mark = () => eligible().forEach((im) => { im.style.cursor = "zoom-in"; });
    mark();
    const start = (im: HTMLImageElement) => {
      const scope = im.closest("section, main") ?? root;
      const group = eligible().filter((x) => (x.closest("section, main") ?? root) === scope);
      back.current = document.activeElement as HTMLElement | null;
      setOpen({ shots: group.map((g) => ({ src: full(g.currentSrc || g.src), alt: g.alt })), i: Math.max(0, group.indexOf(im)) });
    };
    const click = (e: MouseEvent) => {
      const im = (e.target as HTMLElement).closest?.("img") as HTMLImageElement | null;
      if (im && eligible().includes(im)) start(im);
    };
    root.addEventListener("click", click);
    return () => root.removeEventListener("click", click);
  }, [rootId, enabled]);

  const close = useCallback(() => { setOpen(null); back.current?.focus?.(); }, []);
  const step = useCallback((d: number) => setOpen((o) => o && { ...o, i: (o.i + d + o.shots.length) % o.shots.length }), []);

  useEffect(() => {
    if (!open) return;
    closeBtn.current?.focus();
    const prev = document.body.style.overflow; document.body.style.overflow = "hidden";
    const key = (e: KeyboardEvent) => { if (e.key === "Escape") close(); else if (e.key === "ArrowRight") step(1); else if (e.key === "ArrowLeft") step(-1); };
    window.addEventListener("keydown", key);
    return () => { window.removeEventListener("keydown", key); document.body.style.overflow = prev; };
  }, [open, close, step]);

  if (!open) return null;
  const shot = open.shots[open.i], many = open.shots.length > 1;
  return (
    <div className="lbox" role="dialog" aria-modal="true" aria-label="Photo viewer" onClick={close}>
      <button ref={closeBtn} type="button" className="lbx" onClick={close} aria-label="Close">×</button>
      {many && <button type="button" className="lbn prev" onClick={(e) => { e.stopPropagation(); step(-1); }} aria-label="Previous photo">‹</button>}
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img src={shot.src} alt={shot.alt} onClick={(e) => e.stopPropagation()} />
      {many && <button type="button" className="lbn next" onClick={(e) => { e.stopPropagation(); step(1); }} aria-label="Next photo">›</button>}
      {many && <span className="lbc" aria-live="polite">{open.i + 1} / {open.shots.length}</span>}
    </div>
  );
}
