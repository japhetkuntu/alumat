"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import "@alumni/site/styles.css";
import { SiteView, SAMPLE_DATA, BLOCKS, TEMPLATES, applyLook, siteDataFromRaw, type EditApi, type SectionType } from "@alumni/site";
import { Button, ArrowLeft, ExternalLink, Redo, Search, Skeleton, Undo, compressImage, cn } from "@alumni/ui";
import { handleApiError } from "@/lib/api-client";
import { getInstitutionProfile, getWebsiteContent, uploadImage } from "@/lib/institution-api";
import { sora } from "./site-font";
import { AddSectionDialog, MemberPickerDialog, CommandPalette, PhotoDialog, PublishDialog, VersionsDialog, type Command } from "./dialogs";
import { addItem, duplicateSection, insertSection, moveSection, removeItem, removePhoto, removeSection, sectionName, setItemField, setPhoto, setProp } from "./doc-ops";
import { DesignPanel, LeftPanel, SectionPanel, SitePanel, type PanelCtx } from "./panels";
import { Segmented } from "./fields";
import { useWebsiteEditor } from "./use-website-editor";

type Tab = "section" | "design" | "site";
const WIDTHS = { desktop: "100%", tablet: "792px", phone: "414px" } as const;

const SAVE_TEXT = { saved: "All changes saved", saving: "Saving…", unsaved: "Unsaved changes", error: "Could not save. Retrying when you change something", conflict: "Changed in another window" } as const;

