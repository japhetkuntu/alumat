"use client";

import { useState } from "react";
import { BLOCKS, TEMPLATES, WEBSITE_FEATURES, applyLook, buildTemplate, type SectionType, type SiteBrand, type SiteDocument, type SiteSection } from "@alumni/site";
import { Button, ChevronDown, ChevronUp, Copy, Eye, EyeOff, Plus, Trash2, X, cn } from "@alumni/ui";
import { setItemField as setItemFieldOp, addItem, duplicateSection, moveItem, moveSection, removeItem, removeSection, reorderSection, sectionName, setDesign, setFeature, setFooter, setLink, setSection, setSeo, setSocial } from "./doc-ops";
import { Check, Field, Group, PanelTitle, Segmented, inputCls } from "./fields";
import { LinkEditor } from "./link-editor";

export interface PanelCtx {
  doc: SiteDocument;
  brand: SiteBrand;
  disabled: string[];
  change: (next: SiteDocument, key?: string) => void;
  selectedId: string | null;
  select: (id: string | null) => void;
  openAdd: (at?: number) => void;
  /** Opens the file chooser, uploads, and returns the new address (or null if cancelled). */
  upload: () => Promise<string | null>;
  scrollTo: (id: string) => void;
  /** Opens the member chooser for a Leaders section. */
  pickMembers: (sectionId: string) => void;
}

const PLATFORM_KEY: Record<string, string> = { Services: "Services", Store: "Store", Fundraisers: "Contributions", Jobs: "Jobs", Businesses: "BusinessDirectory", Communities: "Communities" };

// ── Left: what is on the page ─────────────────────────────────────────────────────────────────────

