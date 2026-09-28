"use client";

import * as React from "react";
import { cn } from "../lib/utils";

interface FitImageProps {
  style?: React.CSSProperties;
  src: string;
  alt: string;
  /** Classes for the outer frame. It must have a size (e.g. absolute inset-0, or an aspect ratio). */
  className?: string;
  /** Classes for the visible photo itself, e.g. a hover transition. */
  imgClassName?: string;
  /**
   * How different the photo's shape may be from the frame's before we stop cropping it. 0.35 means a photo that
   * would lose more than about a quarter of itself to cropping is shown whole instead.
   */
  tolerance?: number;
  /** Where to anchor the crop when we do fill the frame. */
  objectPosition?: string;
}

/**
 * A photo that fits whatever frame it is put in. If the photo's shape is close to the frame's, it fills the frame
 * (a normal crop). If it is very different, such as a tall portrait in a wide banner, the whole photo is shown
 * over a soft, blurred copy of itself, so nothing is badly cropped and there are no empty bands. The choice is
 * made per image, and again if the frame changes size (for example across screen widths).
 */
export function FitImage({ src, alt, className, style, imgClassName, tolerance = 0.35, objectPosition = "center" }: FitImageProps) {
  const frameRef = React.useRef<HTMLDivElement>(null);
  const [natural, setNatural] = React.useState<{ w: number; h: number } | null>(null);
  const [frame, setFrame] = React.useState<{ w: number; h: number } | null>(null);

  React.useEffect(() => {
    const el = frameRef.current;
    if (!el) return;
    const measure = () => {
      const r = el.getBoundingClientRect();
      if (r.width > 0 && r.height > 0) setFrame({ w: r.width, h: r.height });
    };
    measure();
    if (typeof ResizeObserver === "undefined") return;
    const ro = new ResizeObserver(measure);
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  // An image that finished loading before React hydrated never fires onLoad (common with server-rendered pages and
  // cached photos), so also read its size directly once mounted.
  const imgRef = React.useRef<HTMLImageElement>(null);
  React.useEffect(() => {
    const img = imgRef.current;
    if (img && img.complete && img.naturalWidth > 0) setNatural({ w: img.naturalWidth, h: img.naturalHeight });
    else setNatural(null);
  }, [src]);

  // Until we know both shapes, fill the frame: that is what almost every photo wants and avoids a visible jump.
  const fits = React.useMemo(() => {
    if (!natural || !frame) return true;
    const ratio = natural.w / natural.h / (frame.w / frame.h);
    return Math.abs(ratio - 1) <= tolerance || Math.abs(1 / ratio - 1) <= tolerance;
  }, [natural, frame, tolerance]);

  return (
    <div ref={frameRef} className={cn("relative overflow-hidden", className)} style={style}>
      {!fits && (
        <img src={src} alt="" aria-hidden="true" className="absolute inset-0 h-full w-full scale-125 object-cover opacity-60 blur-2xl" />
      )}
      <img
        ref={imgRef}
        src={src}
        alt={alt}
        onLoad={(e) => setNatural({ w: e.currentTarget.naturalWidth, h: e.currentTarget.naturalHeight })}
        className={cn("absolute inset-0 h-full w-full", fits ? "object-cover" : "object-contain", imgClassName)}
        style={fits ? { objectPosition } : undefined}
      />
    </div>
  );
}
