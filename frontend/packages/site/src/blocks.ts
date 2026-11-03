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
  actions: { name: "Quick actions", description: "A row of the things visitors come to do: join, pay dues, give, update their details.", variants: { bar: "Row" }, items: { word: "action", many: "Actions", label: (f) => f.label ?? "Action" } },
  membership: { name: "Membership types", description: "The kinds of membership you offer, what each costs and who it is for.", variants: { cards: "Cards" }, items: { word: "membership type", many: "Membership types", label: (f) => f.title ?? "Membership type" } },
  chapters: { name: "Chapters and branches", description: "Where your groups meet and who to contact, as a simple table.", variants: { table: "Table" }, items: { word: "chapter", many: "Chapters", label: (f) => f.name ?? "Chapter" } },
  partners: { name: "Partners and sponsors", description: "A row of logos for the organisations that support you.", variants: { strip: "Logo row" }, items: { word: "partner", many: "Partners", label: (f) => f.name ?? "Partner" } },
  hero: { name: "Hero", description: "The big opening of the page: a headline, a few words and buttons.", variants: { split: "Photo beside text", full: "Full-width photo", center: "Centred text", intro: "Plain opening, no photo" } },
  text: { name: "Text", description: "A heading and some paragraphs.", variants: { one: "One column", two: "Two columns" } },
  imagetext: { name: "Image and text", description: "A photo beside a heading and text.", variants: { left: "Image left", right: "Image right" } },
  news: { name: "Latest news", description: "Your latest news posts, filled in for you.", variants: { list: "List", grid: "Cards" }, live: "News posts", platformFeature: "News", counted: true, allHref: "/news" },
  events: { name: "Upcoming events", description: "Your upcoming events, filled in for you.", variants: { list: "List", cards: "Cards" }, live: "Events", platformFeature: "Events", counted: true, allHref: "/events" },
  stats: { name: "Community numbers", description: "Member and event numbers, filled in for you.", variants: { row: "Row" }, live: "Member and event counts" },
  gallery: { name: "Photo gallery", description: "A grid of photos you add.", variants: { grid: "Even grid", mosaic: "Mosaic" }, items: { word: "photo", many: "Photos", label: () => "Photo" } },
  leaders: { name: "Leaders", description: "Names, roles and photos of your executive and council, grouped by tier.", variants: { row: "Row" }, items: { word: "person", many: "People", label: (f) => f.name ?? "Person" } },
  faq: { name: "Questions and answers", description: "Questions that open to show answers.", variants: { list: "List" }, items: { word: "question", many: "Questions", label: (f) => f.q ?? "Question" } },
  cta: { name: "Call to action", description: "A banner that asks visitors to do one thing.", variants: { band: "Band" } },
  cards: { name: "Cards", description: "Cards with a title, text, photo and button. Use for programmes, benefits or steps.", variants: { photo: "With photos", plain: "Text only", numbered: "Numbered", steps: "Steps with pictures" }, items: { word: "card", many: "Cards", label: (f) => f.title ?? "Card" } },
  contact: { name: "Contact", description: "Address, email, phone and a photo.", variants: { split: "Details and photo" } },
  services: { name: "Services", description: "Services members can request or buy, filled in from your Services.", variants: { cards: "Cards", list: "List" }, live: "Services", feature: "Services", platformFeature: "Services", counted: true, allHref: "/services" },
  store: { name: "Store", description: "Products from your store, filled in for you.", variants: { grid: "Grid" }, live: "Store products", feature: "Store", platformFeature: "Store", counted: true, allHref: "/store" },
  fundraisers: { name: "Fundraisers", description: "Your fundraisers with how much has been raised.", variants: { cards: "Cards" }, live: "Fundraisers", feature: "Fundraisers", platformFeature: "Contributions", counted: true },
  jobs: { name: "Jobs", description: "Opportunities members have posted.", variants: { list: "List" }, live: "Job posts", feature: "Jobs", platformFeature: "Jobs", counted: true, allHref: "/jobs" },
  businesses: { name: "Member businesses", description: "Businesses run by your members.", variants: { cards: "Cards" }, live: "Member businesses", feature: "Businesses", platformFeature: "BusinessDirectory", counted: true, allHref: "/business-directory" },
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
  jobs: "Jobs", businesses: "Businesses", communities: "Communities", spotlights: "Spotlight", membership: "Membership", chapters: "Chapters", leaders: "Leaders",
};

/** The member portal pages a button can open, by name. Keep in step with PortalPages on the server. */
export const PORTAL_PAGES: Record<string, { label: string; path: string }> = {
  contributions: { label: "Pay dues or give", path: "/contributions" },
  profile: { label: "Update my details", path: "/profile" },
  directory: { label: "Alumni directory", path: "/directory" },
  events: { label: "All events", path: "/events" },
  calendar: { label: "Calendar", path: "/calendar" },
  news: { label: "All news", path: "/news" },
  jobs: { label: "Jobs", path: "/jobs" },
  store: { label: "Store", path: "/store" },
  services: { label: "Services", path: "/services" },
  albums: { label: "Photo albums", path: "/albums" },
  resources: { label: "Resources", path: "/resources" },
  mentorship: { label: "Mentorship", path: "/mentorship" },
  polls: { label: "Polls", path: "/polls" },
  communities: { label: "Communities", path: "/communities" },
  "business-directory": { label: "Business directory", path: "/business-directory" },
  spotlights: { label: "Spotlights", path: "/spotlights" },
  forum: { label: "Forum", path: "/forum" },
};

/** The member portal address for one item, or for the full list. The portal already shows these to visitors who are not signed in. */
export const portalPath = {
  // These open the website's own reader page, which shows the whole item to anyone, with a way to join or sign in at the end.
  news: (id: string) => `/read/news/${id}`,
  events: (id: string) => `/read/event/${id}`,
  services: (id: string) => `/services/${id}`,
  store: (id: string) => `/store/${id}`,
  fundraisers: (id: string) => `/fundraiser/${id}`,
  jobs: (id: string) => `/read/job/${id}`,
  businesses: (id: string) => `/business-directory/${id}`,
  communities: (id: string) => `/read/community/${id}`,
  spotlights: (id: string) => `/read/spotlight/${id}`,
  join: "/register",
  signin: "/login",
  privacy: "/privacy",
  terms: "/terms",
} as const;
