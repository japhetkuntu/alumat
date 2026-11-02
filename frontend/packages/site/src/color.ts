import type { SiteBrand, SiteDesign } from "./types";

const hex = (h: string) => { const n = parseInt(h.slice(1), 16); return [n >> 16, (n >> 8) & 255, n & 255]; };

/** Text colour that stays readable on top of a colour. */
export function onColor(h: string): "#161513" | "#ffffff" {
  const [r, g, b] = hex(h);
  const f = (v: number) => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); };
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b) > 0.4 ? "#161513" : "#ffffff";
}

const HEX = /^#[0-9a-f]{6}$/i;
export const isHex = (s?: string | null): s is string => !!s && HEX.test(s);

/** The colour the website uses: the institution's own unless the admin chose another for the website only. */
export function primaryOf(design: SiteDesign, brand: SiteBrand): string {
  if (!design.useBrand && isHex(design.primary)) return design.primary;
  return isHex(brand.primary) ? brand.primary : "#1f4d3a";
}

export function nameOf(design: SiteDesign, brand: SiteBrand): string {
  return (design.name && design.name.trim()) || brand.name;
}

export function logoOf(design: SiteDesign, brand: SiteBrand): string | null {
  if (!design.useBrand && design.logoUrl) return design.logoUrl;
  return design.useBrand ? brand.logoUrl ?? null : null;
}

export const FONT_STACKS: Record<string, { display: string; body: string; weight: number }> = {
  vibrant: { display: "var(--site-sora), 'Sora', system-ui, sans-serif", body: "var(--site-sora), 'Sora', system-ui, sans-serif", weight: 600 },
  heritage: { display: "var(--font-sans), 'IBM Plex Sans', system-ui, sans-serif", body: "var(--font-sans), 'IBM Plex Sans', system-ui, sans-serif", weight: 600 },
  campus: { display: "var(--font-sans), 'IBM Plex Sans', system-ui, sans-serif", body: "var(--font-sans), 'IBM Plex Sans', system-ui, sans-serif", weight: 700 },
  journal: { display: "Georgia, 'Times New Roman', serif", body: "var(--font-sans), 'IBM Plex Sans', system-ui, sans-serif", weight: 500 },
  bold: { display: "var(--font-sans), 'IBM Plex Sans', system-ui, sans-serif", body: "var(--font-sans), 'IBM Plex Sans', system-ui, sans-serif", weight: 800 },
};

/** The CSS variables a website is drawn with. Everything on the page is driven by these, so a colour or look change is one place. */
export function tokens(design: SiteDesign, brand: SiteBrand): Record<string, string> {
  const p = primaryOf(design, brand), f = FONT_STACKS[design.font] ?? FONT_STACKS.vibrant;
  const dark = design.dark;
  const bg = dark ? "#12161c" : "#fbfaf6", ink = dark ? "#f2efe8" : "#1d211e", mut = dark ? "#a9a49b" : "#5d655f", line = dark ? "#2a313b" : "#dcd9cf";
  const warm = onColor(p) === "#ffffff";
  return {
    "--card": dark ? "#1a212b" : "#ffffff",
    "--acc": warm ? "#ffd98a" : "#7a3d00",
    "--p": p, "--on-p": onColor(p), "--r": design.corners,
    "--fd": f.display, "--fb": f.body, "--fw": String(f.weight),
    "--bg": bg, "--ink": ink, "--mut": mut, "--line": line,
    "--tint": `color-mix(in srgb, ${p} ${dark ? 7 : 8}%, ${bg})`,
  };
}

export const initials = (n?: string | null) => String(n ?? "").trim().split(/\s+/).slice(0, 2).map((w) => w[0] ?? "").join("").toUpperCase();
