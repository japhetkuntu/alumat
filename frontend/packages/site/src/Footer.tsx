import { portalPath } from "./blocks";
import type { SiteCtx } from "./context";
import { nameOf } from "./color";
import type { MenuItem } from "./Header";
import type { SiteSection } from "./types";

const SOCIAL: Record<string, string> = { facebook: "Facebook", instagram: "Instagram", linkedin: "LinkedIn", x: "X", youtube: "YouTube" };

interface FooterProps { ctx: SiteCtx; brandMark: React.ReactNode; logoUrl?: string | null; items: { sec: SiteSection; label: string }[]; featureOf: (s: SiteSection) => boolean }

/**
 * The footer is its own surface so the page visibly ends: the brand block, links grouped by purpose and built from the menu, contact details
 * as real links, and a legal row. Empty fields are left out.
 */
export function SiteFooter({ ctx, brandMark, logoUrl, items, featureOf }: FooterProps) {
  const F = ctx.doc.footer, name = nameOf(ctx.doc.design, ctx.brand);
  const explore = items.filter((i) => !featureOf(i.sec)), involved = items.filter((i) => featureOf(i.sec));
  const cols: { label: string; links: (MenuItem | { id: "top"; label: string })[] }[] = [];
  if (F.menu) {
    cols.push({ label: "Explore", links: [{ id: "top", label: "Home" }, ...explore.map((i) => ({ id: i.sec.id, label: i.label }))] });
    if (involved.length) cols.push({ label: "Get involved", links: involved.map((i) => ({ id: i.sec.id, label: i.label })) });
  }
  const hasContact = !!(F.email || F.phone || F.addr);
  const social = Object.entries(F.social ?? {}).filter(([k, v]) => v && SOCIAL[k]);
  const one = "minmax(0,1fr)";
  const wide = ["minmax(0,1.5fr)", ...cols.map(() => one), ...(hasContact ? ["minmax(0,1.5fr)"] : [])].join(" ");
  const mid = [...cols.map(() => one), ...(hasContact ? ["minmax(0,1.6fr)"] : [])].join(" ") || one;
  return (
    <footer className={`ft ${F.style ?? "dark"}`}>
      <div className="wrap">
        <div className="ftgrid" style={{ ["--ftw" as string]: wide, ["--ftm" as string]: mid }}>
          <div className="fbrand">
            {logoUrl ? <span className="flogo"><img src={logoUrl} alt={name} /></span> : <div className="logo">{brandMark}<span className="lname">{name}</span></div>}
            {F.text && <p>{F.text}</p>}
            <div className="fcta">
              <a className="btn" href={portalPath.join}>Join the community</a>
              <a className="btn ghost" href={portalPath.signin}>Member sign in</a>
            </div>
            {social.length > 0 && <div className="fsoc">{social.map(([k, v]) => <a key={k} href={v} target="_blank" rel="noopener noreferrer">{SOCIAL[k]}</a>)}</div>}
          </div>
          {cols.map((c) => (
            <div className="fcol" key={c.label}>
              <div className="fth">{c.label}</div>
              <nav aria-label={c.label}>{c.links.map((l) => <a key={l.id} href={l.id === "top" ? "#alsite-top" : `#sec-${l.id}`}>{l.label}</a>)}</nav>
            </div>
          ))}
          {hasContact && (
            <div className="fcol fcontact">
              <div className="fth">Contact</div>
              <address>
                {F.addr && <p>{F.addr}</p>}
                {F.email && <a href={`mailto:${F.email}`}>{F.email}</a>}
                {F.phone && <a href={`tel:${F.phone.replace(/[^\d+]/g, "")}`}>{F.phone}</a>}
              </address>
            </div>
          )}
        </div>
        <div className="legal">
          <span>© {new Date().getFullYear()} {name}. All rights reserved.</span>
          <nav aria-label="Legal"><a href={portalPath.privacy}>Privacy</a><a href={portalPath.terms}>Terms</a><a href="#alsite-top">Back to top</a></nav>
          {F.credit && <span className="credit">Powered by AlumUnion</span>}
        </div>
      </div>
    </footer>
  );
}
