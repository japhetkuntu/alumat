import { BLOCKS } from "./blocks";
import { crestUrl } from "./brand";
import { nameOf, tokens } from "./color";
import type { SiteCtx } from "./context";
import type { EditApi } from "./edit";
import { SiteFooter } from "./Footer";
import { SiteHeader } from "./Header";
import { Lightbox } from "./Lightbox";
import { Motion } from "./Motion";
import { SectionBody } from "./Sections";
import { visibleSections, whyHidden } from "./visibility";
import type { SiteBrand, SiteData, SiteDocument, SiteSection } from "./types";

interface SiteViewProps {
  doc: SiteDocument;
  brand: SiteBrand;
  data: SiteData;
  /** Platform feature keys switched off for this institution. */
  platformDisabled?: string[];
  edit?: EditApi;
  sample?: boolean;
  currency?: string;
  loading?: boolean;
  /** The id of the element that scrolls, so the menu can follow it inside the editor. */
  className?: string;
  /** A page other than the home page (a full story): drawn between the same header and footer instead of the sections. */
  reader?: React.ReactNode;
}

const menuLabel = (s: SiteSection) => (s.menu.label || BLOCKS[s.type].name).trim() || BLOCKS[s.type].name;

/** The whole website: header, sections, footer. The same component draws the live page (no `edit`) and the editor's page (with `edit`). */
export function SiteView({ doc, brand, data, platformDisabled = [], edit, sample, currency, loading, className = "", reader }: SiteViewProps) {
  const editing = !!edit;
  const sections = visibleSections(doc, data, platformDisabled, editing);
  const ctx: SiteCtx = { doc, brand, data, sections, edit, sample, currency, loading };
  const shown = editing ? sections.filter((s) => !whyHidden(s, doc, data, platformDisabled)) : sections;
  const items = shown.filter((s) => s.menu.on).map((s) => ({ sec: s, label: menuLabel(s) }));
  const d = doc.design, name = nameOf(d, brand);
  const customLogo = !d.useBrand && d.logoUrl ? d.logoUrl : null;
  const mark = d.useBrand
    ? <img className="mark crest" src={brand.logoUrl || crestUrl(brand.primary, name[0] ?? "A")} alt="" />
    : <span className="mark">{(name.trim()[0] ?? "A").toUpperCase()}</span>;
  const first = sections.find((s) => !s.hidden);
  const overlay = d.headerStyle === "overlay" && first?.type === "hero" && first.variant === "full";

  return (
    <div id="alsite-top" className={`alsite tpl-${doc.template} ${d.vibe ? "vibe" : ""} ${d.sticky ? "stickyhdr" : ""} ${d.dark ? "dark" : ""} ${className}`} style={tokens(d, brand) as React.CSSProperties}>
      <SiteHeader logoUrl={customLogo} mark={mark} name={name} items={items.map((i) => ({ id: i.sec.id, label: i.label }))} overlay={reader ? false : overlay} sticky={d.sticky} inEditor={editing} base={reader ? "/" : ""} />
      {reader ? <main className="sec bg-plain sp-n readpage">{reader}</main> : sections.map((s, i) => {
        const why = editing ? whyHidden(s, doc, data, platformDisabled) : null;
        const cls = ["sec", s.type === "hero" ? `hero-${s.variant === "full" ? "full" : s.variant === "center" ? "center" : s.variant === "intro" ? "intro" : "split"}` : "", `bg-${s.bg}`, `sp-${s.space}`, edit?.selectedId === s.id ? "selected" : "", why ? "featoff" : ""].join(" ");
        return (
          <div key={s.id} style={{ display: "contents" }}>
            {edit && i === 0 && <InsertGap at={0} edit={edit} />}
            <section id={`sec-${s.id}`} className={cls} data-block={s.id} {...(why ? { "data-off": why } : {})} onClick={edit ? () => edit.select(s.id) : undefined} aria-label={BLOCKS[s.type].name}>
              {edit && <><span className="tag">{BLOCKS[s.type].name}</span><SectionToolbar sec={s} edit={edit} /></>}
              <SectionBody sec={s} ctx={ctx} />
            </section>
            {edit && <InsertGap at={i + 1} edit={edit} />}
          </div>
        );
      })}
      {editing && sections.length === 0 && <div className="sec"><div className="wrap narrow"><button type="button" className="emptybox" onClick={() => edit!.insertAt(0)}>This page is empty. Add the first section</button></div></div>}
      <SiteFooter base={reader ? "/" : ""} ctx={ctx} brandMark={mark} logoUrl={customLogo} items={items} featureOf={(s) => !!BLOCKS[s.type].feature} />
      <Motion rootId="alsite-top" enabled={!editing} />
      <Lightbox rootId="alsite-top" enabled={!editing} />
    </div>
  );
}

function InsertGap({ at, edit }: { at: number; edit: EditApi }) {
  return <div className="ins"><button type="button" onClick={(e) => { e.stopPropagation(); edit.insertAt(at); }}>+ Add a section here</button></div>;
}

function SectionToolbar({ sec, edit }: { sec: SiteSection; edit: EditApi }) {
  const stop = (fn: () => void) => (e: React.MouseEvent) => { e.stopPropagation(); fn(); };
  return (
    <div className="tb" role="toolbar" aria-label="Section actions">
      <button type="button" onClick={stop(() => edit.move(sec.id, -1))} title="Move up">Up</button>
      <button type="button" onClick={stop(() => edit.move(sec.id, 1))} title="Move down">Down</button>
      <button type="button" onClick={stop(() => edit.duplicate(sec.id))} title="Duplicate">Duplicate</button>
      <button type="button" className="danger" onClick={stop(() => edit.remove(sec.id))} title="Remove this section">Remove</button>
    </div>
  );
}

/** What shows while the page is on its way: the bar and a calm block where the first section goes, in the same shapes as the real page. */
export function SiteSkeleton() {
  return (
    <div className="alsite vibe" style={{ minHeight: "100vh" }} aria-busy="true" aria-label="Loading">
      <div className="hdr"><i className="sk" style={{ width: 180, height: 36 }} /><span style={{ flex: 1 }} /><i className="sk" style={{ width: 140, height: 44, borderRadius: 999 }} /></div>
      <div className="sec bg-brand" style={{ padding: "72px 32px" }}>
        <div className="wrap"><i className="sk" style={{ width: "62%", height: 54 }} /><i className="sk" style={{ width: "48%", height: 54, marginTop: 14 }} /><i className="sk" style={{ width: "40%", height: 18, marginTop: 28 }} /><i className="sk" style={{ width: "34%", height: 18, marginTop: 10 }} /><i className="sk" style={{ width: 200, height: 48, borderRadius: 999, marginTop: 32 }} /></div>
      </div>
    </div>
  );
}
