import { BLOCKS, createItem, createSection, type SectionType, type SiteDocument, type SiteItem, type SiteLink, type SiteSection, uid } from "@alumni/site";

/** Pure changes to a page. Each takes a page and returns a new one, so history and autosave stay simple. */
type Doc = SiteDocument;
const clone = <T,>(v: T): T => (typeof structuredClone === "function" ? structuredClone(v) : JSON.parse(JSON.stringify(v)));

const withSection = (doc: Doc, id: string, fn: (s: SiteSection) => void): Doc => {
  const next = clone(doc);
  const s = next.sections.find((x) => x.id === id);
  if (s) fn(s);
  return next;
};

export const setProp = (doc: Doc, id: string, key: string, value: string) => withSection(doc, id, (s) => { s.props[key] = value; });
export const setItemField = (doc: Doc, id: string, itemId: string, key: string, value: string) => withSection(doc, id, (s) => { const it = s.items?.find((i) => i.id === itemId); if (it) it.fields[key] = value; });
export const setSection = <K extends keyof SiteSection>(doc: Doc, id: string, key: K, value: SiteSection[K]) => withSection(doc, id, (s) => { s[key] = value; });
export const setPhoto = (doc: Doc, id: string, slot: string, url: string) => withSection(doc, id, (s) => { s.imgs[slot] = url; });
export const removePhoto = (doc: Doc, id: string, slot: string) => withSection(doc, id, (s) => { delete s.imgs[slot]; });
export const setLink = (doc: Doc, id: string, key: string, link: SiteLink, itemId?: string) => withSection(doc, id, (s) => { if (itemId) { const it = s.items?.find((i) => i.id === itemId); if (it) it.links[key] = link; } else s.links[key] = link; });

export function addItem(doc: Doc, id: string): { doc: Doc; item?: SiteItem } {
  let made: SiteItem | undefined;
  const next = withSection(doc, id, (s) => { made = createItem(s.type); (s.items ??= []).push(made); });
  return { doc: next, item: made };
}
export const removeItem = (doc: Doc, id: string, itemId: string) => withSection(doc, id, (s) => { s.items = (s.items ?? []).filter((i) => i.id !== itemId); delete s.imgs["i" + itemId]; });
export const moveItem = (doc: Doc, id: string, itemId: string, dir: -1 | 1) => withSection(doc, id, (s) => {
  const list = s.items ?? []; const i = list.findIndex((x) => x.id === itemId), j = i + dir;
  if (i >= 0 && j >= 0 && j < list.length) { const [m] = list.splice(i, 1); list.splice(j, 0, m); }
});

export function insertSection(doc: Doc, type: SectionType, at?: number): { doc: Doc; section: SiteSection } {
  const section = createSection(type);
  const next = clone(doc);
  const last = next.sections.length - 1;
  // A new section goes before the closing call to action unless the admin chose a spot.
  const pos = at ?? (next.sections[last]?.type === "cta" ? last : next.sections.length);
  next.sections.splice(Math.max(0, Math.min(pos, next.sections.length)), 0, section);
  return { doc: next, section };
}
export function removeSection(doc: Doc, id: string): Doc { const next = clone(doc); next.sections = next.sections.filter((s) => s.id !== id); return next; }
export function moveSection(doc: Doc, id: string, dir: -1 | 1): Doc {
  const next = clone(doc); const i = next.sections.findIndex((s) => s.id === id), j = i + dir;
  if (i >= 0 && j >= 0 && j < next.sections.length) { const [m] = next.sections.splice(i, 1); next.sections.splice(j, 0, m); }
  return next;
}
export function reorderSection(doc: Doc, fromId: string, toId: string): Doc {
  const next = clone(doc); const from = next.sections.findIndex((s) => s.id === fromId), to = next.sections.findIndex((s) => s.id === toId);
  if (from < 0 || to < 0 || from === to) return doc;
  const [m] = next.sections.splice(from, 1); next.sections.splice(to, 0, m);
  return next;
}
export function duplicateSection(doc: Doc, id: string): { doc: Doc; section?: SiteSection } {
  const next = clone(doc); const i = next.sections.findIndex((s) => s.id === id);
  if (i < 0) return { doc };
  const copy = clone(next.sections[i]); copy.id = uid();
  // Every item gets its own identifier, and its photo moves with it.
  const imgs: Record<string, string> = {};
  for (const [k, v] of Object.entries(copy.imgs)) if (!k.startsWith("i") || k.length < 2) imgs[k] = v;
  copy.items?.forEach((it) => { const old = it.id; it.id = uid(); const url = next.sections[i].imgs["i" + old]; if (url) imgs["i" + it.id] = url; });
  copy.imgs = imgs;
  next.sections.splice(i + 1, 0, copy);
  return { doc: next, section: copy };
}

export const setDesign = <K extends keyof Doc["design"]>(doc: Doc, key: K, value: Doc["design"][K]): Doc => ({ ...doc, design: { ...doc.design, [key]: value } });
export const setFooter = <K extends keyof Doc["footer"]>(doc: Doc, key: K, value: Doc["footer"][K]): Doc => ({ ...doc, footer: { ...doc.footer, [key]: value } });
export const setSeo = (doc: Doc, key: keyof Doc["seo"], value: string | null): Doc => ({ ...doc, seo: { ...doc.seo, [key]: value } });
export const setFeature = (doc: Doc, key: keyof Doc["features"], on: boolean): Doc => ({ ...doc, features: { ...doc.features, [key]: on } });
export const setSocial = (doc: Doc, key: string, value: string): Doc => ({ ...doc, footer: { ...doc.footer, social: { ...doc.footer.social, [key]: value } } });

export const sectionName = (s: SiteSection) => (s.menu.on && s.menu.label ? s.menu.label : BLOCKS[s.type].name);
