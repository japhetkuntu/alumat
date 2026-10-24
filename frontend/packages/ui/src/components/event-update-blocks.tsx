"use client";

import { Fragment, useState } from "react";
import { cn, fileKind, formatFileSize } from "../lib/utils";
import { ChevronDown, Download, ExternalLink, Link2 } from "./icons";
import { MediaGallery } from "./media-gallery";
import { YouTubeEmbed } from "./youtube-embed";

/** One part of an event update, as the portals receive it. Which fields are used depends on the type. */
export interface EventUpdateBlockData {
  id: string;
  type: "Text" | "Images" | "Video" | "Link" | "Files";
  heading?: string;
  text?: string;
  url?: string;
  label?: string;
  items: { url: string; name: string; sizeBytes: number; contentType?: string }[];
}

const PHOTOS_SHOWN = 6;

/** Web addresses inside text become links. Only http and https are ever linked. */
function Linkified({ text }: { text: string }) {
  const parts = text.split(/(https?:\/\/[^\s<]+)/g);
  return (
    <>
      {parts.map((part, i) => /^https?:\/\//.test(part)
        ? <a key={i} href={part} target="_blank" rel="noopener noreferrer" className="break-all font-medium text-primary underline underline-offset-2">{part}</a>
        : <Fragment key={i}>{part}</Fragment>)}
    </>
  );
}

function domainOf(url: string): string {
  try { return new URL(url).hostname.replace(/^www\./, ""); } catch { return url; }
}

function Photos({ block }: { block: EventUpdateBlockData }) {
  const [all, setAll] = useState(false);
  const urls = block.items.map((i) => i.url);
  const shown = all ? urls : urls.slice(0, PHOTOS_SHOWN);
  return (
    <div className="space-y-2">
      <MediaGallery imageUrls={shown} />
      {urls.length > PHOTOS_SHOWN && (
        <button type="button" onClick={() => setAll((v) => !v)}
          className="flex w-full items-center justify-center gap-1.5 border border-border bg-card py-2.5 text-[13px] font-semibold text-foreground transition-colors hover:border-primary/50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary">
          {all ? "Show fewer photos" : `Show all ${urls.length} photos`}
          <ChevronDown size={14} className={cn("transition-transform", all && "rotate-180")} />
        </button>
      )}
    </div>
  );
}

/** One block of an event update, drawn the way attendees see it. Shared by the member portal and the admins' own preview. */
export function EventUpdateBlockView({ block }: { block: EventUpdateBlockData }) {
  return (
    <div className="space-y-2.5">
      {block.heading && <h4 className="text-[14px] font-semibold text-foreground">{block.heading}</h4>}

      {block.type === "Text" && block.text && (
        <p className="whitespace-pre-line text-[14.5px] leading-relaxed text-foreground/90"><Linkified text={block.text} /></p>
      )}

      {block.type === "Images" && block.items.length > 0 && <Photos block={block} />}

      {block.type === "Video" && block.url && <YouTubeEmbed url={block.url} />}

      {block.type === "Link" && block.url && (
        <a href={block.url} target="_blank" rel="noopener noreferrer"
          className="group flex items-center gap-3 border border-border bg-card p-3.5 transition-colors hover:border-primary/50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary">
          <span className="flex size-10 shrink-0 items-center justify-center bg-primary/10 text-primary"><Link2 size={16} /></span>
          <span className="min-w-0 flex-1">
            <span className="block truncate text-[14px] font-semibold group-hover:text-primary">{block.label || domainOf(block.url)}</span>
            {block.text && <span className="block text-[13px] text-muted-foreground">{block.text}</span>}
            <span className="block truncate text-[12px] text-muted-foreground">{domainOf(block.url)}</span>
          </span>
          <ExternalLink size={15} className="shrink-0 text-muted-foreground group-hover:text-primary" />
        </a>
      )}

      {block.type === "Files" && block.items.length > 0 && (
        <ul className="divide-y divide-border border border-border bg-card">
          {block.items.map((f) => (
            <li key={f.url}>
              <a href={f.url} target="_blank" rel="noopener noreferrer" download={f.name}
                className="group flex items-center gap-3 px-3.5 py-3 transition-colors hover:bg-primary/5 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary">
                <span className="flex size-10 shrink-0 items-center justify-center bg-primary/10 text-[11px] font-bold uppercase text-primary">{fileKind(f.name).slice(0, 4)}</span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-[14px] font-medium group-hover:text-primary">{f.name}</span>
                  <span className="block text-[12px] text-muted-foreground">{fileKind(f.name)} · {formatFileSize(f.sizeBytes)}</span>
                </span>
                <Download size={16} className="shrink-0 text-muted-foreground group-hover:text-primary" />
              </a>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
