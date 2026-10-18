"use client";

import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { AlignLeft, ArrowDown, ArrowUp, FileText, Images, Link2, Plus, Trash2, Upload, X, Youtube } from "@alumni/ui";
import { Button, Input, Label, Textarea, YouTubePreview, cn, fileKind, formatFileSize } from "@alumni/ui";
import type { EventUpdateBlockType } from "@/types";
import type { EventUpdateDraftBlock, EventUpdateDraftItem } from "@/lib/institution-api";

const IMAGE_TYPES = /\.(jpe?g|png|webp|gif)$/i;
const FILE_TYPES = /\.(pdf|docx?|xlsx?|pptx?|txt|csv|rtf|zip)$/i;
const MAX_IMAGE = 15 * 1024 * 1024;
const MAX_FILE = 30 * 1024 * 1024;

const BLOCKS: { type: EventUpdateBlockType; label: string; hint: string; icon: typeof AlignLeft }[] = [
  { type: "Text", label: "Text", hint: "Words, schedules, instructions", icon: AlignLeft },
  { type: "Images", label: "Photos", hint: "As many as you like", icon: Images },
  { type: "Video", label: "Video", hint: "A YouTube link", icon: Youtube },
  { type: "Link", label: "Link", hint: "A page or a map", icon: Link2 },
  { type: "Files", label: "Files", hint: "PDF, Word, Excel, PowerPoint", icon: FileText },
];

export function newBlockId() { return Math.random().toString(36).slice(2, 10); }

export function emptyBlock(type: EventUpdateBlockType): EventUpdateDraftBlock {
  return { id: newBlockId(), type, heading: "", text: "", url: "", label: "", items: [] };
}

/** The first thing wrong with a draft, in the organiser's words, or null. The server checks again. */
export function draftProblem(title: string, category: string, blocks: EventUpdateDraftBlock[]): string | null {
  if (!title.trim()) return "Give the update a title.";
  if (!category.trim()) return "Choose a category for this update.";
  if (blocks.length === 0) return "Add something to the update: text, photos, a video, a link or files.";
  for (const b of blocks) {
    if (b.type === "Text" && !b.text.trim()) return "One of the text parts is empty.";
    if (b.type === "Images" && b.items.length === 0) return "A photos part has no photos yet.";
    if (b.type === "Files" && b.items.length === 0) return "A files part has no files yet.";
    if (b.type === "Video" && !/(youtube\.com|youtu\.be)/i.test(b.url)) return "A video part needs a YouTube link.";
    if (b.type === "Link" && !/^https?:\/\/\S+/i.test(b.url.trim())) return `A link part needs an address starting with http:// or https://.`;
  }
  return null;
}

/**
 * Builds an update from parts. Any mix, any number, in any order: the organiser adds what they have, moves it around, and
 * removes what they don't need. Photos and files can be dropped straight onto a part.
 */
