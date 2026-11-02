import type { SectionType, WebsiteFeature } from "./types";

export interface BlockInfo {
  name: string;
  description: string;
  variants: Record<string, string>;
  /** Fills itself from the institution's own data. */
  live?: string;
  /** The website feature switch that shows or hides it. */
  feature?: WebsiteFeature;
  /** The platform feature key that must be on for the institution. */
  platformFeature?: string;
  counted?: boolean;
  items?: { word: string; many: string; label: (fields: Record<string, string>) => string };
  /** Where "View all" goes in the member portal. */
  allHref?: string;
}

export const BLOCKS: Record<SectionType, BlockInfo> = {
  hero: { name: "Hero", description: "The big opening of the page: a headline, a few words and buttons.", variants: { split: "Photo beside text", full: "Full-width photo", center: "Centred text" } },
  text: { name: "Text", description: "A heading and some paragraphs.", variants: { one: "One column", two: "Two columns" } },
  imagetext: { name: "Image and text", description: "A photo beside a heading and text.", variants: { left: "Image left", right: "Image right" } },
  news: { name: "Latest news", description: "Your latest news posts, filled in for you.", variants: { list: "List", grid: "Cards" }, live: "News posts", platformFeature: "News", counted: true, allHref: "/news" },
  events: { name: "Upcoming events", description: "Your upcoming events, filled in for you.", variants: { list: "List", cards: "Cards" }, live: "Events", platformFeature: "Events", counted: true, allHref: "/events" },
  stats: { name: "Community numbers", description: "Member and event numbers, filled in for you.", variants: { row: "Row" }, live: "Member and event counts" },
  gallery: { name: "Photo gallery", description: "A grid of photos you add.", variants: { grid: "Even grid", mosaic: "Mosaic" }, items: { word: "photo", many: "Photos", label: () => "Photo" } },
  leaders: { name: "Leaders", description: "Names, roles and photos of your executive.", variants: { row: "Row" }, items: { word: "person", many: "People", label: (f) => f.name ?? "Person" } },
  faq: { name: "Questions and answers", description: "Questions that open to show answers.", variants: { list: "List" }, items: { word: "question", many: "Questions", label: (f) => f.q ?? "Question" } },
  cta: { name: "Call to action", description: "A banner that asks visitors to do one thing.", variants: { band: "Band" } },
  cards: { name: "Cards", description: "Cards with a title, text, photo and button. Use for programmes, benefits or steps.", variants: { photo: "With photos", plain: "Text only", numbered: "Numbered", steps: "Steps with pictures" }, items: { word: "card", many: "Cards", label: (f) => f.title ?? "Card" } },
  contact: { name: "Contact", description: "Address, email, phone and a photo.", variants: { split: "Details and photo" } },
  services: { name: "Services", description: "Services members can request or buy, filled in from your Services.", variants: { cards: "Cards", list: "List" }, live: "Services", feature: "Services", platformFeature: "Services", counted: true, allHref: "/services" },
  store: { name: "Store", description: "Products from your store, filled in for you.", variants: { grid: "Grid" }, live: "Store products", feature: "Store", platformFeature: "Store", counted: true, allHref: "/store" },
  fundraisers: { name: "Fundraisers", description: "Your fundraisers with how much has been raised.", variants: { cards: "Cards" }, live: "Fundraisers", feature: "Fundraisers", platformFeature: "Contributions", counted: true },
  jobs: { name: "Jobs", description: "Opportunities members have posted.", variants: { list: "List" }, live: "Job posts", feature: "Jobs", platformFeature: "Jobs", counted: true, allHref: "/jobs" },
  businesses: { name: "Member businesses", description: "Businesses run by your members.", variants: { cards: "Cards" }, live: "Business directory", feature: "Businesses", platformFeature: "BusinessDirectory", counted: true, allHref: "/business-directory" },
  spotlights: { name: "Member spotlights", description: "Members your community is proud of, filled in from your Spotlights.", variants: { cards: "Cards" }, live: "Spotlights", platformFeature: "Spotlights", counted: true, allHref: "/spotlights" },
  communities: { name: "Communities", description: "Your communities and how many people are in them.", variants: { cards: "Cards" }, live: "Communities", feature: "Communities", platformFeature: "Communities", counted: true, allHref: "/communities" },
};

export const SECTION_TYPES = Object.keys(BLOCKS) as SectionType[];

export const WEBSITE_FEATURES: { key: WebsiteFeature; label: string; description: string }[] = [
  { key: "Services", label: "Services", description: "Certificates, transcripts, ID cards and anything else members can request or pay for." },
  { key: "Store", label: "Store", description: "Merchandise members can buy." },
  { key: "Fundraisers", label: "Fundraisers", description: "Campaigns people can give to, with progress." },
  { key: "Jobs", label: "Jobs", description: "Opportunities members have posted." },
  { key: "Businesses", label: "Business directory", description: "Businesses run by your members." },
  { key: "Communities", label: "Communities", description: "Your sub-communities and how big they are." },
];

/** The menu name a new section gets, and whether it appears in the menu from the start. */
export const MENU_DEFAULT: Partial<Record<SectionType, string>> = {
  services: "Services", store: "Store", fundraisers: "Give", news: "News", events: "Events", contact: "Contact",
  jobs: "Jobs", businesses: "Businesses", communities: "Communities", spotlights: "Spotlight",
};

/** The member portal address for one item, or for the full list. The portal already shows these to visitors who are not signed in. */
export const portalPath = {
  news: (id: string) => `/news/${id}`,
  events: (id: string) => `/events/${id}`,
  services: (id: string) => `/services/${id}`,
  store: (id: string) => `/store/${id}`,
  fundraisers: (id: string) => `/fundraiser/${id}`,
  jobs: (id: string) => `/jobs/${id}`,
  businesses: (id: string) => `/business-directory/${id}`,
  communities: (id: string) => `/communities/${id}`,
  spotlights: "/spotlights",
  join: "/register",
  signin: "/login",
  privacy: "/privacy",
  terms: "/terms",
} as const;
