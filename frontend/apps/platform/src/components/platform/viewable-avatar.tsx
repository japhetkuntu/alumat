"use client";

import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { Expand, UserAvatar, X, cn } from "@alumni/ui";

type AvatarSize = "xs" | "sm" | "default" | "lg" | "xl";

function PhotoViewer({ src, name, caption, onClose }: { src: string; name: string; caption?: string | null; onClose: () => void }) {
  const [loaded, setLoaded] = useState(false);
  const [actual, setActual] = useState(false);   // false: fitted to the screen; true: at its real size, scrollable

  // Escape closes, and the page behind stays still while the photo is up.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", onKey);
    return () => { document.body.style.overflow = prev; window.removeEventListener("keydown", onKey); };
  }, [onClose]);

  return createPortal(
    <div role="dialog" aria-modal="true" aria-label={`${name}'s picture`} onClick={onClose}
      className="fixed inset-0 z-[10050] flex flex-col items-center justify-center gap-5 bg-black/75 px-4 py-6 backdrop-blur-md animate-in fade-in duration-200"
      style={{ paddingTop: "max(1.5rem, env(safe-area-inset-top))", paddingBottom: "max(1.5rem, env(safe-area-inset-bottom))" }}>
      <button type="button" onClick={onClose} aria-label="Close"
        className="absolute right-4 top-4 grid h-10 w-10 place-items-center rounded-full bg-white/12 text-white outline-none ring-1 ring-white/20 transition hover:bg-white/22 focus-visible:ring-2 focus-visible:ring-white"
        style={{ top: "max(1rem, env(safe-area-inset-top))" }}>
        <X size={18} />
      </button>

      <figure className="flex max-h-full min-h-0 flex-col items-center gap-4" onClick={(e) => e.stopPropagation()}>
        <div className={cn("relative min-h-0 rounded-[22px] bg-white/5 shadow-[0_30px_80px_-20px_rgba(0,0,0,0.7)] ring-1 ring-white/15 animate-in zoom-in-95 duration-300",
          actual ? "max-h-[72vh] max-w-[94vw] overflow-auto overscroll-contain" : "overflow-hidden")}>
          {!loaded && <div className="absolute inset-0 animate-pulse bg-white/10" aria-hidden />}
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src={src} alt={`${name}'s profile picture`} onLoad={() => setLoaded(true)}
            className={cn("block transition-opacity duration-300", loaded ? "opacity-100" : "opacity-0",
              actual ? "max-h-none max-w-none" : "max-h-[72vh] w-auto max-w-[min(92vw,640px)] object-contain")}
            style={{ minHeight: loaded ? undefined : 280, minWidth: loaded ? undefined : 240 }} />
        </div>
        <figcaption className="flex flex-col items-center gap-2 text-center">
          <span className="text-[17px] font-semibold tracking-tight text-white">{name}</span>
          {caption && <span className="text-[13px] text-white/65">{caption}</span>}
          <button type="button" onClick={() => setActual((v) => !v)}
            className="mt-1 inline-flex items-center gap-1.5 rounded-full px-3.5 py-1.5 text-[12.5px] font-medium text-white/80 outline-none ring-1 ring-white/20 transition hover:bg-white/10 hover:text-white focus-visible:ring-2 focus-visible:ring-white">
            <Expand size={12} /> {actual ? "Fit to screen" : "View original size"}
          </button>
        </figcaption>
      </figure>
    </div>,
    document.body,
  );
}

/**
 * A member's picture that opens full size when clicked, so an administrator can look at the whole photo (to check it is a real,
 * suitable portrait, for instance) instead of the small circle. Without a picture it is just the initials and does nothing.
 */
export function ViewableAvatar({ src, name, caption, size, className }: { src?: string | null; name: string; caption?: string | null; size?: AvatarSize; className?: string }) {
  const [open, setOpen] = useState(false);
  if (!src) return <UserAvatar src={src} name={name} size={size} className={className} />;
  return (
    <>
      <button type="button" onClick={(e) => { e.stopPropagation(); setOpen(true); }} aria-label={`View ${name}'s picture full size`} title="View full picture"
        className="shrink-0 cursor-zoom-in rounded-full outline-none transition-transform hover:scale-[1.04] focus-visible:ring-2 focus-visible:ring-primary">
        <UserAvatar src={src} name={name} size={size} className={className} />
      </button>
      {open && <PhotoViewer src={src} name={name} caption={caption} onClose={() => setOpen(false)} />}
    </>
  );
}
