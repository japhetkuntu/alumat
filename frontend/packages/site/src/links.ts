import { PORTAL_PAGES, portalPath } from "./blocks";
import type { SiteLink, SiteSection } from "./types";

/** Whether a button points somewhere real. An empty address or a section that was removed counts as nowhere. */
export function linkOk(l?: SiteLink | null): boolean {
  if (!l || l.t === "none") return false;
  if (l.t === "join" || l.t === "signin") return true;
  return !!l.v;
}

/** The address a button opens. Sections on this page are reached with a #, everything else is a portal page or an outside address. */
export function linkHref(l: SiteLink | undefined | null, sections: SiteSection[]): string | undefined {
  if (!l || !linkOk(l)) return undefined;
  switch (l.t) {
    case "anchor": return sections.some((s) => s.id === l.v) ? `#sec-${l.v}` : undefined;
    case "join": return portalPath.join;
    case "signin": return portalPath.signin;
    case "portal": return l.v ? PORTAL_PAGES[l.v]?.path : undefined;
    case "external": return l.v ?? undefined;
    case "email": return `mailto:${l.v}`;
    case "phone": return `tel:${(l.v ?? "").replace(/[^\d+]/g, "")}`;
    default: return undefined;
  }
}

export const isExternal = (l?: SiteLink | null) => l?.t === "external";

export function linkLabel(l: SiteLink | undefined, sections: SiteSection[], nameOf: (s: SiteSection) => string): string {
  switch (l?.t) {
    case "anchor": { const s = sections.find((x) => x.id === l.v); return s ? `Section: ${nameOf(s)}` : "Section (missing)"; }
    case "join": return "Join page";
    case "signin": return "Member sign in";
    case "portal": return l.v && PORTAL_PAGES[l.v] ? PORTAL_PAGES[l.v].label : "Portal page (not chosen)";
    case "external": return l.v || "Website (empty)";
    case "email": return l.v ? `Email ${l.v}` : "Email (empty)";
    case "phone": return l.v ? `Call ${l.v}` : "Phone (empty)";
    default: return "No link yet";
  }
}
