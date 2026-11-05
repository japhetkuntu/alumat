import { BLOCKS, MENU_DEFAULT } from "./blocks";
import { uid } from "./id";
import { linkOk } from "./links";
import { STOCK } from "./photos";
import type { SectionType, SiteDocument, SiteItem, SiteSection } from "./types";
import { newSection } from "./templates";

/** Words that mark a place still waiting for the admin's own. A page that still contains one is flagged before it goes live. */
export const PLACEHOLDERS = ["Say a little about it here.", "Full name", "New question", "Write the answer here.", "New card", "Add your words here.", "Membership type", "Chapter name", "Partner name", "Add their role", "New file"];

const item = (fields: Record<string, string>, links: SiteItem["links"] = {}): SiteItem => ({ id: uid(), fields, links });

const COPY: Partial<Record<SectionType, Record<string, string>>> = {
  actions: {},
  video: { heading: "Watch", body: "", url: "" }, social: { heading: "Follow us", body: "Stay close to what is happening.", fb: "" }, downloads: { heading: "Downloads" },
  membership: { heading: "Ways to belong" }, chapters: { heading: "Chapters and branches" }, partners: { heading: "Our partners" },
  hero: { heading: "Your headline", body: "A line or two that tells visitors who you are and why to join.", b1: "Join the community" },
  text: { heading: "Our story", body: "Add your words here." },
  imagetext: { heading: "A heading", body: "Add your words here.", link: "Learn more" },
  news: { heading: "News" }, events: { heading: "Coming up" }, services: { heading: "Services for members" }, store: { heading: "The store" },
  fundraisers: { heading: "Fundraisers you can back" }, jobs: { heading: "Opportunities" }, businesses: { heading: "Member businesses" }, communities: { heading: "Communities" }, spotlights: { heading: "Member spotlights" },
  gallery: { heading: "Photos" }, leaders: { heading: "Our leaders" }, faq: { heading: "Questions" }, cards: { heading: "What we do" },
  cta: { heading: "Ready to join?", body: "It takes two minutes.", b1: "Join now" },
  contact: { heading: "Get in touch", addr: "", mail: "", tel: "" },
};

/** A section as it first appears when the admin adds it: sensible words they will replace, and the starting photo where one belongs. */
export function createSection(type: SectionType): SiteSection {
  const variant = Object.keys(BLOCKS[type].variants)[0];
  const s = newSection(type, variant, { ...(COPY[type] ?? { heading: BLOCKS[type].name }) });
  if (type === "hero") { s.imgs.main = STOCK.hero; s.links.b1 = { t: "join" }; s.menu = { on: false, label: "" }; }
  if (type === "imagetext") { s.imgs.main = STOCK.graduate; s.links.link = { t: "none" }; }
  if (type === "cta") { s.bg = "brand"; s.links.b1 = { t: "join" }; s.menu = { on: false, label: "" }; }
  if (type === "contact") s.imgs.map = STOCK.campus;
  if (type === "cards") s.items = [0, 1, 2].map((i) => { const it = item({ title: ["First", "Second", "Third"][i], text: "Say a little about it here.", btn: "" }, { btn: { t: "none" } }); s.imgs["i" + it.id] = STOCK.cards[i]; return it; });
  if (type === "faq") s.items = [item({ q: "New question", a: "Write the answer here." })];
  if (type === "actions") { s.menu = { on: false, label: "" }; s.items = [item({ label: "Join the community", hint: "Create your profile" }, { go: { t: "join" } }), item({ label: "Pay dues or give", hint: "Support your alumni family" }, { go: { t: "portal", v: "contributions" } }), item({ label: "Update your details", hint: "Stay in touch" }, { go: { t: "portal", v: "profile" } })]; }
  if (type === "downloads") s.items = [];
  if (type === "membership") { s.bg = "tint"; s.items = []; }
  if (type === "leaders") s.items = [];
  if (type === "gallery") s.items = [];
  return s;
}

/** A blank item for a section that holds several (cards, questions, people, photos). */
export function createItem(type: SectionType): SiteItem {
  switch (type) {
    case "cards": return item({ title: "New card", text: "Say a little about it here.", btn: "" }, { btn: { t: "none" } });
    case "faq": return item({ q: "New question", a: "Write the answer here." });
    case "actions": return item({ label: "Pay dues or give", hint: "" }, { go: { t: "portal", v: "contributions" } });
    case "leaders": return item({ name: "Full name", role: "Add their role", group: "" });
    case "membership": return item({ title: "Membership type", price: "", text: "Say a little about it here.", btn: "Join now" }, { btn: { t: "join" } });
    case "chapters": return item({ name: "Chapter name", where: "", contact: "" });
    case "partners": return item({ name: "Partner name" }, { go: { t: "none" } });
    case "downloads": return item({ label: "New file", hint: "" }, { go: { t: "none" } });
    case "hero": return item({ title: "", text: "" }, { go: { t: "none" } });
    default: return item({});
  }
}