export function LeftPanel({ ctx }: { ctx: PanelCtx }) {
  const { doc } = ctx;
  const [dragId, setDragId] = useState<string | null>(null), [overId, setOverId] = useState<string | null>(null);
  return (
    <div className="space-y-6">
      <section>
        <PanelTitle>Features on your website</PanelTitle>
        <ul className="m-0 grid list-none gap-2 p-0">
          {WEBSITE_FEATURES.map((f) => {
            const platformOff = ctx.disabled.includes(PLATFORM_KEY[f.key]);
            const count = doc.sections.filter((s) => BLOCKS[s.type].feature === f.key).length;
            const on = doc.features[f.key] !== false;
            return (
              <li key={f.key} className="border border-border bg-card p-2.5">
                <Check checked={on && !platformOff} disabled={platformOff} onChange={(v) => ctx.change(setFeature(doc, f.key, v))}
                  hint={platformOff ? "Not available for your institution. Ask the platform team to switch it on." : `${f.description}${on ? "" : " Hidden from visitors."}`}>
                  {f.label}
                </Check>
                {!platformOff && <p className="m-0 mt-1 pl-6 text-[12px] text-primary">{count} section{count === 1 ? "" : "s"} on your page</p>}
              </li>
            );
          })}
        </ul>
        <p className="mt-2 text-[12px] leading-snug text-muted-foreground">Only items you mark “Show on website” in each feature appear to visitors. Buying and signing in always happen in the member portal.</p>
      </section>

      <section>
        <PanelTitle>Sections on your page</PanelTitle>
        <p className="mb-2 text-[12px] leading-snug text-muted-foreground">One page that visitors scroll. Tick “In menu” on a section to give it a tab at the top.</p>
        <ul className="m-0 grid list-none gap-1.5 p-0" aria-label="Sections">
          {doc.sections.map((s) => {
            const b = BLOCKS[s.type];
            const platformOff = !!b.platformFeature && ctx.disabled.includes(b.platformFeature);
            const featOff = !!b.feature && doc.features[b.feature] === false;
            return (
              <li key={s.id} draggable
                onDragStart={(e) => { setDragId(s.id); e.dataTransfer.effectAllowed = "move"; }}
                onDragOver={(e) => { if (dragId) { e.preventDefault(); setOverId(s.id); } }}
                onDrop={(e) => { e.preventDefault(); if (dragId) ctx.change(reorderSection(doc, dragId, s.id)); setDragId(null); setOverId(null); }}
                onDragEnd={() => { setDragId(null); setOverId(null); }}
                className={cn("border bg-card p-1.5 transition-colors", ctx.selectedId === s.id ? "border-foreground shadow-[inset_0_0_0_1px_var(--foreground)]" : "border-border", overId === s.id && dragId !== s.id && "border-primary bg-primary/5", (s.hidden || featOff || platformOff) && "opacity-60")}>
                <div className="flex items-center gap-1.5">
                  <span className="cursor-grab select-none px-1 text-muted-foreground" aria-hidden="true">⋮⋮</span>
                  <button type="button" className="min-h-8 min-w-0 flex-1 truncate text-left text-[13px] font-medium" onClick={() => { ctx.select(s.id); ctx.scrollTo(s.id); }}>
                    {b.name}{(featOff || platformOff) && <span className="ml-1.5 border border-amber-300 bg-amber-50 px-1 text-[11px] text-amber-800">Off</span>}
                  </button>
                  <button type="button" className="grid h-8 w-8 place-items-center text-muted-foreground hover:bg-muted" title={s.hidden ? "Show this section" : "Hide this section"} aria-label={s.hidden ? "Show this section" : "Hide this section"} onClick={() => ctx.change(setSection(doc, s.id, "hidden", !s.hidden))}>
                    {s.hidden ? <EyeOff size={14} /> : <Eye size={14} />}
                  </button>
                  <button type="button" className="grid h-8 w-8 place-items-center text-muted-foreground hover:bg-muted hover:text-destructive" aria-label={`Remove ${b.name}`} title="Remove this section" onClick={() => ctx.change(removeSection(doc, s.id))}><X size={14} /></button>
                </div>
                <label className="mt-0.5 flex min-h-7 items-center gap-1.5 pl-7 text-[12px] text-muted-foreground">
                  <input type="checkbox" className="accent-[var(--primary)]" checked={s.menu.on} onChange={(e) => ctx.change(setSection(doc, s.id, "menu", { ...s.menu, on: e.target.checked, label: s.menu.label || b.name }))} />
                  In menu{s.menu.on && s.menu.label ? `: ${s.menu.label}` : ""}
                </label>
              </li>
            );
          })}
        </ul>
        <Button variant="outline" size="sm" className="mt-3 w-full" onClick={() => ctx.openAdd()}><Plus size={14} /> Add a section</Button>
      </section>
    </div>
  );
}

// ── Right, tab 1: the selected section ────────────────────────────────────────────────────────────

