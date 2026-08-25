import type { CSSProperties } from "react";

/**
 * Forces an element onto its own GPU compositor layer. Any fixed/sticky
 * element that also uses backdrop-filter needs this — otherwise WebKit
 * (iOS/macOS Safari, and Chrome on iOS which is Safari underneath) can fail
 * to repaint that layer after a sibling blur layer (a modal overlay, drawer,
 * notification panel) mounts and then unmounts, leaving it invisible until a
 * manual scroll or hard refresh forces a fresh paint. Apply to every
 * `backdrop-blur-*` element that is also `fixed`/`sticky`.
 */
export const GPU_LAYER_STYLE: CSSProperties = {
  transform: "translateZ(0)",
  WebkitBackfaceVisibility: "hidden",
  backfaceVisibility: "hidden",
};
