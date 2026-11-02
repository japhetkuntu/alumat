import type { EditApi } from "./edit";
import type { SiteBrand, SiteData, SiteDocument, SiteSection } from "./types";

/** Everything a section needs to draw itself. */
export interface SiteCtx {
  doc: SiteDocument;
  brand: SiteBrand;
  data: SiteData;
  /** Sections that are on the page for visitors, in order. */
  sections: SiteSection[];
  edit?: EditApi;
  /** True when the live sections are showing made-up sample items (inside the editor). */
  sample?: boolean;
  currency?: string;
}