export function SectionPanel({ ctx, section: s }: { ctx: PanelCtx; section: SiteSection }) {
  const { doc } = ctx, b = BLOCKS[s.type];
  const isOn = !b.platformFeature || !ctx.disabled.includes(b.platformFeature);
  const buttonKeys = s.type === "hero" ? ["b1", "b2"].filter((k) => s.props[k]) : s.type === "cta" ? ["b1"] : s.type === "imagetext" && s.props.link ? ["link"] : [];
  const up = (next: SiteDocument) => ctx.change(next);
  return (
    <div className="space-y-4">
      <div>
        <h3 className="m-0 text-[15px] font-bold">{b.name}</h3>
        <p className="m-0 mt-0.5 text-[12px] text-muted-foreground">{b.description}</p>
      </div>
      {b.live && <p className="m-0 border-l-[3px] border-primary bg-primary/5 px-2.5 py-1.5 text-[12px]">Fills itself from your {b.live.toLowerCase()}. In the editor the items are samples; visitors only ever see your own.{b.feature ? ` Items appear once you mark them “Show on website”.` : ""}</p>}
      {!isOn && <p className="m-0 border-l-[3px] border-amber-500 bg-amber-50 px-2.5 py-1.5 text-[12px]">{b.feature ?? b.name} is not available for your institution, so visitors will not see this section.</p>}

      {Object.keys(b.variants).length > 1 && (
        <Field label="Layout"><Segmented label="Layout" value={s.variant} onChange={(v) => up(setSection(doc, s.id, "variant", v))} options={Object.entries(b.variants).map(([value, label]) => ({ value, label }))} /></Field>
      )}
      {b.counted && (
        <Field label="How many to show"><Segmented label="How many" value={String(s.count)} onChange={(v) => up(setSection(doc, s.id, "count", Number(v)))} options={["3", "4", "6"].map((n) => ({ value: n, label: n }))} /></Field>
      )}
      {(s.type === "hero" || s.type === "cta") && ["b1", ...(s.type === "hero" ? ["b2"] : [])].map((k, i) => (
        <Field key={k} label={i === 0 ? (s.type === "hero" ? "First button" : "Button") : "Second button"}>
          <Segmented label="Show button" value={s.props[k] ? "on" : "off"} onChange={(v) => up(setProp2(doc, s.id, k, v === "off" ? "" : i === 0 ? "Join now" : "See what is on"))} options={[{ value: "on", label: "Show" }, { value: "off", label: "Hide" }]} />
        </Field>
      ))}

      {b.items && (
        <Field label={`${b.items.many} (${s.items?.length ?? 0})`}>
          <ul className="m-0 grid list-none gap-1.5 p-0">
            {(s.items ?? []).map((it, i) => (
              <li key={it.id} className="flex items-center gap-1 border border-border bg-card px-2 py-1">
                <span className="min-w-0 flex-1 truncate text-[13px]">{i + 1}. {s.type === "gallery" ? (s.imgs["i" + it.id] ? "Photo added" : "Empty photo slot") : b.items!.label(it.fields)}</span>
                <button type="button" className="grid h-8 w-8 place-items-center hover:bg-muted" aria-label="Move up" onClick={() => up(moveItem(doc, s.id, it.id, -1))}><ChevronUp size={14} /></button>
                <button type="button" className="grid h-8 w-8 place-items-center hover:bg-muted" aria-label="Move down" onClick={() => up(moveItem(doc, s.id, it.id, 1))}><ChevronDown size={14} /></button>
                <button type="button" className="grid h-8 w-8 place-items-center hover:bg-muted hover:text-destructive" aria-label="Remove" onClick={() => up(removeItem(doc, s.id, it.id))}><X size={14} /></button>
              </li>
            ))}
            {!(s.items ?? []).length && <li className="text-[12px] text-muted-foreground">Nothing here yet.</li>}
          </ul>
          <Button variant="secondary" size="sm" onClick={() => up(addItem(doc, s.id).doc)}><Plus size={14} /> Add {s.type === "gallery" ? "a photo" : `a ${b.items.word}`}</Button>
          {s.type === "gallery" && s.variant === "mosaic" && !(s.items && s.items.length >= 5 && s.items.length % 2 === 1) && <p className="m-0 text-[12px] text-muted-foreground">The mosaic needs 5, 7 or 9 photos. It shows an even grid until then.</p>}
        </Field>
      )}

      {(s.type === "cards" || s.type === "membership") && (s.items ?? []).some((it) => it.fields.btn) && (
        <Group>
          <div className="text-[12px] font-semibold text-muted-foreground">Where each button goes</div>
          {(s.items ?? []).filter((it) => it.fields.btn).map((it) => (
            <LinkEditor key={it.id} label={`“${it.fields.title}”`} link={it.links.btn} sections={doc.sections} onChange={(l) => up(setLink(doc, s.id, "btn", l, it.id))} />
          ))}
        </Group>
      )}
      {s.type === "partners" && (s.items ?? []).length > 0 && (
        <Group>
          <div className="text-[12px] font-semibold text-muted-foreground">Where each logo links to (optional)</div>
          {(s.items ?? []).map((it) => (
            <LinkEditor key={it.id} label={`“${it.fields.name}”`} link={it.links.go} sections={doc.sections} onChange={(l) => up(setLink(doc, s.id, "go", l, it.id))} />
          ))}
        </Group>
      )}
      {s.type === "leaders" && (
        <Group>
          <div className="text-[12px] font-semibold text-muted-foreground">People</div>
          <Button variant="secondary" size="sm" onClick={() => ctx.pickMembers(s.id)}>Add from your members</Button>
          <p className="m-0 text-[12px] text-muted-foreground">Only the members you choose appear, with their name and photo. Nothing else from their profile is shown.</p>
          {(s.items ?? []).map((it) => (
            <Field key={it.id} label={it.fields.name || "Person"}>
              <input className={inputCls} aria-label="Group, such as Executive or Council" placeholder="Group, such as Executive" maxLength={200} value={it.fields.group ?? ""} onChange={(e) => ctx.change(setItemFieldOp(doc, s.id, it.id, "group", e.target.value), "grp-" + it.id)} />
            </Field>
          ))}
        </Group>
      )}

      {s.type === "actions" && (s.items ?? []).length > 0 && (
        <Group>
          <div className="text-[12px] font-semibold text-muted-foreground">Where each action goes</div>
          {(s.items ?? []).map((it) => (
            <LinkEditor key={it.id} label={`“${it.fields.label}”`} link={it.links.go} sections={doc.sections} onChange={(l) => up(setLink(doc, s.id, "go", l, it.id))} />
          ))}
        </Group>
      )}

      {buttonKeys.length > 0 && (
        <Group>
          <div className="text-[12px] font-semibold text-muted-foreground">Where the buttons go</div>
          {buttonKeys.map((k) => <LinkEditor key={k} label={`“${s.props[k]}”`} link={s.links[k]} sections={doc.sections} onChange={(l) => up(setLink(doc, s.id, k, l))} />)}
        </Group>
      )}

      <Group>
        <Check checked={s.menu.on} onChange={(v) => up(setSection(doc, s.id, "menu", { ...s.menu, on: v, label: s.menu.label || b.name }))}>Show this section in the menu</Check>
        {s.menu.on && (
          <Field label="Name in the menu"><input className={inputCls} value={s.menu.label} maxLength={22} onChange={(e) => ctx.change(setSection(doc, s.id, "menu", { ...s.menu, label: e.target.value }), "menu-" + s.id)} /></Field>
        )}
      </Group>

      <Field label="Background"><Segmented label="Background" value={s.bg} onChange={(v) => up(setSection(doc, s.id, "bg", v))} options={[{ value: "plain", label: "Plain" }, { value: "tint", label: "Tinted" }, { value: "brand", label: "Colour" }]} /></Field>
      <Field label="Spacing"><Segmented label="Spacing" value={s.space} onChange={(v) => up(setSection(doc, s.id, "space", v))} options={[{ value: "c", label: "Tight" }, { value: "n", label: "Normal" }, { value: "r", label: "Roomy" }]} /></Field>

      <div className="flex flex-wrap gap-1.5">
        <Button variant="secondary" size="sm" onClick={() => up(moveSection(doc, s.id, -1))}><ChevronUp size={13} /> Up</Button>
        <Button variant="secondary" size="sm" onClick={() => up(moveSection(doc, s.id, 1))}><ChevronDown size={13} /> Down</Button>
        <Button variant="secondary" size="sm" onClick={() => { const r = duplicateSection(doc, s.id); up(r.doc); if (r.section) ctx.select(r.section.id); }}><Copy size={13} /> Duplicate</Button>
        <Button variant="destructive" size="sm" onClick={() => { up(removeSection(doc, s.id)); ctx.select(null); }}><Trash2 size={13} /> Remove</Button>
      </div>
      <p className="m-0 text-[12px] text-muted-foreground">Click any text on the page to change it. Click any photo to add or replace it.</p>
    </div>
  );
}