export interface PublishCheck { ok: boolean; info?: boolean; /** A failing check that stops publishing, not just a warning. */ blocking?: boolean; text: string; detail?: string; sectionId?: string }

/** The plain-words check an admin sees before going live. Each failing check can point at the section to look at. */
export function checkBeforePublish(doc: SiteDocument, hiddenByPlatform: string[] = []): PublishCheck[] {
  const out: PublishCheck[] = [];
  const secs = doc.sections.filter((s) => !s.hidden);
  const dead: { id: string; what: string }[] = [];
  for (const s of secs) {
    const keys = s.type === "hero" ? ["b1", "b2"].filter((k) => s.props[k]) : s.type === "cta" ? ["b1"] : s.type === "imagetext" && s.props.link ? ["link"] : [];
    for (const k of keys) if (!linkOk(s.links[k])) dead.push({ id: s.id, what: `“${s.props[k]}” in ${BLOCKS[s.type].name}` });
    if (s.type === "cards") for (const it of s.items ?? []) if (it.fields.btn && !linkOk(it.links.btn)) dead.push({ id: s.id, what: `“${it.fields.btn}” on the card “${it.fields.title}”` });
  }
  out.push(dead.length
    ? { ok: false, text: `${dead.length} button${dead.length === 1 ? "" : "s"} go nowhere yet`, detail: dead.slice(0, 3).map((d) => d.what).join(", ") + (dead.length > 3 ? ` and ${dead.length - 3} more` : ""), sectionId: dead[0].id }
    : { ok: true, text: "Every button goes somewhere" });

  const empty = secs.filter((s) => (s.type === "video" && !s.props.url) || (s.type === "downloads" && !(s.items ?? []).some((i) => linkOk(i.links.go))) || (s.type === "social" && !s.props.fb && !Object.values(doc.footer.social).some(Boolean)));
  if (empty.length) out.push({ ok: false, text: `${empty.length} section${empty.length === 1 ? " is" : "s are"} empty, so visitors will not see ${empty.length === 1 ? "it" : "them"}`, detail: "Add the video link, the files or the social pages, or remove the section.", sectionId: empty[0].id });

  const noHeading = secs.filter((s) => "heading" in s.props && !s.props.heading?.trim() && s.type !== "hero" && s.type !== "actions");
  out.push(noHeading.length ? { ok: false, text: `${noHeading.length} section${noHeading.length === 1 ? " has" : "s have"} no heading`, detail: "A heading helps visitors and search engines.", sectionId: noHeading[0].id } : { ok: true, text: "Every section has a heading" });

  const holders = secs.filter((s) => JSON.stringify([s.props, s.items]).split(/\\"|"/).some((chunk) => PLACEHOLDERS.some((p) => chunk.includes(p))));
  out.push(holders.length ? { ok: false, text: `${holders.length} section${holders.length === 1 ? " still has" : "s still have"} words waiting to be replaced`, detail: "Replace or remove “Say a little about it here”, “Full name” or “Write the answer here” first. They would show to visitors.", blocking: true, sectionId: holders[0].id } : { ok: true, text: "No placeholder words are left" });

  const menu = secs.filter((s) => s.menu.on).length;
  out.push(menu >= 2 ? { ok: true, text: `The menu has ${menu + 1} items and jumps to each section` } : { ok: false, text: "The menu is nearly empty", detail: "Tick “Show in menu” on the sections visitors will look for." });
  out.push(doc.footer.email || doc.footer.phone ? { ok: true, text: "Visitors can contact you from the footer" } : { ok: false, text: "The footer has no contact details", detail: "Add an email or phone number under Header and footer." });
  const off = Object.entries(doc.features).filter(([k, v]) => v === false).length + hiddenByPlatform.length;
  if (off) out.push({ ok: true, info: true, text: `${off} feature${off === 1 ? " is" : "s are"} switched off, so visitors will not see ${off === 1 ? "it" : "them"}` });
  return out;
}

export const hasPlaceholder = (text: string) => PLACEHOLDERS.some((p) => text.includes(p));
