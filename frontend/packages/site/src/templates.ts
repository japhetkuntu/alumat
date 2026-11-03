import { MENU_DEFAULT } from "./blocks";
import { uid } from "./id";
import { STOCK } from "./photos";
import type { Background, SectionType, SiteBrand, SiteDocument, SiteItem, SiteLink, SiteSection, Spacing } from "./types";

export interface TemplateContext { brand: SiteBrand; /** "community" for non-alumni organisations; changes a few words only. */ kind?: "alumni" | "community" }

export interface TemplateInfo { key: string; name: string; description: string; vibe: boolean; font: SiteDocument["design"]["font"]; corners: string }

export const TEMPLATES: TemplateInfo[] = [
  { key: "vibrant", name: "Modern", description: "A full-colour opening, big photos and clean, flat cards.", vibe: true, font: "vibrant", corners: "14px" },
  { key: "journal", name: "Simple", description: "A calm letter from home: plain words, clear choices, big story photos.", vibe: false, font: "journal", corners: "0px" },
  { key: "classic", name: "Classic", description: "Calm and square-cornered, like a well-kept journal.", vibe: false, font: "heritage", corners: "6px" },
];

const none: SiteLink = { t: "none" };

export function newSection(type: SectionType, variant: string, props: Record<string, string> = {}, extra: Partial<SiteSection> = {}): SiteSection {
  return {
    id: uid(), type, variant, bg: "plain", space: "n", hidden: false, count: 3,
    menu: { on: type in MENU_DEFAULT, label: MENU_DEFAULT[type] ?? "" },
    props, imgs: {}, links: {}, ...extra,
  };
}

const item = (fields: Record<string, string>, links: Record<string, SiteLink> = {}): SiteItem => ({ id: uid(), fields, links });
const sec = (type: SectionType, variant: string, props: Record<string, string>, bg: Background = "plain", extra: Partial<SiteSection> = {}, space: Spacing = "n") =>
  newSection(type, variant, props, { bg, space, ...extra });

/** A starting page for an institution. Every word is plain and honest, nothing here invents a person, a number or a quote. */
export function buildTemplate(key: string, ctx: TemplateContext): SiteDocument {
  const info = TEMPLATES.find((t) => t.key === key) ?? TEMPLATES[0];
  if (info.key === "journal") return buildSimple(info, ctx);
  const community = ctx.kind === "community";
  const short = ctx.brand.name.replace(/\b(alumni|association|society|union)\b/gi, "").trim() || ctx.brand.name;

  const hero = sec("hero", "split", {
    heading: community ? `A community that looks out for its own,` : `Once a ${short} graduate,`,
    heading2: community ? "and for each other." : "always part of the family.",
    body: ctx.brand.tagline?.trim() || (community ? "Meet members, find events near you and give back together." : "Reconnect with your classmates, find events near you and give back to the place that shaped you."),
    b1: community ? "Join us" : "Join the community", b2: "See what is on",
  }, info.vibe ? "brand" : "plain", { imgs: { main: STOCK.hero }, links: { b1: { t: "join" }, b2: { t: "none" } }, menu: { on: false, label: "" } });

  const actions = newSection("actions", "bar", {}, {
    bg: "plain", space: "c", menu: { on: false, label: "" },
    items: [
      item({ label: community ? "Join us" : "Join the community", hint: "Create your profile" }, { go: { t: "join" } }),
      item({ label: "Pay dues or give", hint: "Support your family" }, { go: { t: "portal", v: "contributions" } }),
      item({ label: "Update your details", hint: "Stay in touch" }, { go: { t: "portal", v: "profile" } }),
      item({ label: "Member sign in", hint: "Directory, events, jobs" }, { go: { t: "signin" } }),
    ],
  });
  const stats = sec("stats", "row", {}, "tint", {}, "c");

  const steps = sec("cards", "steps", { heading: `Your ${community ? "community" : "alumni family"} in three steps` }, "tint", {
    items: [
      item({ title: "Join", text: community ? "Create your profile. It takes two minutes." : "Make your profile and confirm your graduating year. It takes two minutes.", btn: "Join now" }, { btn: { t: "join" } }),
      item({ title: "Connect", text: "Find members near you and come to the next event.", btn: "See events" }, { btn: none }),
      item({ title: "Give back", text: "Mentor someone, back a good cause or hire from your own network.", btn: "Ways to give" }, { btn: none }),
    ],
  });
  steps.items!.forEach((it, i) => { steps.imgs["i" + it.id] = STOCK.steps[i]; });

  const services = sec("services", "cards", { heading: "Services for members" });
  const store = sec("store", "grid", { heading: "The store" }, "tint", { count: 4 });
  const fundraisers = sec("fundraisers", "cards", { heading: "Fundraisers you can back" }, info.vibe ? "brand" : "plain");
  const news = sec("news", info.vibe ? "grid" : "list", { heading: "News" });
  const spotlights = sec("spotlights", "cards", { heading: "Member spotlights" });
  const events = sec("events", "list", { heading: "Coming up" }, "tint");
  const jobs = sec("jobs", "list", { heading: "Opportunities" });
  const businesses = sec("businesses", "cards", { heading: "Member businesses" }, "tint");
  const communities = sec("communities", "cards", { heading: "Communities" });
  const cta = sec("cta", "band", { heading: community ? "Not a member yet?" : "Not a member yet?", body: "It takes two minutes to join.", b1: "Join now" }, "brand", { links: { b1: { t: "join" } }, menu: { on: false, label: "" } });

  // The buttons that point at a section on this page, wired once the sections exist.
  hero.links.b2 = { t: "anchor", v: events.id };
  steps.items![1].links.btn = { t: "anchor", v: events.id };
  steps.items![2].links.btn = { t: "anchor", v: fundraisers.id };

  const sections = info.vibe
    ? [hero, actions, stats, steps, services, store, fundraisers, news, spotlights, events, jobs, businesses, communities, cta]
    : [hero, actions, stats, services, news, spotlights, events, store, fundraisers, jobs, businesses, communities, cta];

  return {
    schemaVersion: 1,
    template: info.key,
    design: { useBrand: true, primary: null, logoUrl: null, name: null, font: info.font, corners: info.corners, dark: false, vibe: info.vibe, headerStyle: "solid", sticky: false },
    sections,
    footer: { text: ctx.brand.tagline ?? null, email: null, phone: null, addr: null, menu: true, style: "dark", credit: true, social: {} },
    seo: { title: null, description: null, shareImage: null },
    features: { Services: true, Store: true, Fundraisers: true, Jobs: true, Businesses: true, Communities: true },
  };
}