function setProp2(doc: SiteDocument, id: string, key: string, value: string): SiteDocument {
  return { ...doc, sections: doc.sections.map((s) => (s.id === id ? { ...s, props: { ...s.props, [key]: value } } : s)) };
}

// ── Right, tab 2: the look ────────────────────────────────────────────────────────────────────────

const SWATCHES = ["#1f4d3a", "#14506b", "#7a2e2e", "#a4561c", "#2c3e6b", "#f2b01e"];

export function DesignPanel({ ctx }: { ctx: PanelCtx }) {
  const { doc, brand } = ctx, d = doc.design;
  return (
    <div className="space-y-5">
      <Field label="Start from a look" hint="Switching changes the look only. Your sections, words and photos stay.">
        <div className="grid grid-cols-2 gap-2">
          {TEMPLATES.map((t) => (
            <button key={t.key} type="button" aria-pressed={doc.template === t.key} onClick={() => ctx.change(applyLook(doc, t.key))}
              className={cn("border p-2.5 text-left transition-colors hover:bg-muted", doc.template === t.key ? "border-foreground shadow-[inset_0_0_0_1px_var(--foreground)]" : "border-border")}>
              <div className="text-[13px] font-semibold">{t.name}</div>
              <div className="mt-0.5 text-[12px] leading-snug text-muted-foreground">{t.description}</div>
            </button>
          ))}
        </div>
        <Button variant="secondary" size="sm" onClick={() => {
          if (!window.confirm(`Start a fresh page in the ${TEMPLATES.find((t) => t.key === doc.template)?.name ?? "current"} look? This replaces your sections and words. You can undo it straight after.`)) return;
          const fresh = buildTemplate(doc.template, { brand });
          // Keep what is about the institution rather than the layout: its own colours and logo choice, name and contact details.
          ctx.change({ ...fresh, design: { ...fresh.design, useBrand: d.useBrand, primary: d.primary, logoUrl: d.logoUrl, name: d.name }, footer: { ...fresh.footer, email: doc.footer.email, phone: doc.footer.phone, addr: doc.footer.addr, social: doc.footer.social, hours: doc.footer.hours, links: doc.footer.links } });
        }}>Start a fresh page in this look</Button>
      </Field>

      <Field label="Name shown on the website" hint={doc.design.name ? undefined : "Uses your institution’s name."}>
        <input className={inputCls} value={d.name ?? ""} placeholder={brand.name} maxLength={80} onChange={(e) => ctx.change(setDesign(doc, "name", e.target.value || null), "site-name")} />
      </Field>

      <Group className={d.useBrand ? "border-primary/40 bg-primary/5" : ""}>
        <Check checked={d.useBrand} onChange={(v) => ctx.change(setDesign(doc, "useBrand", v))} hint="Colours and logo come from your portal settings. They stay the way you set them there.">Use my institution’s branding</Check>
        <div className="flex items-center gap-2.5 border border-border bg-background px-2.5 py-2">
          <span className="h-5 w-5 shrink-0 rounded-full ring-1 ring-border" style={{ background: brand.primary }} />
          <span className="min-w-0 text-[13px] font-semibold leading-tight">{brand.name}<span className="block text-[11.5px] font-normal text-muted-foreground">{brand.primary.toUpperCase()}</span></span>
        </div>
        {!d.useBrand && (
          <>
            <Field label="Website colour" hint="Text on top switches between light and dark by itself so it stays readable.">
              <div className="flex flex-wrap items-center gap-2">
                {SWATCHES.map((c) => <button key={c} type="button" aria-label={`Colour ${c}`} aria-pressed={(d.primary ?? "").toLowerCase() === c} onClick={() => ctx.change(setDesign(doc, "primary", c))} className={cn("h-7 w-7 rounded-full border-2 border-white shadow-[0_0_0_1px_var(--border)]", (d.primary ?? "").toLowerCase() === c && "shadow-[0_0_0_2px_var(--foreground)]")} style={{ background: c }} />)}
                <input type="color" aria-label="Pick any colour" className="h-8 w-11 border border-border bg-background p-0.5" value={d.primary && /^#[0-9a-f]{6}$/i.test(d.primary) ? d.primary : "#1f7f78"} onChange={(e) => ctx.change(setDesign(doc, "primary", e.target.value), "color")} />
              </div>
            </Field>
            <Field label="Logo for the website">
              <div className="flex flex-wrap gap-1.5">
                <Button variant="secondary" size="sm" onClick={async () => { const u = await ctx.upload(); if (u) ctx.change(setDesign(doc, "logoUrl", u)); }}>{d.logoUrl ? "Replace logo" : "Upload a logo"}</Button>
                {d.logoUrl && <Button variant="ghost" size="sm" onClick={() => ctx.change(setDesign(doc, "logoUrl", null))}>Remove</Button>}
              </div>
            </Field>
            <Button variant="outline" size="sm" onClick={() => ctx.change(setDesign(doc, "useBrand", true))}>Back to my institution’s branding</Button>
          </>
        )}
        <p className="m-0 flex gap-1.5 text-[12px] leading-snug text-muted-foreground"><span className="font-bold text-primary">✓</span> Changes here only affect this website. Your portal, emails and certificates keep your institution’s branding.</p>
      </Group>

      <Field label="Corners"><Segmented label="Corners" value={d.corners} onChange={(v) => ctx.change(setDesign(doc, "corners", v))} options={[{ value: "0px", label: "Square" }, { value: "6px", label: "Soft" }, { value: "14px", label: "Round" }, { value: "26px", label: "Rounder" }]} /></Field>
      <Field label="Page style"><Segmented label="Page style" value={d.dark ? "dark" : "light"} onChange={(v) => ctx.change(setDesign(doc, "dark", v === "dark"))} options={[{ value: "light", label: "Light" }, { value: "dark", label: "Dark" }]} /></Field>
      <Check checked={d.vibe} onChange={(v) => ctx.change(setDesign(doc, "vibe", v))} hint="Full-colour sections with rounded edges, pill buttons and photo arches.">Full-colour look</Check>
    </div>
  );
}

// ── Right, tab 3: header, footer and search ───────────────────────────────────────────────────────

export function SitePanel({ ctx }: { ctx: PanelCtx }) {
  const { doc } = ctx, f = doc.footer, d = doc.design;
  const first = doc.sections.find((s) => !s.hidden);
  const canOverlay = first?.type === "hero" && first.variant === "full";
  return (
    <div className="space-y-5">
      <section className="space-y-3">
        <PanelTitle>Header</PanelTitle>
        <Field label="Header" hint={canOverlay ? undefined : "“Over the photo” works when the page opens with the full-width photo hero."}>
          <Segmented label="Header" value={d.headerStyle} onChange={(v) => ctx.change(setDesign(doc, "headerStyle", v))} options={[{ value: "solid", label: "Solid" }, { value: "overlay", label: "Over the photo" }]} />
        </Field>
        <Check checked={d.sticky} onChange={(v) => ctx.change(setDesign(doc, "sticky", v))}>Keep the header at the top while scrolling</Check>
      </section>

      <section className="space-y-3">
        <PanelTitle>Footer</PanelTitle>
        <Field label="A short line about you"><input className={inputCls} value={f.text ?? ""} maxLength={200} onChange={(e) => ctx.change(setFooter(doc, "text", e.target.value), "ft-text")} /></Field>
        <Field label="Address"><input className={inputCls} value={f.addr ?? ""} maxLength={200} onChange={(e) => ctx.change(setFooter(doc, "addr", e.target.value), "ft-addr")} /></Field>
        <Field label="Email"><input className={inputCls} type="email" value={f.email ?? ""} onChange={(e) => ctx.change(setFooter(doc, "email", e.target.value), "ft-email")} /></Field>
        <Field label="Phone"><input className={inputCls} value={f.phone ?? ""} onChange={(e) => ctx.change(setFooter(doc, "phone", e.target.value), "ft-phone")} /></Field>
        <Field label="Opening hours" hint="One line each, such as “Mon to Fri, 8am to 5pm”."><textarea className={cn(inputCls, "h-16 resize-none py-2")} value={f.hours ?? ""} maxLength={240} onChange={(e) => ctx.change(setFooter(doc, "hours", e.target.value), "ft-hours")} /></Field>
        <div className="space-y-2">
          <div className="text-[12px] font-semibold text-muted-foreground">Useful links (up to 6), such as your university website</div>
          {Array.from({ length: Math.min(6, (f.links?.length ?? 0) + 1) }, (_, i) => {
            const l = f.links?.[i] ?? { label: "", url: "" };
            const set = (next: { label: string; url: string }) => { const all = [...(f.links ?? [])]; all[i] = next; ctx.change({ ...doc, footer: { ...f, links: all.filter((x) => x.label || x.url) } }, "ft-link-" + i); };
            return (
              <div key={i} className="grid grid-cols-[1fr_1.4fr] gap-1.5">
                <input className={inputCls} aria-label={`Link ${i + 1} name`} placeholder="Name" maxLength={40} value={l.label} onChange={(e) => set({ ...l, label: e.target.value })} />
                <input className={inputCls} aria-label={`Link ${i + 1} address`} placeholder="https://…" value={l.url} onChange={(e) => set({ ...l, url: e.target.value })} />
              </div>
            );
          })}
        </div>
        <Field label="Footer look"><Segmented label="Footer look" value={f.style} onChange={(v) => ctx.change(setFooter(doc, "style", v))} options={[{ value: "dark", label: "Dark" }, { value: "brand", label: "Colour" }, { value: "light", label: "Light" }]} /></Field>
        <Check checked={f.menu} onChange={(v) => ctx.change(setFooter(doc, "menu", v))}>List my sections in the footer, grouped by purpose</Check>
        <Check checked={f.credit} onChange={(v) => ctx.change(setFooter(doc, "credit", v))}>Show “Powered by AlumUnion”</Check>
        <div className="space-y-2">
          <div className="text-[12px] font-semibold text-muted-foreground">Social links (leave empty to hide)</div>
          {(["facebook", "instagram", "linkedin", "x", "youtube"] as const).map((k) => (
            <input key={k} className={inputCls} aria-label={`${k} address`} placeholder={`${k[0].toUpperCase()}${k.slice(1)} address (https://…)`} value={f.social[k] ?? ""} onChange={(e) => ctx.change(setSocial(doc, k, e.target.value), "soc-" + k)} />
          ))}
        </div>
      </section>

      <section className="space-y-3">
        <PanelTitle>Search and sharing</PanelTitle>
        <Field label="Page title" hint="What shows on a browser tab and in search results."><input className={inputCls} value={doc.seo.title ?? ""} maxLength={70} placeholder={ctx.brand.name} onChange={(e) => ctx.change(setSeo(doc, "title", e.target.value || null), "seo-t")} /></Field>
        <Field label="Short description" hint="One or two sentences for search results and link previews."><textarea className={cn(inputCls, "h-20 resize-none py-2")} value={doc.seo.description ?? ""} maxLength={200} onChange={(e) => ctx.change(setSeo(doc, "description", e.target.value || null), "seo-d")} /></Field>
        <Field label="Image for link previews">
          <div className="flex flex-wrap items-center gap-1.5">
            {doc.seo.shareImage && <img src={doc.seo.shareImage} alt="" className="h-12 w-20 border border-border object-cover" />}
            <Button variant="secondary" size="sm" onClick={async () => { const u = await ctx.upload(); if (u) ctx.change(setSeo(doc, "shareImage", u)); }}>{doc.seo.shareImage ? "Replace" : "Upload an image"}</Button>
            {doc.seo.shareImage && <Button variant="ghost" size="sm" onClick={() => ctx.change(setSeo(doc, "shareImage", null))}>Remove</Button>}
          </div>
        </Field>
      </section>
    </div>
  );
}

export { sectionName };
export type { SectionType };
