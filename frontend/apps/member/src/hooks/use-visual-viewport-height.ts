"use client";

import { useEffect, useState } from "react";

/**
 * The actual visible viewport height, tracked live via the VisualViewport
 * API where available. `100dvh` is meant to solve exactly this, but real
 * mobile browsers are inconsistent about it — some don't shrink it for their
 * own bottom toolbar until a scroll/resize event fires, so a full-screen
 * overlay sized with `100dvh` can render a bit short, leaving a gap at the
 * bottom where the page underneath (including any fixed bottom nav) shows
 * through. This bug is invisible in desktop/simulator emulation, which
 * doesn't reproduce real mobile browser chrome. `visualViewport.height` is a
 * live, JS-computed pixel value that isn't subject to that per-browser
 * CSS-unit disagreement, so it's used as an authoritative override.
 */
export function useVisualViewportHeight(): number | null {
  const [height, setHeight] = useState<number | null>(null);

  useEffect(() => {
    const vv = window.visualViewport;
    if (!vv) return;
    const update = () => setHeight(vv.height);
    update();
    vv.addEventListener("resize", update);
    vv.addEventListener("scroll", update);
    return () => {
      vv.removeEventListener("resize", update);
      vv.removeEventListener("scroll", update);
    };
  }, []);

  return height;
}