export function WebsiteEditor() {
  const ed = useWebsiteEditor();
  const profile = useQuery({ queryKey: ["institution-profile"], queryFn: getInstitutionProfile, staleTime: 5 * 60_000 });
  const liveUrl = (profile.data as { memberPortalUrl?: string | null } | undefined)?.memberPortalUrl ?? null;

  // The institution's own items, as visitors would see them. Refreshed when the admin returns from adding some in another tab.
  const content = useQuery({ queryKey: ["website-content"], queryFn: getWebsiteContent, staleTime: 20_000, refetchOnWindowFocus: true });
  const realData = useMemo(() => (content.data ? siteDataFromRaw(content.data) : {}), [content.data]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [tab, setTab] = useState<Tab>("design");
  const [device, setDevice] = useState<keyof typeof WIDTHS>("desktop");
  const [preview, setPreview] = useState(false);
  const [sheet, setSheet] = useState<"left" | "right" | null>(null);
  const [picking, setPicking] = useState<string | null>(null);
  const [adding, setAdding] = useState<{ at?: number } | null>(null);
  const [photo, setPhoto_] = useState<{ id: string; slot: string } | null>(null);
  const [publishing, setPublishing] = useState(false);
  const [welcomeGone, setWelcomeGone] = useState(false);
  // Off: live sections show what they would show to visitors (nothing, until the institution has its own items). On: made-up examples, only to preview a layout.
  const [examples, setExamples] = useState(false);
  const [versions, setVersions] = useState(false), [palette, setPalette] = useState(false);

  const doc = ed.doc, brand = ed.brand;
  const select = useCallback((id: string | null) => { setSelectedId(id); if (id) setTab("section"); }, []);
  const scrollTo = useCallback((id: string) => { setSheet(null); setTimeout(() => document.getElementById("sec-" + id)?.scrollIntoView({ behavior: "smooth", block: "center" }), 60); }, []);

  // Choose a file, shrink it, upload it, give back its address.
  const upload = useCallback(() => new Promise<string | null>((resolve) => {
    const input = document.createElement("input");
    input.type = "file"; input.accept = "image/*";
    input.onchange = async () => {
      const f = input.files?.[0]; if (!f) return resolve(null);
      const t = toast.loading("Uploading photo…");
      try { const small = await compressImage(f, { maxDimension: 1800 }); const r = await uploadImage(small); toast.success("Photo added", { id: t }); resolve(r.url); }
      catch (e) { toast.error(handleApiError(e), { id: t }); resolve(null); }
    };
    input.addEventListener("cancel", () => resolve(null));
    input.click();
  }), []);

  const api: EditApi | undefined = useMemo(() => {
    if (!doc || preview) return undefined;
    return {
      selectedId, select,
      setProp: (id, k, v) => ed.change(setProp(doc, id, k, v), `p:${id}:${k}`),
      setItemField: (id, itemId, k, v) => ed.change(setItemField(doc, id, itemId, k, v), `i:${id}:${itemId}:${k}`),
      pickPhoto: (id, slot) => setPhoto_({ id, slot }),
      removePhoto: (id, slot) => ed.change(removePhoto(doc, id, slot)),
      addItem: (id) => { const r = addItem(doc, id); ed.change(r.doc); if (r.item && doc.sections.find((s) => s.id === id)?.type === "gallery") setPhoto_({ id, slot: "i" + r.item.id }); },
      removeItem: (id, itemId) => ed.change(removeItem(doc, id, itemId)),
      insertAt: (at) => setAdding({ at }),
      move: (id, dir) => ed.change(moveSection(doc, id, dir)),
      duplicate: (id) => { const r = duplicateSection(doc, id); ed.change(r.doc); if (r.section) select(r.section.id); },
      remove: (id) => { ed.change(removeSection(doc, id)); setSelectedId(null); toast("Section removed.", { action: { label: "Undo", onClick: ed.undo } }); },
      describeLink: (s, key, itemId) => { const l = itemId ? s.items?.find((i) => i.id === itemId)?.links[key] : s.links[key]; return l ? `Goes to: ${l.t === "anchor" ? "a section on this page" : l.t === "join" ? "the join page" : l.t === "signin" ? "member sign in" : l.t === "portal" ? `the portal page “${l.v}”` : l.v ?? l.t}` : "No link yet"; },
    };
  }, [doc, preview, selectedId, select, ed]);

  // Undo, redo and preview from the keyboard, unless typing in a field.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const t = e.target as HTMLElement; const typing = !!t.closest?.("input,textarea,select,[contenteditable=true]");
      const mod = e.metaKey || e.ctrlKey, k = e.key.toLowerCase();
      if (mod && k === "k") { e.preventDefault(); setPalette((o) => !o); }
      else if (mod && k === "z" && !typing) { e.preventDefault(); if (e.shiftKey) ed.redo(); else ed.undo(); }
      else if (mod && k === "y" && !typing) { e.preventDefault(); ed.redo(); }
      else if ((e.key === "Delete" || e.key === "Backspace") && !typing && selectedId && !preview) { e.preventDefault(); api?.remove(selectedId); }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [ed, selectedId, preview, api]);

  if (ed.error) return <Centered><p className="text-[14px]">The website could not be opened.</p><Button className="mt-3" onClick={() => ed.refetch()}>Try again</Button></Centered>;
  if (ed.isLoading || !doc || !brand) return <EditorSkeleton />;

  const commands: Command[] = [
    { group: "Do", label: preview ? "Back to editing" : "Preview the site as a visitor", run: () => { setPreview((p) => !p); setSelectedId(null); } },
    { group: "Do", label: examples ? "Hide example content" : "Show example content", run: () => setExamples((e) => !e) },
    { group: "Do", label: "Publish…", run: () => setPublishing(true) },
    { group: "Do", label: "Earlier versions or take the website down…", run: () => setVersions(true) },
    { group: "Do", label: "Undo", hint: "Ctrl Z", run: ed.undo }, { group: "Do", label: "Redo", hint: "Ctrl Shift Z", run: ed.redo },
    ...(["desktop", "tablet", "phone"] as const).map((d) => ({ group: "View", label: `Show on ${d}`, run: () => setDevice(d) })),
    ...doc.sections.map((s) => ({ group: "Go to section", label: sectionName(s), run: () => { setPreview(false); select(s.id); scrollTo(s.id); } })),
    ...(Object.entries(BLOCKS) as [SectionType, (typeof BLOCKS)[SectionType]][]).filter(([, b]) => !b.platformFeature || !ed.disabledFeatures.includes(b.platformFeature)).map(([t, b]) => ({ group: "Add a section", label: b.name, run: () => { const r = insertSection(doc, t); ed.change(r.doc); select(r.section.id); scrollTo(r.section.id); } })),
    ...TEMPLATES.map((t) => ({ group: "Look", label: `Use the ${t.name} look`, hint: doc.template === t.key ? "current" : undefined, run: () => ed.change(applyLook(doc, t.key)) })),
  ];
  const section = doc.sections.find((s) => s.id === selectedId) ?? null;
  const ctx: PanelCtx = { doc, brand, disabled: ed.disabledFeatures, change: ed.change, selectedId, select, openAdd: (at) => setAdding({ at }), upload, scrollTo, pickMembers: (id) => setPicking(id) };
  const panelTab: Tab = tab === "section" && !section ? "design" : tab;
  const status = ed.status;

  return (
    <div className={cn("fixed inset-0 z-[60] flex flex-col bg-muted text-foreground", sora.variable)}>
      {/* top bar */}
      <header className="flex items-center gap-1.5 overflow-x-auto border-b border-border bg-background px-3 py-2">
        <Link href="/dashboard" className="flex h-9 items-center gap-1.5 px-2 text-[13px] font-medium hover:bg-muted" aria-label="Back to the dashboard"><ArrowLeft size={14} /> Back</Link>
        <h1 className="m-0 mr-auto text-[15px] font-bold">Website</h1>
        <div className="hidden lg:block"><Segmented label="Preview size" value={device} onChange={setDevice} options={[{ value: "desktop", label: "Desktop" }, { value: "tablet", label: "Tablet" }, { value: "phone", label: "Phone" }]} /></div>
        <Button variant="ghost" size="sm" onClick={() => setPalette(true)} aria-label="Search and commands"><span className="hidden xl:inline">Search</span> <kbd className="hidden border border-border px-1 text-[11px] xl:inline">Ctrl K</kbd><span className="xl:hidden" aria-hidden="true"><Search size={14} /></span></Button>
        {status && status.publishedRevision > 0 && <Button variant="ghost" size="sm" onClick={() => setVersions(true)}>Versions</Button>}
        <Button variant="ghost" size="sm" onClick={ed.undo} disabled={!ed.canUndo} aria-label="Undo"><Undo size={14} /> <span className="hidden xl:inline">Undo</span></Button>
        <Button variant="ghost" size="sm" onClick={ed.redo} disabled={!ed.canRedo} aria-label="Redo"><Redo size={14} /> <span className="hidden xl:inline">Redo</span></Button>
        <span className={cn("hidden text-[12px] xl:inline", ed.saveState === "conflict" || ed.saveState === "error" ? "text-destructive" : "text-muted-foreground")} aria-live="polite">{SAVE_TEXT[ed.saveState]}</span>
        <Button variant={examples ? "default" : "ghost"} size="sm" onClick={() => setExamples((e) => !e)} aria-pressed={examples} title="Show made-up items in the sections that fill themselves, to preview a layout. Never published.">{examples ? "Hide example content" : "Show example content"}</Button>
        <Button variant={preview ? "default" : "secondary"} size="sm" onClick={() => { setPreview((p) => !p); setSelectedId(null); }} aria-pressed={preview}>{preview ? "Back to editing" : "Preview"}</Button>
        {status?.isLive && liveUrl && <a href={liveUrl} target="_blank" rel="noopener noreferrer" className="hidden h-9 items-center gap-1 px-2 text-[13px] font-medium text-primary hover:underline xl:flex"><ExternalLink size={13} /> View live</a>}
        <Button size="sm" onClick={() => setPublishing(true)}>{status?.isLive ? (status.hasUnpublishedChanges ? "Publish changes" : "Published") : "Publish"}</Button>
      </header>

      {!welcomeGone && status && !status.isLive && status.publishedRevision === 0 && !preview && (
        <div className="flex flex-wrap items-center gap-3 border-b border-primary/30 bg-primary/5 px-4 py-2.5 text-[13px]">
          <span className="min-w-0 flex-1"><b className="font-semibold">Your starter website is ready.</b> It is built from your institution’s name, colours and logo. Change anything you like, then publish. Until you do, visitors keep seeing your current landing page.</span>
          <Button size="sm" onClick={() => setPublishing(true)}>Publish the starter site</Button>
          <Button size="sm" variant="ghost" onClick={() => setWelcomeGone(true)}>Keep editing</Button>
        </div>
      )}

      {ed.saveState === "conflict" && (
        <div role="alert" className="flex flex-wrap items-center gap-3 border-b border-amber-300 bg-amber-50 px-4 py-2 text-[13px]">
          This page was changed in another window, so your latest edits were not saved.
          <Button size="sm" variant="secondary" onClick={() => void ed.reload()}>Load the latest version</Button>
        </div>
      )}

      <div className="flex min-h-0 flex-1">
        {/* left */}
        <aside className={cn("w-[280px] shrink-0 overflow-y-auto border-r border-border bg-background p-3.5", "max-lg:fixed max-lg:inset-x-0 max-lg:bottom-14 max-lg:z-40 max-lg:max-h-[70vh] max-lg:w-auto max-lg:border-t max-lg:shadow-2xl", sheet === "left" ? "max-lg:block" : "max-lg:hidden")} aria-label="Sections and features">
          <LeftPanel ctx={ctx} />
        </aside>

        {/* page */}
        <main className="min-w-0 flex-1 overflow-auto p-3 max-lg:pb-20 sm:p-4" data-site-scroller onClick={() => setSheet(null)}>
          <div className={cn("mx-auto overflow-clip bg-background shadow-[0_1px_2px_rgba(0,0,0,.12)] transition-[width] duration-300", device !== "desktop" && "border-[12px] border-[#17211e] shadow-2xl", device === "phone" ? "rounded-[38px]" : device === "tablet" ? "rounded-[26px]" : "")} style={{ width: WIDTHS[device], maxWidth: "100%" }}>
            <SiteView doc={doc} brand={brand} data={examples ? SAMPLE_DATA : realData} platformDisabled={ed.disabledFeatures} edit={api} sample={examples} loading={content.isLoading && !examples} />
          </div>
          {!preview && (
            <p className="mx-auto mt-3 max-w-md text-center text-[12px] text-muted-foreground">{examples ? "Example content is showing, only to preview layouts. It is never published." : "Sections that fill themselves show your own items. Where you have none yet, visitors do not see that section."}</p>
          )}
        </main>

        {/* right */}
        <aside className={cn("w-[320px] shrink-0 overflow-y-auto border-l border-border bg-background", "max-lg:fixed max-lg:inset-x-0 max-lg:bottom-14 max-lg:z-40 max-lg:max-h-[70vh] max-lg:w-auto max-lg:border-t max-lg:shadow-2xl", sheet === "right" ? "max-lg:block" : "max-lg:hidden")} aria-label="Settings">
          <div role="tablist" className="sticky top-0 z-10 flex gap-1 border-b border-border bg-background p-2">
            {([["section", "Section", !!section], ["design", "Design", true], ["site", "Header & footer", true]] as const).map(([k, label, enabled]) => (
              <button key={k} role="tab" aria-selected={panelTab === k} disabled={!enabled} onClick={() => setTab(k)} className={cn("min-h-9 flex-1 px-1 text-[12.5px] font-semibold transition-colors disabled:opacity-40", panelTab === k ? "bg-foreground text-background" : "hover:bg-muted")}>{label}</button>
            ))}
          </div>
          <div className="p-3.5">
            {panelTab === "section" && section ? <SectionPanel ctx={ctx} section={section} /> : panelTab === "site" ? <SitePanel ctx={ctx} /> : <DesignPanel ctx={ctx} />}
          </div>
        </aside>
      </div>

      {/* small screens: panels open from a bar */}
      <nav className="fixed inset-x-0 bottom-0 z-50 grid h-14 grid-cols-2 border-t border-border bg-background lg:hidden" aria-label="Panels">
        <button className={cn("text-[13px] font-semibold", sheet === "left" && "bg-foreground text-background")} onClick={() => setSheet(sheet === "left" ? null : "left")}>Sections</button>
        <button className={cn("text-[13px] font-semibold", sheet === "right" && "bg-foreground text-background")} onClick={() => setSheet(sheet === "right" ? null : "right")}>Edit and design{selectedId ? " •" : ""}</button>
      </nav>

      <MemberPickerDialog open={!!picking} onClose={() => setPicking(null)} onPick={(people) => {
        if (!picking) return;
        let next = doc;
        for (const m of people) {
          const r = addItem(next, picking); next = r.doc;
          if (!r.item) continue;
          next = setItemField(next, picking, r.item.id, "name", `${m.firstName} ${m.lastName}`.trim());
          next = setItemField(next, picking, r.item.id, "role", "Add their role");
          if (m.profilePictureUrl && /^https?:\/\//.test(m.profilePictureUrl)) next = setPhoto(next, picking, "i" + r.item.id, m.profilePictureUrl);
        }
        ed.change(next);
        toast(`${people.length} ${people.length === 1 ? "person" : "people"} added. Set their roles and groups in the panel.`);
      }} />
      <AddSectionDialog open={!!adding} disabled={ed.disabledFeatures} doc={doc} onClose={() => setAdding(null)} onAdd={(t: SectionType) => {
        const r = insertSection(doc, t, adding?.at); ed.change(r.doc); setAdding(null); select(r.section.id); scrollTo(r.section.id);
        toast(`“${sectionName(r.section)}” added.`, { action: { label: "Undo", onClick: ed.undo } });
      }} />

      <PhotoDialog open={!!photo} current={photo ? doc.sections.find((s) => s.id === photo.id)?.imgs[photo.slot] : undefined} onClose={() => setPhoto_(null)}
        onPick={(url) => { if (photo) ed.change(setPhoto(doc, photo.id, photo.slot, url)); setPhoto_(null); }}
        onRemove={() => { if (photo) ed.change(removePhoto(doc, photo.id, photo.slot)); setPhoto_(null); }}
        onUpload={async () => { const p = photo; const u = await upload(); if (u && p) ed.change(setPhoto(doc, p.id, p.slot, u)); setPhoto_(null); }} />

      <CommandPalette open={palette} commands={commands} onClose={() => setPalette(false)} />
      <VersionsDialog open={versions} onClose={() => setVersions(false)} onTakeDown={async () => { await ed.unpublish(); toast.success("The website is down. Visitors see your standard landing page."); }} onRestored={() => { toast.success("Put back in the editor. Publish when you are happy."); void ed.reload(); }} />
      <PublishDialog open={publishing} doc={doc} disabled={ed.disabledFeatures} liveUrl={liveUrl} isLive={!!status?.isLive} onClose={() => setPublishing(false)}
        onPublish={async () => { await ed.publish(); }} onFix={(id) => { select(id); scrollTo(id); }} />
    </div>
  );
}

function Centered({ children }: { children: React.ReactNode }) {
  return <div className="fixed inset-0 z-[60] grid place-items-center bg-background text-center">{<div>{children}</div>}</div>;
}

function EditorSkeleton() {
  return (
    <div className="fixed inset-0 z-[60] flex flex-col bg-muted" aria-busy="true" aria-label="Opening the website">
      <div className="flex items-center gap-3 border-b border-border bg-background px-3 py-2.5"><Skeleton className="h-8 w-20" /><Skeleton className="h-6 w-24" /><span className="flex-1" /><Skeleton className="h-8 w-24" /><Skeleton className="h-8 w-24" /></div>
      <div className="flex min-h-0 flex-1">
        <div className="hidden w-[280px] space-y-3 border-r border-border bg-background p-4 lg:block">{Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-12 w-full" />)}</div>
        <div className="flex-1 p-4"><Skeleton className="h-[70vh] w-full" /></div>
        <div className="hidden w-[320px] space-y-3 border-l border-border bg-background p-4 lg:block">{Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className="h-16 w-full" />)}</div>
      </div>
    </div>
  );
}
