/**
 * Derives an email-safe, UI-safe color palette from a single brand hex
 * using OKLCH (a perceptually-uniform color space) rather than naive
 * RGB/HSL mixing. This is a direct TypeScript mirror of the C# version in
 * `backend/src/ReservEase.Alumni.Mailtrap.Sdk/Services/EmailColorPalette.cs`
 * — same formulas, same clamp bounds — so a color looks the same whether
 * it's rendered here (live UI theming, onboarding preview) or in an email.
 *
 * Plain RGB interpolation makes "lighten by X%" look wildly different
 * across hues (yellow blows out, blue barely moves); OKLCH keeps perceived
 * brightness consistent for any input hue. Two guardrails sit on top of
 * the math, because "technically derived" isn't the same as "looks good":
 *  - `clampSeed`: nudges a seed that's too pale/dark/gray into a workable
 *    band before anything is derived from it.
 *  - `textOn`: picks white or near-black text by computing actual WCAG
 *    contrast against the given background, not by assuming white always works.
 */

// Clamp bounds tuned for "looks like a real UI accent color, not a pastel
// wash or a near-black smudge" — same reasoning a hand-picked brand palette
// would apply. Keep in sync with EmailColorPalette.cs.
const MIN_LIGHTNESS = 0.38;
const MAX_LIGHTNESS = 0.62;
const MIN_CHROMA = 0.06;

// ── OKLCH conversion (Björn Ottosson's OKLab: https://bottosson.github.io/posts/oklab/) ──

function srgbToLinear(c: number): number {
  return c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
}

function linearToSrgb(c: number): number {
  const clamped = Math.min(1, Math.max(0, c));
  return clamped <= 0.0031308 ? clamped * 12.92 : 1.055 * Math.pow(clamped, 1 / 2.4) - 0.055;
}

function cbrt(x: number): number {
  return Math.sign(x) * Math.pow(Math.abs(x), 1 / 3);
}

function parseHex(hex: string): [number, number, number] {
  let h = hex.trim().replace("#", "");
  if (h.length === 3) h = h.split("").map((c) => c + c).join("");
  if (!/^[0-9a-f]{6}$/i.test(h)) return [14, 113, 67]; // fallback: platform default green
  return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)];
}

function toHex(r: number, g: number, b: number): string {
  const byte = (c: number) => Math.min(255, Math.max(0, Math.round(c))).toString(16).padStart(2, "0");
  return `#${byte(r)}${byte(g)}${byte(b)}`;
}

function linearSrgbToOklab(r: number, g: number, b: number): [number, number, number] {
  const l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b;
  const m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b;
  const s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b;

  const l_ = cbrt(l);
  const m_ = cbrt(m);
  const s_ = cbrt(s);

  return [
    0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,
    1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,
    0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_,
  ];
}

function oklabToLinearSrgb(L: number, a: number, b: number): [number, number, number] {
  const l_ = L + 0.3963377774 * a + 0.2158037573 * b;
  const m_ = L - 0.1055613458 * a - 0.0638541728 * b;
  const s_ = L - 0.0894841775 * a - 1.2914855480 * b;

  const l = l_ * l_ * l_;
  const m = m_ * m_ * m_;
  const s = s_ * s_ * s_;

  return [
    +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
    -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
    -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s,
  ];
}

interface Oklch {
  l: number;
  c: number;
  h: number;
}

function toOklch(hex: string): Oklch {
  const [r, g, b] = parseHex(hex);
  const [L, a, bb] = linearSrgbToOklab(srgbToLinear(r / 255), srgbToLinear(g / 255), srgbToLinear(b / 255));
  return { l: L, c: Math.sqrt(a * a + bb * bb), h: Math.atan2(bb, a) };
}

function fromOklch(l: number, c: number, h: number): string {
  const a = c * Math.cos(h);
  const bb = c * Math.sin(h);
  const [r, g, b] = oklabToLinearSrgb(l, a, bb);
  return toHex(linearToSrgb(r) * 255, linearToSrgb(g) * 255, linearToSrgb(b) * 255);
}

/** Hue (radians) of the platform's own default green (#0e7143) — used when a seed has no real hue to preserve. */
const DEFAULT_HUE = toOklch("#0e7143").h;

/** Softly clamps a seed color into a workable saturation/lightness band before any derivation happens. */
export function clampSeed(hex: string): string {
  const { l, c, h } = toOklch(hex);

  // Hue is meaningless (and numerically noisy — atan2 near the origin) once
  // chroma is this low: white, black, and every shade of gray all have "no
  // real color", so deriving one from floating-point noise produces an
  // arbitrary muddy tint instead of admitting there's no brand hue to work
  // with. Fall back to the platform's own hue.
  const hue = c < 0.02 ? DEFAULT_HUE : h;

  const clampedL = Math.min(MAX_LIGHTNESS, Math.max(MIN_LIGHTNESS, l));
  const clampedC = Math.max(c, MIN_CHROMA);
  return fromOklch(clampedL, clampedC, hue);
}

function withLightness(hex: string, adjust: (l: number) => number, chromaScale = 1): string {
  const { l, c, h } = toOklch(clampSeed(hex));
  return fromOklch(adjust(l), c * chromaScale, h);
}

export function darkShade(hex: string): string {
  return withLightness(hex, (l) => Math.max(0, l - 0.16));
}

export function lightShade(hex: string): string {
  return withLightness(hex, () => 0.94, 0.35);
}

export function softShade(hex: string): string {
  return withLightness(hex, () => 0.86, 0.55);
}

function relativeLuminance([r, g, b]: [number, number, number]): number {
  const channel = (v: number) => {
    const s = v / 255;
    return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
}

function contrastRatio(a: [number, number, number], b: [number, number, number]): number {
  const l1 = relativeLuminance(a);
  const l2 = relativeLuminance(b);
  const [lighter, darker] = l1 > l2 ? [l1, l2] : [l2, l1];
  return (lighter + 0.05) / (darker + 0.05);
}

/** White or near-black — whichever has sufficient WCAG contrast against the given background. */
export function textOn(backgroundHex: string): string {
  const rgb = parseHex(backgroundHex);
  const whiteContrast = contrastRatio(rgb, [255, 255, 255]);
  const blackContrast = contrastRatio(rgb, [17, 24, 39]); // slate-900
  return whiteContrast >= blackContrast ? "#ffffff" : "#111827";
}

export interface BrandPalette {
  primary: string;
  primaryDark: string;
  primaryLight: string;
  primarySoft: string;
  textOnPrimary: string;
  /** rgba(...) string at the given alpha — for ring/border tokens that use a translucent brand tint. */
  ring: (alpha: number) => string;
}

/** Generates the full derived palette for a single seed color, ready to plug into CSS custom properties. */
export function generateBrandPalette(seedHex: string): BrandPalette {
  const primary = clampSeed(seedHex);
  const [r, g, b] = parseHex(primary);
  return {
    primary,
    primaryDark: darkShade(primary),
    primaryLight: lightShade(primary),
    primarySoft: softShade(primary),
    textOnPrimary: textOn(primary),
    ring: (alpha: number) => `rgba(${r}, ${g}, ${b}, ${alpha})`,
  };
}