/** Change the look only. The sections, words and photos stay exactly as they are. */
export function applyLook(doc: SiteDocument, key: string): SiteDocument {
  const info = TEMPLATES.find((t) => t.key === key);
  if (!info) return doc;
  return { ...doc, template: info.key, design: { ...doc.design, font: info.font, corners: info.corners, vibe: info.vibe } };
}

/** The Simple look: a plain opening, three clear things to do, then stories. Nothing here invents a person, a number or a quote. */
function buildSimple(info: TemplateInfo, ctx: TemplateContext): SiteDocument {
  const community = ctx.kind === "community";
  const short = ctx.brand.name.replace(/\b(alumni|association|society|union)\b/gi, "").trim() || ctx.brand.name;
  const hero = sec("hero", "intro", {
    heading: community ? "A community that looks out for its own" : "A lifelong community",
    body: ctx.brand.tagline?.trim() || (community
      ? "This page is your home. Meet members, find events near you and give back together."
      : `As a ${short} graduate you are part of a community shaping the world. This page is your home: reconnect, ask for help, or help advance our work.`),
    b1: community ? "Join us" : "Join the community", b2: "Member sign in",
  }, "tint", { imgs: { bg: STOCK.campus }, links: { b1: { t: "join" }, b2: { t: "signin" } }, menu: { on: false, label: "" } }, "r");

  const boxes = sec("cards", "plain", { heading: "What would you like to do?" }, "plain", {
    items: [
      item({ title: "Stay connected", text: "Find classmates and members near you, and join groups that share your interests.", btn: "Open the directory" }, { btn: { t: "portal", v: "directory" } }),
      item({ title: "Update your details", text: "Keep your record current so we can reach you with news and events.", btn: "Update profile" }, { btn: { t: "portal", v: "profile" } }),
      item({ title: "Get involved", text: "Mentor someone, hire from your own network or support a good cause.", btn: "Ways to give" }, { btn: { t: "portal", v: "contributions" } }),
    ],
  });

  const spotlights = sec("spotlights", "cards", { heading: community ? "Member stories" : "Alumni stories" });
  const news = sec("news", "grid", { heading: "News" }, "tint");
  const events = sec("events", "list", { heading: "Coming up" });
  const services = sec("services", "cards", { heading: "Services for members" }, "tint");
  const store = sec("store", "grid", { heading: "The store" }, "plain", { count: 4 });
  const fundraisers = sec("fundraisers", "cards", { heading: "Fundraisers you can back" }, "tint");
  const jobs = sec("jobs", "list", { heading: "Opportunities" });
  const businesses = sec("businesses", "cards", { heading: "Member businesses" }, "tint");
  const communities = sec("communities", "cards", { heading: "Communities" });
  const cta = sec("cta", "band", { heading: "Not a member yet?", body: "It takes two minutes to join.", b1: "Join now" }, "brand", { links: { b1: { t: "join" } }, menu: { on: false, label: "" } });

  return {
    schemaVersion: 1, template: info.key,
    design: { useBrand: true, primary: null, logoUrl: null, name: null, font: info.font, corners: info.corners, dark: false, vibe: false, headerStyle: "solid", sticky: false },
    sections: [hero, boxes, spotlights, news, events, services, store, fundraisers, jobs, businesses, communities, cta],
    footer: { text: ctx.brand.tagline ?? null, email: null, phone: null, addr: null, menu: true, style: "light", credit: true, social: {} },
    seo: { title: null, description: null, shareImage: null },
    features: { Services: true, Store: true, Fundraisers: true, Jobs: true, Businesses: true, Communities: true },
  };
}
