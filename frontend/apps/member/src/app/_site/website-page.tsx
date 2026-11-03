import "@alumni/site/styles.css";
import { SiteView } from "@alumni/site";
import type { SiteBrand, SiteData, SiteDocument } from "@alumni/site";
import { sora } from "./site-font";

/** The institution's own website, as published from the builder. Server-rendered so the whole page, photos included, arrives ready to read. */
export function WebsitePage({ doc, brand, data, platformDisabled, reader }: { doc: SiteDocument; brand: SiteBrand; data: SiteData; platformDisabled: string[]; reader?: React.ReactNode }) {
  return (
    <div className={sora.variable}>
      <SiteView doc={doc} brand={brand} data={data} platformDisabled={platformDisabled} reader={reader} />
    </div>
  );
}