export function EventUpdateEditor({ blocks, onChange }: { blocks: EventUpdateDraftBlock[]; onChange: (next: EventUpdateDraftBlock[]) => void }) {
  // Photos added here are previewed from the file itself. Remember each preview so it can be released.
  const previews = useRef<Set<string>>(new Set());
  useEffect(() => {
    const held = previews.current;
    return () => { held.forEach((u) => URL.revokeObjectURL(u)); held.clear(); };
  }, []);

  const patch = (id: string, change: Partial<EventUpdateDraftBlock>) =>
    onChange(blocks.map((b) => (b.id === id ? { ...b, ...change } : b)));

  const move = (index: number, by: -1 | 1) => {
    const to = index + by;
    if (to < 0 || to >= blocks.length) return;
    const next = [...blocks];
    [next[index], next[to]] = [next[to], next[index]];
    onChange(next);
  };

  const removeBlock = (id: string) => {
    const gone = blocks.find((b) => b.id === id);
    gone?.items.forEach((i) => { if (i.previewUrl) { URL.revokeObjectURL(i.previewUrl); previews.current.delete(i.previewUrl); } });
    onChange(blocks.filter((b) => b.id !== id));
  };

  const addFiles = (block: EventUpdateDraftBlock, picked: File[]) => {
    const isImages = block.type === "Images";
    const accepted: EventUpdateDraftItem[] = [];
    for (const file of picked) {
      if (!(isImages ? IMAGE_TYPES : FILE_TYPES).test(file.name)) {
        toast.error(isImages ? `"${file.name}" isn't a photo. Use JPG, PNG, WebP or GIF.` : `"${file.name}" can't be attached. Use PDF, Word, Excel, PowerPoint, text, CSV, RTF or ZIP.`);
        continue;
      }
      if (file.size > (isImages ? MAX_IMAGE : MAX_FILE)) {
        toast.error(`"${file.name}" is bigger than ${(isImages ? MAX_IMAGE : MAX_FILE) / 1024 / 1024} MB.`);
        continue;
      }
      let previewUrl: string | undefined;
      if (isImages) { previewUrl = URL.createObjectURL(file); previews.current.add(previewUrl); }
      accepted.push({ key: newBlockId() + newBlockId(), name: file.name, sizeBytes: file.size, contentType: file.type, file, previewUrl });
    }
    if (accepted.length) patch(block.id, { items: [...block.items, ...accepted] });
  };

  const removeItem = (block: EventUpdateDraftBlock, key: string) => {
    const gone = block.items.find((i) => i.key === key);
    if (gone?.previewUrl) { URL.revokeObjectURL(gone.previewUrl); previews.current.delete(gone.previewUrl); }
    patch(block.id, { items: block.items.filter((i) => i.key !== key) });
  };

  return (
    <div className="space-y-3">
      {blocks.length === 0 && (
        <div className="border border-dashed border-border bg-muted/30 px-4 py-6 text-center">
          <p className="text-[14px] font-semibold">Start with whatever you have</p>
          <p className="mt-1 text-[13px] text-muted-foreground">Add text, photos, a video, a link or files below. Mix them in any order and add as many parts as you need.</p>
        </div>
      )}

      {blocks.map((b, index) => {
        const meta = BLOCKS.find((x) => x.type === b.type)!;
        const Icon = meta.icon;
        return (
          <div key={b.id} className="border border-border bg-card">
            <div className="flex items-center gap-2 border-b border-border px-3 py-2">
              <Icon size={14} className="shrink-0 text-primary" />
              <span className="text-[13px] font-semibold">{meta.label}</span>
              <span className="text-[12px] text-muted-foreground">Part {index + 1}</span>
              <span className="ml-auto flex items-center">
                <Button type="button" size="sm" variant="ghost" className="px-2" disabled={index === 0} onClick={() => move(index, -1)} aria-label={`Move part ${index + 1} up`}><ArrowUp size={14} /></Button>
                <Button type="button" size="sm" variant="ghost" className="px-2" disabled={index === blocks.length - 1} onClick={() => move(index, 1)} aria-label={`Move part ${index + 1} down`}><ArrowDown size={14} /></Button>
                <Button type="button" size="sm" variant="ghost" className="px-2 text-destructive" onClick={() => removeBlock(b.id)} aria-label={`Remove part ${index + 1}`}><Trash2 size={14} /></Button>
              </span>
            </div>
            <div className="space-y-3 p-3">
              <div className="space-y-1.5">
                <Label className="text-[12.5px]">Heading <span className="font-normal text-muted-foreground">(optional)</span></Label>
                <Input value={b.heading} maxLength={120} placeholder={b.type === "Images" ? "e.g. Friday night" : b.type === "Files" ? "e.g. Programme and slides" : "Add a heading"} onChange={(e) => patch(b.id, { heading: e.target.value })} />
              </div>

              {b.type === "Text" && (
                <div className="space-y-1.5">
                  <Textarea rows={5} maxLength={10000} placeholder="Write the update. Line breaks are kept, and web addresses become links." value={b.text} onChange={(e) => patch(b.id, { text: e.target.value })} />
                  <p className="text-right text-[12px] text-muted-foreground">{b.text.length.toLocaleString()} / 10,000</p>
                </div>
              )}

              {b.type === "Video" && (
                <div className="space-y-2">
                  <Input type="url" placeholder="https://www.youtube.com/watch?v=…" value={b.url} onChange={(e) => patch(b.id, { url: e.target.value })} />
                  {b.url.trim() && <YouTubePreview url={b.url} className="max-w-[320px]" />}
                  {b.url.trim() && !/(youtube\.com|youtu\.be)/i.test(b.url) && <p className="text-[12.5px] text-destructive">Only YouTube links work for videos.</p>}
                </div>
              )}

              {b.type === "Link" && (
                <div className="space-y-3">
                  <div className="space-y-1.5"><Label className="text-[12.5px]">Address</Label>
                    <Input type="url" placeholder="https://…" value={b.url} onChange={(e) => patch(b.id, { url: e.target.value })} /></div>
                  <div className="space-y-1.5"><Label className="text-[12.5px]">What to call it <span className="font-normal text-muted-foreground">(optional)</span></Label>
                    <Input maxLength={120} placeholder="e.g. Parking map" value={b.label} onChange={(e) => patch(b.id, { label: e.target.value })} /></div>
                  <div className="space-y-1.5"><Label className="text-[12.5px]">A line about it <span className="font-normal text-muted-foreground">(optional)</span></Label>
                    <Input maxLength={300} placeholder="Why they should open it" value={b.text} onChange={(e) => patch(b.id, { text: e.target.value })} /></div>
                </div>
              )}

              {(b.type === "Images" || b.type === "Files") && (
                <ItemsEditor block={b} onAdd={(files) => addFiles(b, files)} onRemove={(key) => removeItem(b, key)} />
              )}
            </div>
          </div>
        );
      })}

      <div className="border border-border bg-muted/30 p-3">
        <p className="mb-2 text-[12.5px] font-semibold text-muted-foreground">{blocks.length === 0 ? "Add a part" : "Add another part"}</p>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-5">
          {BLOCKS.map((x) => (
            <button
              key={x.type}
              type="button"
              onClick={() => onChange([...blocks, emptyBlock(x.type)])}
              className="flex flex-col items-start gap-1 border border-border bg-card px-3 py-2.5 text-left transition-colors hover:border-primary/50 hover:bg-primary/5 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary"
            >
              <span className="flex items-center gap-1.5 text-[13px] font-semibold"><Plus size={12} className="text-primary" />{x.label}</span>
              <span className="text-[11.5px] leading-snug text-muted-foreground">{x.hint}</span>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

/** Photos or files in one part: pick them or drop them, see what is there, remove what isn't wanted. */
function ItemsEditor({ block, onAdd, onRemove }: { block: EventUpdateDraftBlock; onAdd: (files: File[]) => void; onRemove: (key: string) => void }) {
  const input = useRef<HTMLInputElement>(null);
  const [over, setOver] = useState(false);
  const isImages = block.type === "Images";
  return (
    <div className="space-y-3">
      <div
        onDragOver={(e) => { e.preventDefault(); setOver(true); }}
        onDragLeave={() => setOver(false)}
        onDrop={(e) => { e.preventDefault(); setOver(false); onAdd(Array.from(e.dataTransfer.files)); }}
        className={cn("flex flex-col items-center gap-1.5 border border-dashed px-4 py-5 text-center transition-colors", over ? "border-primary bg-primary/5" : "border-border bg-muted/20")}
      >
        <Upload size={18} className="text-muted-foreground" />
        <p className="text-[13px] text-muted-foreground">
          Drop {isImages ? "photos" : "files"} here, or{" "}
          <button type="button" className="font-semibold text-primary underline-offset-2 hover:underline" onClick={() => input.current?.click()}>choose from your device</button>
        </p>
        <p className="text-[12px] text-muted-foreground">{isImages ? "JPG, PNG, WebP or GIF, up to 15 MB each" : "PDF, Word, Excel, PowerPoint, text, CSV, RTF or ZIP, up to 30 MB each"}</p>
        <input ref={input} type="file" multiple hidden accept={isImages ? ".jpg,.jpeg,.png,.webp,.gif" : ".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv,.rtf,.zip"}
          onChange={(e) => { onAdd(Array.from(e.target.files ?? [])); e.target.value = ""; }} />
      </div>

      {block.items.length > 0 && (isImages ? (
        <ul className="grid grid-cols-3 gap-2 sm:grid-cols-4">
          {block.items.map((i) => (
            <li key={i.key} className="group relative aspect-square overflow-hidden border border-border bg-muted">
              <img src={i.previewUrl ?? i.url} alt={i.name} className="h-full w-full object-cover" />
              <button type="button" onClick={() => onRemove(i.key)} aria-label={`Remove ${i.name}`}
                className="absolute right-1 top-1 flex size-7 items-center justify-center bg-black/70 text-white opacity-100 transition-opacity hover:bg-black focus-visible:outline focus-visible:outline-2 focus-visible:outline-white sm:opacity-0 sm:group-hover:opacity-100 sm:focus-visible:opacity-100"><X size={13} /></button>
              {i.file && <span className="absolute bottom-0 left-0 bg-primary px-1.5 py-0.5 text-[10.5px] font-semibold uppercase text-primary-foreground">New</span>}
            </li>
          ))}
        </ul>
      ) : (
        <ul className="divide-y divide-border border border-border">
          {block.items.map((i) => (
            <li key={i.key} className="flex items-center gap-3 px-3 py-2.5">
              <span className="flex size-9 shrink-0 items-center justify-center bg-primary/10 text-[10.5px] font-bold uppercase text-primary">{fileKind(i.name).slice(0, 4)}</span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-[13.5px] font-medium">{i.name}</span>
                <span className="block text-[12px] text-muted-foreground">{formatFileSize(i.sizeBytes)}{i.file ? " · new" : ""}</span>
              </span>
              <Button type="button" size="sm" variant="ghost" className="px-2" onClick={() => onRemove(i.key)} aria-label={`Remove ${i.name}`}><X size={14} /></Button>
            </li>
          ))}
        </ul>
      ))}
      {block.items.length > 0 && <p className="text-[12px] text-muted-foreground">{block.items.length} {isImages ? (block.items.length === 1 ? "photo" : "photos") : (block.items.length === 1 ? "file" : "files")}</p>}
    </div>
  );
}
