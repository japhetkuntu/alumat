"use client";

import * as React from "react";
import { ExternalLink } from "./icons";
import { YouTubeEmbed } from "./youtube-embed";
import { ZoomableImage } from "./media-gallery";
import { cn } from "../lib/utils";

export type ResourcePreviewKind = "youtube" | "image" | "pdf" | "office" | "video" | "audio" | "none";

const EXTENSIONS: Record<string, ResourcePreviewKind> = {
  png: "image", jpg: "image", jpeg: "image", gif: "image", webp: "image", svg: "image", avif: "image",
  pdf: "pdf",
  doc: "office", docx: "office", ppt: "office", pptx: "office", xls: "office", xlsx: "office",
  mp4: "video", webm: "video", mov: "video", m4v: "video",
  mp3: "audio", wav: "audio", m4a: "audio", ogg: "audio",
};

/** What kind of preview a resource address can have, from the file type at the end of the address. */
export function resourcePreviewKind(url?: string | null): ResourcePreviewKind {
  if (!url) return "none";
  if (/(youtube\.com|youtu\.be)/i.test(url)) return "youtube";
  let path = url;
  try { path = decodeURIComponent(new URL(url).pathname); } catch { path = url.split(/[?#]/)[0]; }
  const ext = /\.([a-z0-9]+)$/i.exec(path)?.[1]?.toLowerCase();
  return (ext && EXTENSIONS[ext]) || "none";
}

/** Phones and tablets mostly download a PDF shown in a frame instead of displaying it, so they get a viewer that works there. */
function isTouchBrowser() {
  return typeof navigator !== "undefined" && /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent);
}

/**
 * Shows a resource in the page instead of only offering a download: PDFs, Word, PowerPoint and Excel files, images, video,
 * audio and YouTube. Anything else renders nothing, so the caller can show its own card. A link to open the file in a new tab
 * always sits underneath, for the case where the browser or the network will not display it.
 */
export function ResourcePreview({ url, title, className }: { url: string; title: string; className?: string }) {
  const kind = resourcePreviewKind(url);
  const [touch, setTouch] = React.useState(false);
  React.useEffect(() => setTouch(isTouchBrowser()), []);
  if (kind === "none") return null;

  const frame = "w-full h-[420px] sm:h-[560px] bg-background border-0";
  let body: React.ReactNode = null;
  switch (kind) {
    case "youtube": body = <YouTubeEmbed url={url} />; break;
    case "image": body = <ZoomableImage src={url} alt={title} className="w-full max-h-[520px] object-contain bg-background" />; break;
    case "pdf":
      body = <iframe
        src={touch ? `https://docs.google.com/viewer?embedded=true&url=${encodeURIComponent(url)}` : url}
        className={frame} title={`${title} (PDF)`} loading="lazy" />;
      break;
    case "office":
      body = <iframe src={`https://view.officeapps.live.com/op/embed.aspx?src=${encodeURIComponent(url)}`} className={frame} title={title} loading="lazy" />;
      break;
    case "video": body = <video src={url} controls preload="metadata" className="w-full max-h-[520px] bg-black" />; break;
    case "audio": body = <audio src={url} controls preload="metadata" className="w-full p-3" />; break;
  }

  return (
    <div className={cn("space-y-2", className)}>
      <div className="overflow-hidden border border-border/40 bg-muted/20">{body}</div>
      {kind !== "youtube" && (
        <a href={url} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1.5 text-[13px] font-semibold text-primary hover:underline">
          <ExternalLink size={13} /> Not showing? Open it in a new tab
        </a>
      )}
    </div>
  );
}
