"use client";

import * as React from "react";

/**
 * Drag-down-to-close for a bottom sheet, the way every phone app behaves. Attach `ref` to the sheet (DialogContent) and spread `bind` on
 * the part you want to drag by: the grip and the title row. Past 90px, or a quick flick, the sheet closes; otherwise it springs back.
 * Touch only, so it does nothing on a mouse, and it works the same on iPhone and Android.
 */
export function useSwipeDownToClose(onClose: () => void) {
  const ref = React.useRef<HTMLDivElement>(null);
  const startY = React.useRef<number | null>(null);
  const startAt = React.useRef(0);
  const dy = React.useRef(0);

  const bind = {
    onTouchStart: (e: React.TouchEvent) => {
      startY.current = e.touches[0].clientY;
      startAt.current = Date.now();
      dy.current = 0;
      if (ref.current) ref.current.style.transition = "none";
    },
    onTouchMove: (e: React.TouchEvent) => {
      if (startY.current == null) return;
      dy.current = Math.max(0, e.touches[0].clientY - startY.current);
      if (ref.current) ref.current.style.transform = `translateY(${dy.current}px)`;
    },
    onTouchEnd: () => {
      if (startY.current == null) return;
      startY.current = null;
      const el = ref.current;
      const flick = dy.current > 40 && Date.now() - startAt.current < 220;
      if (dy.current > 90 || flick) {
        onClose();
        return;
      }
      if (el) {
        el.style.transition = "transform 180ms ease";
        el.style.transform = "";
        window.setTimeout(() => { if (el) el.style.transition = ""; }, 200);
      }
    },
    onTouchCancel: () => {
      startY.current = null;
      if (ref.current) { ref.current.style.transition = "transform 180ms ease"; ref.current.style.transform = ""; }
    },
  };

  return { ref, bind };
}
