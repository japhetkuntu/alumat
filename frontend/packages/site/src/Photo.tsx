import type { ReactNode } from "react";
import { sized } from "./photos";
import type { EditApi } from "./edit";

interface PhotoProps {
  src?: string | null;
  alt?: string;
  className?: string;
  /** Pixel width to ask the image service for. */
  width?: number;
  /** The first photo on a page loads at once; the rest wait until they are close to the screen. */
  priority?: boolean;
  position?: string;
  edit?: EditApi;
  sectionId?: string;
  slot?: string;
  /** What shows in an empty slot while editing. */
  prompt?: string;
  children?: ReactNode;
}

/**
 * A photo slot. Visitors get a normal lazy image over a soft shimmer. In the editor the same slot is a button: click to add or replace,
 * and a small control removes it. An empty slot is only drawn in the editor, so a live page never shows a hole.
 */
export function Photo({ src, alt = "", className = "", width = 900, priority, position, edit, sectionId, slot, prompt, children }: PhotoProps) {
  const url = sized(src, width);
  if (!edit) {
    if (!url) return null;
    return (
      <div className={`img ${className}`} style={position ? ({ "--pos": position } as React.CSSProperties) : undefined}>
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img src={url} alt={alt} loading={priority ? "eager" : "lazy"} decoding="async" {...(priority ? { fetchPriority: "high" as const } : {})} />
        {children}
      </div>
    );
  }
  const has = !!url;
  return (
    <div
      className={`img ${className} ${has ? "has" : "empty-slot"}`}
      style={position ? ({ "--pos": position } as React.CSSProperties) : undefined}
      role="button" tabIndex={0} aria-label={has ? "Replace photo" : "Add a photo"}
      data-img={slot}
      onClick={(e) => { e.stopPropagation(); edit.pickPhoto(sectionId!, slot!); }}
      onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); edit.pickPhoto(sectionId!, slot!); } }}
    >
      {/* eslint-disable-next-line @next/next/no-img-element */}
      {has && <img src={url} alt={alt} loading="lazy" decoding="async" />}
      <span className="slot-hint">{has ? "Replace photo" : prompt ?? "Click to add a photo"}</span>
      {has && <button type="button" className="rm" onClick={(e) => { e.stopPropagation(); edit.removePhoto(sectionId!, slot!); }}>Remove photo</button>}
      {children}
    </div>
  );
}
