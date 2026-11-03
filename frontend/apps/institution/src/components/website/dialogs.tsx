"use client";

import { Fragment, useState } from "react";
import { getMembers } from "@/lib/institution-api";
import type { Member } from "@/types";
import { BLOCKS, STOCK, checkBeforePublish, sized, type SectionType, type SiteDocument } from "@alumni/site";
import { Button, Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, Check as CheckIcon, AlertTriangle, Info, Loader2, cn } from "@alumni/ui";
import { inputCls } from "./fields";

// ── Pick a photo ──────────────────────────────────────────────────────────────────────────────────

const STARTERS = Array.from(new Set([STOCK.hero, STOCK.graduate, STOCK.toss, STOCK.campus, STOCK.library, ...STOCK.steps, ...STOCK.cards, ...STOCK.gallery]));

export function PhotoDialog({ open, current, onClose, onPick, onUpload, onRemove }: { open: boolean; current?: string; onClose: () => void; onPick: (url: string) => void; onUpload: () => void; onRemove: () => void }) {
  const [address, setAddress] = useState("");
  const ok = /^https:\/\/\S+$/i.test(address.trim());
  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-xl">
        <DialogHeader><DialogTitle>{current ? "Replace this photo" : "Add a photo"}</DialogTitle><DialogDescription>Upload your own, or start from one of ours. You can change it any time.</DialogDescription></DialogHeader>
        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <Button onClick={onUpload}>Upload a photo</Button>
            {current && <Button variant="ghost" onClick={onRemove}>Remove this photo</Button>}
          </div>
          <div>
            <div className="mb-1.5 text-[12px] font-semibold text-muted-foreground">Starter photos</div>
            <div className="grid max-h-56 grid-cols-4 gap-1.5 overflow-auto sm:grid-cols-5">
              {STARTERS.map((u) => (
                <button key={u} type="button" onClick={() => onPick(u)} aria-label="Use this photo" className={cn("aspect-[4/3] overflow-hidden border border-border hover:border-primary", current === u && "ring-2 ring-primary")}>
                  <img src={sized(u, 240)} alt="" loading="lazy" className="h-full w-full object-cover" />
                </button>
              ))}
            </div>
          </div>
          <div>
            <div className="mb-1.5 text-[12px] font-semibold text-muted-foreground">Or use a photo from a web address</div>
            <div className="flex gap-2">
              <input className={inputCls} placeholder="https://…" value={address} onChange={(e) => setAddress(e.target.value)} aria-label="Photo address" />
              <Button variant="secondary" size="sm" disabled={!ok} onClick={() => { onPick(address.trim()); setAddress(""); }}>Use</Button>
            </div>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

// ── Add a section ─────────────────────────────────────────────────────────────────────────────────

export function AddSectionDialog({ open, disabled, doc, onClose, onAdd }: { open: boolean; disabled: string[]; doc: SiteDocument; onClose: () => void; onAdd: (t: SectionType) => void }) {
  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-3xl">
        <DialogHeader><DialogTitle>Add a section</DialogTitle><DialogDescription>Pick one. You can change its layout, photos and words afterwards.</DialogDescription></DialogHeader>
        <div className="grid max-h-[60vh] grid-cols-1 gap-2 overflow-auto sm:grid-cols-2 lg:grid-cols-3">
          {(Object.entries(BLOCKS) as [SectionType, (typeof BLOCKS)[SectionType]][]).map(([type, b]) => {
            const off = !!b.platformFeature && disabled.includes(b.platformFeature);
            const hidden = !!b.feature && doc.features[b.feature] === false;
            return (
              <button key={type} type="button" disabled={off} onClick={() => onAdd(type)} className="border border-border p-3 text-left transition-colors hover:border-foreground hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50">
                <div className="text-[14px] font-semibold">{b.name}{b.live && <span className="ml-1.5 border border-primary/40 bg-primary/5 px-1 text-[11px] font-medium text-primary">live</span>}</div>
                <div className="mt-1 text-[12.5px] leading-snug text-muted-foreground">{off ? `${b.feature ?? b.name} is not available for your institution.` : hidden ? `${b.description} ${b.feature} is switched off for the website right now.` : b.description}</div>
              </button>
            );
          })}
        </div>
      </DialogContent>
    </Dialog>
  );
}

