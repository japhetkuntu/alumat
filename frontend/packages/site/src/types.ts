/**
 * The shape of an institution's website page. It mirrors the server's WebsiteDocument exactly (camelCase JSON): the server cleans every
 * page it stores, and this package renders it. A page is data, never code: no raw HTML, no scripts.
 */
export type LinkKind = "none" | "anchor" | "join" | "signin" | "external" | "email" | "phone" | "portal";
export interface SiteLink { t: LinkKind; v?: string | null }

export interface SiteMenuEntry { on: boolean; label: string }

export interface SiteItem {
  id: string;
  fields: Record<string, string>;
  links: Record<string, SiteLink>;
}

export type SectionType =
  | "hero" | "text" | "imagetext" | "news" | "events" | "stats" | "gallery" | "leaders" | "faq" | "cta" | "cards" | "contact"
  | "video" | "social" | "downloads" | "actions" | "membership" | "chapters" | "partners" | "services" | "store" | "fundraisers" | "jobs" | "businesses" | "communities" | "spotlights";

export type Background = "plain" | "tint" | "brand";
export type Spacing = "c" | "n" | "r";

export interface SiteSection {
  id: string;
  type: SectionType;
  variant: string;
  bg: Background;
  space: Spacing;
  hidden: boolean;
  count: number;
  menu: SiteMenuEntry;
  props: Record<string, string>;
  /** Photos by slot: "main", "bg", "map", "a"/"b"/"c", or "i{itemId}". Values are https addresses. */
  imgs: Record<string, string>;
  items?: SiteItem[];
  links: Record<string, SiteLink>;
}

export type FontKey = "vibrant" | "heritage" | "campus" | "journal" | "bold";

export interface SiteDesign {
  /** On = the website uses the institution's own colours and logo. Off changes the website only, never the portal. */
  useBrand: boolean;
  primary?: string | null;
  logoUrl?: string | null;
  name?: string | null;
  font: FontKey;
  corners: string;
  dark: boolean;
  vibe: boolean;
  headerStyle: "solid" | "overlay";
  sticky: boolean;
}

export type WebsiteFeature = "Services" | "Store" | "Fundraisers" | "Jobs" | "Businesses" | "Communities";

export interface SiteFooter {
  text?: string | null;
  email?: string | null;
  phone?: string | null;
  addr?: string | null;
  menu: boolean;
  style: "dark" | "brand" | "light";
  credit: boolean;
  social: Record<string, string>;
  hours?: string | null;
  links?: { label: string; url: string }[];
}

export interface SiteSeo { title?: string | null; description?: string | null; shareImage?: string | null }

export interface SiteDocument {
  schemaVersion: number;
  template: string;
  design: SiteDesign;
  sections: SiteSection[];
  footer: SiteFooter;
  seo: SiteSeo;
  features: Partial<Record<WebsiteFeature, boolean>>;
}

/** The institution's own branding, read from its portal theme. The website starts with it. */
export interface SiteBrand {
  name: string;
  tagline?: string | null;
  primary: string;
  secondary?: string | null;
  logoUrl?: string | null;
  slug?: string;
}

// ── What the live sections show ─────────────────────────────────────────────────────────────────────

export interface NewsItem { id: string; title: string; excerpt?: string | null; imageUrl?: string | null; publishedAt?: string | null; category?: string | null }
export interface EventItem { id: string; title: string; startDate: string; venue?: string | null; bannerImageUrl?: string | null }
export interface CommunityItem { id: string; name: string; memberCount?: number | null; description?: string | null; imageUrl?: string | null }
export interface BusinessItem { id: string; name: string; category?: string | null; imageUrl?: string | null }
export interface ServiceItem { id: string; name: string; description?: string | null; price: number; imageUrl?: string | null }
export interface ProductItem { id: string; name: string; price: number; imageUrl?: string | null; category?: string | null }
export interface FundraiserItem { id: string; title: string; summary?: string | null; raised: number; goal: number; imageUrl?: string | null }
export interface JobItem { id: string; title: string; company?: string | null; location?: string | null }
export interface SpotlightItem { id: string; title: string; story?: string | null; imageUrl?: string | null; memberName?: string | null }
export interface StatsData { members?: number | null; joinedLast30Days?: number | null; eventsNext30Days?: number | null; openJobs?: number | null; businesses?: number | null; communities?: number | null }

export interface SiteData {
  news?: NewsItem[];
  events?: EventItem[];
  communities?: CommunityItem[];
  businesses?: BusinessItem[];
  services?: ServiceItem[];
  store?: ProductItem[];
  fundraisers?: FundraiserItem[];
  jobs?: JobItem[];
  spotlights?: SpotlightItem[];
  stats?: StatsData;
}
