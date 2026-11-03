import { BLOCKS } from "./blocks";
import type { SiteData, SiteDocument, SiteSection } from "./types";

/** Does the live section have anything to show? A feature with nothing to show simply does not appear, so visitors never see an empty block. */
export function hasContent(sec: SiteSection, data: SiteData): boolean {
  switch (sec.type) {
    case "news": return (data.news?.length ?? 0) > 0;
    case "events": return true; // with nothing coming up the section says so and points to past events
    case "services": return (data.services?.length ?? 0) > 0;
    case "store": return (data.store?.length ?? 0) > 0;
    case "fundraisers": return (data.fundraisers?.length ?? 0) > 0;
    case "jobs": return (data.jobs?.length ?? 0) > 0;
    case "businesses": return (data.businesses?.length ?? 0) > 0;
    case "communities": return (data.communities?.length ?? 0) > 0;
    case "spotlights": return (data.spotlights?.length ?? 0) > 0;
    case "actions": case "membership": case "chapters": case "partners": case "leaders": case "gallery": return (sec.items?.length ?? 0) > 0;
    case "stats": { const s = data.stats; return !!s && [s.members, s.joinedLast30Days, s.eventsNext30Days, s.openJobs, s.businesses, s.communities].some((v) => (v ?? 0) > 0); }
    default: return true;
  }
}

/**
 * The sections a visitor sees. Hidden ones, ones whose feature the admin switched off for the website or the platform switched off for the
 * institution, and live ones with nothing to show are left out. The editor passes `editing` to see all of them (dimmed where hidden).
 */
export function visibleSections(doc: SiteDocument, data: SiteData, platformDisabled: string[] = [], editing = false): SiteSection[] {
  return doc.sections.filter((s) => {
    if (editing) return true;
    if (s.hidden) return false;
    const b = BLOCKS[s.type];
    if (b.feature && doc.features[b.feature] === false) return false;
    if (b.platformFeature && platformDisabled.includes(b.platformFeature)) return false;
    return hasContent(s, data);
  });
}

/** Why a section is not shown to visitors, in plain words, or null when it is shown. */
export function whyHidden(sec: SiteSection, doc: SiteDocument, data: SiteData, platformDisabled: string[]): string | null {
  const b = BLOCKS[sec.type];
  if (sec.hidden) return "Hidden from visitors";
  if (b.platformFeature && platformDisabled.includes(b.platformFeature)) return `${b.feature ?? b.name} is not available for your institution`;
  if (b.feature && doc.features[b.feature] === false) return `Hidden while ${b.feature} is switched off for the website`;
  if (b.live && !hasContent(sec, data)) return `Nothing to show yet, so visitors will not see this`;
  return null;
}