// ── Publish ───────────────────────────────────────────────────────────────────────────────────────

export function PublishDialog({ open, doc, disabled, liveUrl, isLive, onClose, onPublish, onFix }: { open: boolean; doc: SiteDocument; disabled: string[]; liveUrl?: string | null; isLive: boolean; onClose: () => void; onPublish: () => Promise<void>; onFix: (sectionId: string) => void }) {
  const [phase, setPhase] = useState<"check" | "busy" | "done">("check");
  const [error, setError] = useState<string | null>(null);
  const checks = checkBeforePublish(doc, disabled);
  const bad = checks.filter((c) => !c.ok).length;
  const blocked = checks.some((c) => c.blocking && !c.ok);
  const close = () => { if (phase === "busy") return; setPhase("check"); setError(null); onClose(); };
  const go = async () => {
    setPhase("busy"); setError(null);
    try { await onPublish(); setPhase("done"); } catch (e) { setError(e instanceof Error ? e.message : "It could not be published. Please try again."); setPhase("check"); }
  };
  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) close(); }}>
      <DialogContent className="max-w-lg">
        {phase === "done" ? (
          <div className="space-y-4 py-2 text-center">
            <div className="mx-auto grid h-14 w-14 place-items-center rounded-full bg-primary text-primary-foreground"><CheckIcon size={26} /></div>
            <DialogHeader className="items-center"><DialogTitle>Your website is live</DialogTitle><DialogDescription>Visitors see this version now. Every change is saved, and you can put an earlier version back.</DialogDescription></DialogHeader>
            {liveUrl && <a className="block truncate border border-border bg-muted/40 px-3 py-2 text-[13px] hover:bg-muted" href={liveUrl} target="_blank" rel="noopener noreferrer">{liveUrl.replace(/^https?:\/\//, "")}</a>}
            <div className="flex justify-center gap-2"><Button variant="secondary" onClick={close}>Done</Button>{liveUrl && <a href={liveUrl} target="_blank" rel="noopener noreferrer"><Button>See it as a visitor</Button></a>}</div>
          </div>
        ) : (
          <>
            <DialogHeader>
              <DialogTitle>{phase === "busy" ? "Publishing…" : bad ? "Almost ready" : isLive ? "Publish your changes" : "Ready to go live"}</DialogTitle>
              <DialogDescription>{blocked ? "Some words from the builder are still on the page. Replace or remove them, then publish." : bad ? `${bad} thing${bad === 1 ? "" : "s"} to look at. You can publish anyway, or fix ${bad === 1 ? "it" : "them"} first.` : "Everything looks good. Your changes go live in about a second."}</DialogDescription>
            </DialogHeader>
            <ul className="m-0 grid list-none gap-1.5 p-0">
              {checks.map((c, i) => (
                <li key={i} className={cn("flex items-start gap-2.5 border p-2.5 text-[13px]", c.ok ? (c.info ? "border-border bg-muted/40" : "border-primary/30 bg-primary/5") : "border-amber-300 bg-amber-50")}>
                  <span className="mt-0.5 shrink-0">{c.ok ? (c.info ? <Info size={15} /> : <CheckIcon size={15} className="text-primary" />) : <AlertTriangle size={15} className="text-amber-700" />}</span>
                  <span className="min-w-0 flex-1"><b className="font-semibold">{c.text}</b>{c.detail && <span className="mt-0.5 block text-[12px] text-muted-foreground">{c.detail}</span>}</span>
                  {c.sectionId && <button type="button" className="shrink-0 text-[12px] font-semibold text-primary underline" onClick={() => { close(); onFix(c.sectionId!); }}>Show me</button>}
                </li>
              ))}
            </ul>
            {error && <p className="m-0 text-[13px] text-destructive">{error}</p>}
            <div className="flex justify-end gap-2">
              <Button variant="secondary" onClick={close} disabled={phase === "busy"}>{bad ? "Keep editing" : "Not yet"}</Button>
              <Button onClick={go} disabled={blocked} isLoading={phase === "busy"} loadingText="Publishing…">{bad ? "Publish anyway" : "Publish now"}</Button>
            </div>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

export { Loader2 };

// ── Earlier versions ──────────────────────────────────────────────────────────────────────────────

import { useQuery } from "@tanstack/react-query";
import { getWebsiteVersions, restoreWebsiteVersion } from "@/lib/institution-api";

export function VersionsDialog({ open, onClose, onRestored, onTakeDown }: { open: boolean; onClose: () => void; onRestored: () => void; onTakeDown: () => Promise<void> }) {
  const q = useQuery({ queryKey: ["website-versions"], queryFn: getWebsiteVersions, enabled: open, staleTime: 0 });
  const [busy, setBusy] = useState<string | null>(null);
  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-md">
        <DialogHeader><DialogTitle>Earlier versions</DialogTitle><DialogDescription>Put an earlier published version back into the editor. Visitors see nothing change until you publish it again.</DialogDescription></DialogHeader>
        {q.isLoading ? <p className="text-[13px] text-muted-foreground">Loading…</p> : !(q.data ?? []).length ? <p className="text-[13px] text-muted-foreground">Nothing yet. A version is kept each time you publish.</p> : (
          <ul className="m-0 grid max-h-72 list-none gap-1.5 overflow-auto p-0">
            {q.data!.map((v) => (
              <li key={v.id} className="flex items-center justify-between gap-3 border border-border p-2.5 text-[13px]">
                <span><b className="font-semibold">{new Date(v.publishedAt).toLocaleString("en-GB", { day: "2-digit", month: "short", hour: "2-digit", minute: "2-digit" })}</b><span className="block text-[12px] text-muted-foreground">by {v.publishedBy || "an admin"}</span></span>
                <Button size="sm" variant="secondary" isLoading={busy === v.id} onClick={async () => { setBusy(v.id); try { await restoreWebsiteVersion(v.id); onRestored(); onClose(); } finally { setBusy(null); } }}>Put this back</Button>
              </li>
            ))}
          </ul>
        )}
        <div className="border-t border-border pt-3">
          <Button variant="destructive" size="sm" isLoading={busy === "down"} onClick={async () => { if (!window.confirm("Take the website down? Visitors will see your standard landing page again. Your draft and every version are kept.")) return; setBusy("down"); try { await onTakeDown(); onClose(); } finally { setBusy(null); } }}>Take the website down</Button>
          <p className="m-0 mt-1.5 text-[12px] text-muted-foreground">Goes back to your standard landing page at once. You can publish again any time.</p>
        </div>
      </DialogContent>
    </Dialog>
  );
}

// ── Search and commands (Ctrl or Cmd + K) ─────────────────────────────────────────────────────────

export interface Command { group: string; label: string; hint?: string; run: () => void }

export function CommandPalette({ open, commands, onClose }: { open: boolean; commands: Command[]; onClose: () => void }) {
  const [q, setQ] = useState(""), [sel, setSel] = useState(0);
  const words = q.trim().toLowerCase().split(/\s+/).filter(Boolean);
  const list = commands.filter((c) => words.every((w) => `${c.label} ${c.group}`.toLowerCase().includes(w))).slice(0, 40);
  const run = (c?: Command) => { if (!c) return; onClose(); setQ(""); setTimeout(c.run, 30); };
  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) { onClose(); setQ(""); } }}>
      <DialogContent className="max-w-xl gap-0 p-0">
        <DialogHeader className="sr-only"><DialogTitle>Search and commands</DialogTitle><DialogDescription>Type to find a section or an action.</DialogDescription></DialogHeader>
        <input autoFocus className="h-12 w-full border-b border-border bg-background px-4 text-[15px] outline-none" placeholder="Search sections, looks and actions" aria-label="Search" value={q}
          onChange={(e) => { setQ(e.target.value); setSel(0); }}
          onKeyDown={(e) => { if (e.key === "ArrowDown") { e.preventDefault(); setSel((s) => Math.min(list.length - 1, s + 1)); } else if (e.key === "ArrowUp") { e.preventDefault(); setSel((s) => Math.max(0, s - 1)); } else if (e.key === "Enter") { e.preventDefault(); run(list[sel]); } }} />
        <ul role="listbox" className="m-0 max-h-[50vh] list-none overflow-auto p-1.5">
          {list.length === 0 && <li className="p-6 text-center text-[13px] text-muted-foreground">Nothing matches “{q}”. Try a section name, like “events”.</li>}
          {list.map((c, i) => (
            <Fragment key={i}>
              {c.group !== list[i - 1]?.group && <li role="presentation" className="px-2.5 pb-1 pt-2.5 text-[11px] font-bold uppercase tracking-[0.08em] text-muted-foreground">{c.group}</li>}
              <li role="option" aria-selected={i === sel} onMouseMove={() => setSel(i)} onClick={() => run(c)} className={cn("flex cursor-pointer items-center justify-between gap-3 px-2.5 py-2 text-[13.5px]", i === sel && "bg-primary/10")}><span>{c.label}</span>{c.hint && <small className="text-[12px] text-muted-foreground">{c.hint}</small>}</li>
            </Fragment>
          ))}
        </ul>
      </DialogContent>
    </Dialog>
  );
}

