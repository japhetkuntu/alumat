"use client";

import { BLOCKS, PORTAL_PAGES, linkOk, type LinkKind, type SiteLink, type SiteSection } from "@alumni/site";
import { Field, inputCls } from "./fields";
import { sectionName } from "./doc-ops";

const KINDS: [LinkKind, string][] = [["none", "Not set yet"], ["anchor", "A section on this page"], ["join", "Join the community"], ["signin", "Member sign in"], ["portal", "A page in the member portal"], ["external", "A website address"], ["email", "An email address"], ["phone", "A phone number"]];

/** Where one button goes. Only a few safe places, so a button can never point at anything unexpected. */
export function LinkEditor({ label, link, sections, onChange }: { label: string; link: SiteLink | undefined; sections: SiteSection[]; onChange: (l: SiteLink) => void }) {
  const l: SiteLink = link ?? { t: "none" };
  const ok = linkOk(l);
  const targets = sections.filter((s) => s.menu.on || BLOCKS[s.type].live || s.type !== "hero");
  return (
    <Field label={`${label} goes to`} hint={!ok ? <span className="text-amber-700">Not set yet, so this button does nothing.</span> : undefined}>
      <select className={inputCls} value={l.t} aria-label={`Where “${label}” goes`}
        onChange={(e) => { const t = e.target.value as LinkKind; onChange({ t, v: t === "anchor" ? targets[0]?.id ?? "" : t === "portal" ? "contributions" : "" }); }}>
        {KINDS.map(([v, n]) => <option key={v} value={v}>{n}</option>)}
      </select>
      {l.t === "anchor" && (
        <select className={inputCls} value={l.v ?? ""} aria-label="Which section" onChange={(e) => onChange({ t: "anchor", v: e.target.value })}>
          {targets.map((s) => <option key={s.id} value={s.id}>{sectionName(s)}</option>)}
        </select>
      )}
      {l.t === "portal" && (
        <select className={inputCls} value={l.v ?? ""} aria-label="Which portal page" onChange={(e) => onChange({ t: "portal", v: e.target.value })}>
          {Object.entries(PORTAL_PAGES).map(([k, p]) => <option key={k} value={k}>{p.label}</option>)}
        </select>
      )}
      {(l.t === "external" || l.t === "email" || l.t === "phone") && (
        <input className={inputCls} value={l.v ?? ""} aria-label="Address" placeholder={l.t === "external" ? "https://…" : l.t === "email" ? "name@example.com" : "+233 …"} onChange={(e) => onChange({ t: l.t, v: e.target.value })} />
      )}
    </Field>
  );
}
