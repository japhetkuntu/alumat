import { cookies, headers } from "next/headers";
import { generateBrandPalette } from "@alumni/ui";

export interface InstitutionTheme {
  slug: string;
  portalName: string;
  displayName: string;
  primaryColorHex: string;
  secondaryColorHex: string | null;
  logoUrl: string | null;
  tagline: string | null;
  iconUrl: string | null;
  portalTitle: string | null;
  authHeadline: string | null;
  authSubtext: string | null;
  requireStudentId: boolean;
}

// Server-side only (never sent to the browser, unlike api-client.ts's
// same-origin relative URL): this fetch runs inside the Next.js server
// process, which has no implicit "current origin" to resolve a relative URL
// against, so it needs a real address for member-api. Inside Docker Compose
// that's the internal service DNS name; outside Docker (local `pnpm dev`)
// it's the API's local dev port. Either way we still forward the browser's
// original Host header below, so TenantResolutionMiddleware resolves the
// correct tenant regardless of which URL was used to reach the service.
const API_URL = process.env.MEMBER_API_INTERNAL_URL || "http://localhost:5200/api/v1";

export type TenantThemeStatus = "ok" | "not-found" | "error";

// Shared by getInstitutionTheme() (used throughout <html>/metadata plumbing,
// which only ever wants "give me a theme or null") and
// getInstitutionThemeStatus() (used once, by the root page, to tell "no
// institution resolved for this host" apart from a transient backend error —
// only the former should ever render the platform marketing site; the latter
// must keep degrading to the existing generic-fallback landing page, exactly
// as it already does today, so an outage never misclassifies a real
// institution visitor as a marketing-site visitor).
async function fetchTheme(): Promise<{ status: TenantThemeStatus; theme: InstitutionTheme | null }> {
  try {
    const requestHeaders = await headers();
    const host = requestHeaders.get("host") ?? "";
    // Dev-only tenant override — see TenantResolutionMiddleware. This SSR fetch
    // has no access to localStorage (that's client-only), so the workspace
    // slug is read from a cookie instead, mirrored there by the login/register
    // pages. Ignored by the backend outside Development.
    const workspaceSlug = (await cookies()).get("institution_slug")?.value;
    const fetchHeaders: Record<string, string> = {};
    // Node's fetch() silently ignores a caller-supplied "Host" header (always
    // uses the request URL's own host instead), so the browser's real
    // subdomain has to be forwarded under a different name — see
    // TenantResolutionMiddleware's X-Internal-Tenant-Host handling, which
    // only trusts this from loopback-originated requests (this one always is).
    if (host) fetchHeaders["X-Internal-Tenant-Host"] = host;
    if (workspaceSlug) fetchHeaders["X-Institution-Slug"] = workspaceSlug;
    const res = await fetch(`${API_URL}/public/institution/theme`, {
      headers: Object.keys(fetchHeaders).length ? fetchHeaders : undefined,
      cache: "no-store",
    });
    if (res.status === 404) {
      return { status: "not-found", theme: null };
    }
    if (!res.ok) {
      console.error(`[theme] ${API_URL}/public/institution/theme -> ${res.status} for Host="${host}"`);
      return { status: "error", theme: null };
    }
    const body = await res.json();
    return { status: "ok", theme: body?.data ?? null };
  } catch (err) {
    console.error(`[theme] fetch to ${API_URL}/public/institution/theme failed:`, err);
    return { status: "error", theme: null };
  }
}

export async function getInstitutionTheme(): Promise<InstitutionTheme | null> {
  return (await fetchTheme()).theme;
}

/** Distinguishes "no institution resolved for this host" (404 — the bare/main
 * domain) from a transient error, so the root page can route between the
 * platform marketing site and the normal institution landing page. */
export async function getInstitutionThemeStatus() {
  return fetchTheme();
}

// In :root (light mode), --primary/--success/--info/--chart-1/--sidebar-primary/
// --accent-emerald all reference var(--brand-primary), so overriding that one
// custom property alone used to update those automatically. But .dark
// redefines every one of those same names with hardcoded green oklch
// literals of its own — a fixed color completely independent of
// --brand-primary — so dark mode never reflected the institution's color at
// all, light or dark. Several light-mode tokens are also literal hex/rgba
// values, not var() references — --brand-primary-light/-dark,
// --color-background-info, --color-text-info, --color-border-info, --border,
// --ring, --sidebar-ring. Deriving the full palette from one seed color (see
// generateBrandPalette) and overriding every one of those — plus the tokens
// .dark breaks the var() chain for — as an inline style keeps the whole UI
// internally consistent in both light and dark mode, since an inline style
// always wins over any class selector's declaration for the same property.
export function themeStyleVars(theme: InstitutionTheme | null): Record<string, string> {
  if (!theme?.primaryColorHex) return {};
  const palette = generateBrandPalette(theme.primaryColorHex, theme.secondaryColorHex ?? undefined);
  const vars: Record<string, string> = {
    "--brand-primary": palette.primary,
    "--brand-primary-mid": palette.primary,
    "--brand-primary-light": palette.primaryLight,
    "--brand-primary-dark": palette.primaryDark,
    "--color-background-info": palette.primaryLight,
    "--color-text-info": palette.primaryDark,
    "--color-border-info": palette.primary,
    "--border": palette.ring(0.15),
    "--ring": palette.ring(0.4),
    "--sidebar-ring": palette.ring(0.25),
    // Overrides what .dark otherwise hardcodes to a fixed green, independent
    // of --brand-primary (--accent/--accent-foreground/--sidebar-accent* are
    // the separate gold accent system — overridden below only when the
    // institution has a real secondary color; otherwise left at their
    // existing generic-gold default, unchanged from before).
    "--primary": palette.primary,
    "--primary-foreground": palette.textOnPrimary,
    "--success": palette.primary,
    "--success-foreground": palette.textOnPrimary,
    "--info": palette.primary,
    "--info-foreground": palette.textOnPrimary,
    "--accent-emerald": palette.primary,
    "--chart-1": palette.primary,
    "--sidebar-primary": palette.primary,
    "--sidebar-primary-foreground": palette.textOnPrimary,
  };

  if (palette.accent) {
    vars["--brand-accent"] = palette.accent.color;
    vars["--brand-accent-mid"] = palette.accent.color;
    vars["--brand-accent-light"] = palette.accent.light;
    vars["--brand-accent-dark"] = palette.accent.dark;
    vars["--accent"] = palette.accent.color;
    vars["--accent-foreground"] = palette.accent.textOnAccent;
    vars["--accent-gold"] = palette.accent.color;
    vars["--warning"] = palette.accent.color;
    vars["--warning-foreground"] = palette.accent.textOnAccent;
    vars["--chart-2"] = palette.accent.color;
    vars["--sidebar-accent"] = palette.accent.light;
    vars["--sidebar-accent-foreground"] = palette.accent.dark;
  }

  return vars;
}