// ── Choose leaders from your members ──────────────────────────────────────────────────────────────

/** The admin picks the people. Only a name, a role and the profile photo are copied onto the website, nothing else from the profile. */
export function MemberPickerDialog({ open, onClose, onPick }: { open: boolean; onClose: () => void; onPick: (people: Member[]) => void }) {
  const [text, setText] = useState(""), [chosen, setChosen] = useState<Record<string, Member>>({});
  const q = useQuery({ queryKey: ["website-member-search", text], queryFn: () => getMembers({ search: text.trim() || undefined, status: "Active", pageSize: 12 }), enabled: open, staleTime: 15_000 });
  const rows = q.data?.results ?? [];
  const count = Object.keys(chosen).length;
  const close = () => { setChosen({}); setText(""); onClose(); };
  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) close(); }}>
      <DialogContent className="max-w-lg">
        <DialogHeader><DialogTitle>Add people from your members</DialogTitle><DialogDescription>Only their name and profile photo are put on the website. You choose who appears, and you can set their role afterwards.</DialogDescription></DialogHeader>
        <input className={inputCls} placeholder="Search by name" aria-label="Search members" value={text} onChange={(e) => setText(e.target.value)} autoFocus />
        <ul className="m-0 grid max-h-[320px] list-none gap-1 overflow-auto p-0" aria-busy={q.isLoading}>
          {q.isLoading && Array.from({ length: 4 }, (_, i) => <li key={i} className="h-12 animate-pulse bg-muted" />)}
          {!q.isLoading && rows.length === 0 && <li className="p-3 text-[13px] text-muted-foreground">No active members match.</li>}
          {rows.map((m) => {
            const on = !!chosen[m.id];
            return (
              <li key={m.id}>
                <label className={cn("flex cursor-pointer items-center gap-3 border border-border p-2", on && "border-foreground bg-muted")}>
                  <input type="checkbox" checked={on} onChange={() => setChosen((c) => { const n = { ...c }; if (n[m.id]) delete n[m.id]; else n[m.id] = m; return n; })} />
                  {m.profilePictureUrl ? <img src={m.profilePictureUrl} alt="" className="h-9 w-9 object-cover" /> : <span className="grid h-9 w-9 place-items-center bg-muted text-[12px] font-semibold">{(m.firstName[0] ?? "") + (m.lastName[0] ?? "")}</span>}
                  <span className="min-w-0 flex-1"><span className="block truncate text-[14px] font-medium">{m.firstName} {m.lastName}</span><span className="block truncate text-[12px] text-muted-foreground">{[m.jobTitle, m.graduationYear ? `Class of ${m.graduationYear}` : null].filter(Boolean).join(" · ")}</span></span>
                </label>
              </li>
            );
          })}
        </ul>
        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={close}>Cancel</Button>
          <Button disabled={!count} onClick={() => { onPick(Object.values(chosen)); close(); }}>Add {count || ""} {count === 1 ? "person" : "people"}</Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
