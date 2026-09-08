"use client";

import { useCallback, useEffect, useMemo, useRef, useState, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { Loader2, XCircle, ArrowLeft, generateBrandPalette } from "@alumni/ui";

const HEX_COLOR = /^#[0-9a-fA-F]{6}$/;

const GOOGLE_CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID;

declare global {
  interface Window {
    google?: {
      accounts: {
        id: {
          initialize: (config: { client_id: string; callback: (resp: { credential: string }) => void }) => void;
          renderButton: (parent: HTMLElement, options: Record<string, unknown>) => void;
        };
      };
    };
  }
}

const PORTAL_LABEL: Record<string, string> = {
  member: "member portal",
  institution: "institution portal",
  platform: "platform portal",
};

/** Decodes (never verifies — that only ever happens server-side) a Google ID
 *  token's payload, purely so the register form can prefill name/email
 *  before the token is actually submitted and checked. */
function decodeJwtPayload(idToken: string): { email?: string; given_name?: string; family_name?: string } {
  try {
    const base64 = idToken.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
    return JSON.parse(decodeURIComponent(escape(atob(base64))));
  } catch {
    return {};
  }
}

/** Bare hostname to reassure a visitor where they're about to land — this
 *  page's whole job is a cross-domain redirect, which is exactly the shape
 *  of a phishing page, so showing the real destination in plain text is a
 *  functional trust signal, not decoration. */
function hostnameOf(url: string | null): string | null {
  if (!url) return null;
  try {
    return new URL(url).hostname;
  } catch {
    return null;
  }
}

/**
 * Shared Google sign-in bridge for all three portals. Google's OAuth client
 * only allows a fixed, pre-registered set of origins — it can't authorize
 * every institution's own subdomain (there can be any number of them, and
 * they're created on the fly) — so every portal's "Continue with Google"
 * button lands here instead of running the Google flow on its own tenant
 * subdomain. This page runs the actual Google sign-in against the one origin
 * Google knows about, exchanges the resulting ID token with the tenant's own
 * API (same-origin to `return`, so tenant resolution by Host header still
 * works correctly), then hands the session back to `return` via a URL
 * fragment — never sent to any server, only ever read by that page's own JS.
 *
 * This page itself has no institution context (it lives on the fixed base
 * domain, not any tenant's subdomain), so — unlike the portals' own login
 * pages — its branding panel is the platform's own identity, not any one
 * institution's. Same split-screen shell as (auth)/layout.tsx for visual
 * continuity, just with generic copy instead of a fetched tenant theme.
 */
function GoogleAuthBridgeContent() {
  const searchParams = useSearchParams();
  const returnUrl = searchParams.get("return");
  const portal = searchParams.get("portal") ?? "member";
  // "register" never completes anything itself — the register page still
  // needs alumni-specific fields (phone, student ID, graduation year) Google
  // has no idea about, so this mode just hands the raw ID token back for the
  // register page's own form submission to include. Real verification of
  // that token only ever happens server-side, same as the login mode below.
  const mode = searchParams.get("mode") === "register" ? "register" : "login";
  const buttonRef = useRef<HTMLDivElement>(null);
  const [status, setStatus] = useState<"loading" | "ready" | "signing-in" | "error">("loading");
  const [error, setError] = useState("");
  const destination = useMemo(() => hostnameOf(returnUrl), [returnUrl]);

  // Whichever institution sent the visitor here (if any — this page itself
  // has no tenant context of its own) forwards its own branding as URL
  // params, so this page can render in that institution's actual colors
  // instead of the generic platform default. Validated before use since
  // it's untrusted URL input landing straight in an inline style.
  const brandName = searchParams.get("name") || "AlumUnion";
  const brandLogo = searchParams.get("logo") || "/alumunion-mark.svg";
  const palette = useMemo(() => {
    const primary = searchParams.get("primary");
    if (!primary || !HEX_COLOR.test(primary)) return null;
    const secondary = searchParams.get("secondary");
    return generateBrandPalette(primary, secondary && HEX_COLOR.test(secondary) ? secondary : undefined);
  }, [searchParams]);
  const accentColor = palette?.primary ?? "var(--primary)";
  const panelColor = palette?.primaryDark ?? "var(--brand-primary-dark, var(--primary))";
  // Wherever they came from, changing their mind shouldn't be a dead end —
  // send them back to that same portal's own login/register page rather
  // than stranding them here with only the Google button as a way forward.
  const cancelHref = returnUrl ? `${returnUrl}/${mode === "register" ? "register" : "login"}` : null;

  const handleCredential = useCallback(async (idToken: string) => {
    if (!returnUrl) {
      setStatus("error");
      setError("Missing return destination — please start sign-in from the portal again.");
      return;
    }

    if (mode === "register") {
      const claims = decodeJwtPayload(idToken);
      const payload = encodeURIComponent(btoa(JSON.stringify({
        idToken, email: claims.email ?? "", firstName: claims.given_name ?? "", lastName: claims.family_name ?? "",
      })));
      window.location.href = `${returnUrl}/register#googleReg=${payload}`;
      return;
    }

    setStatus("signing-in");
    try {
      const res = await fetch(`${returnUrl}/api/v1/auth/google`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ idToken }),
      });
      const body = await res.json();
      if (!res.ok || !body?.data?.user || !body?.data?.tokens) {
        setStatus("error");
        setError(body?.message || "Google sign-in failed. Please try again.");
        return;
      }
      const payload = encodeURIComponent(btoa(JSON.stringify({ user: body.data.user, tokens: body.data.tokens })));
      window.location.href = `${returnUrl}/login#auth=${payload}`;
    } catch {
      setStatus("error");
      setError("Could not reach the server. Please try again.");
    }
  }, [returnUrl, mode]);

  useEffect(() => {
    if (!GOOGLE_CLIENT_ID) {
      setStatus("error");
      setError("Google sign-in is not configured yet.");
      return;
    }
    if (!returnUrl) {
      setStatus("error");
      setError("Missing return destination — please start sign-in from the portal again.");
      return;
    }

    const script = document.createElement("script");
    script.src = "https://accounts.google.com/gsi/client";
    script.async = true;
    script.onload = () => {
      if (!window.google || !buttonRef.current) return;
      window.google.accounts.id.initialize({
        client_id: GOOGLE_CLIENT_ID,
        callback: (resp) => { void handleCredential(resp.credential); },
      });
      window.google.accounts.id.renderButton(buttonRef.current, {
        type: "standard", theme: "filled_black", size: "large", shape: "pill", width: 320, logo_alignment: "left",
      });
      setStatus("ready");
    };
    script.onerror = () => {
      setStatus("error");
      setError("Could not load Google sign-in. Please check your connection and try again.");
    };
    document.body.appendChild(script);
    return () => { document.body.removeChild(script); };
  }, [returnUrl, handleCredential]);

  return (
    <div className="flex min-h-screen bg-background">

      {/* ── Left — branding panel: the originating institution's own colors when known, the platform's own identity otherwise ── */}
      <aside
        className="hidden md:flex md:w-[42%] lg:w-[46%] relative flex-col justify-center overflow-y-auto px-10 lg:px-14 py-10"
        style={{ background: panelColor }}
      >
        <div
          className="pointer-events-none absolute inset-0 opacity-[0.12]"
          style={{ backgroundImage: "radial-gradient(rgba(255,255,255,0.7) 1px, transparent 1px)", backgroundSize: "26px 26px" }}
        />

        <div className="relative space-y-7 w-full">
          <div className="flex items-center gap-3 w-fit">
            <img src={brandLogo} alt={brandName} className="h-9 w-9 rounded-lg object-cover" />
            <span className="text-[15px] font-semibold tracking-wide uppercase" style={{ color: "rgba(255,255,255,0.75)" }}>
              {brandName}
            </span>
          </div>

          <div className="max-w-[92%] xl:max-w-[480px] space-y-5">
            <h1
              className="font-[family-name:var(--font-display)] leading-[1.12]"
              style={{ fontSize: "clamp(2.25rem, 3.6vw, 3.1rem)", fontWeight: 700, letterSpacing: "-0.02em", color: "#fff" }}
            >
              One sign-in for every alumni network.
            </h1>
            <p className="text-[18px] font-medium leading-relaxed max-w-[440px]" style={{ color: "rgba(255,255,255,0.7)" }}>
              Just a moment — you'll be back in your portal, signed in, in a few seconds.
            </p>
          </div>

          <div className="border-t pt-6" style={{ borderColor: "rgba(255,255,255,0.15)" }}>
            <p className="text-[13px] font-medium" style={{ color: "rgba(255,255,255,0.5)" }}>
              Continuing to the {PORTAL_LABEL[portal] ?? "portal"}{destination ? ` · ${destination}` : ""}
            </p>
          </div>
        </div>
      </aside>

      {/* ── Right — the actual bridge interaction ── */}
      <main className="flex-1 flex items-center justify-center p-6 sm:p-10 md:p-14">
        <div className="w-full max-w-[400px] animate-in fade-in slide-in-from-bottom-6 duration-700">

          {/* Mobile-only compact brand mark — the aside above is hidden below md */}
          <div className="mb-10 text-center md:hidden">
            <img src={brandLogo} alt={brandName} className="w-14 h-14 rounded-2xl overflow-hidden mx-auto mb-5 shadow-sm border border-border object-cover" />
            <h2 className="font-[family-name:var(--font-display)] text-2xl font-semibold tracking-tight text-foreground">{brandName}</h2>
          </div>

          {cancelHref ? (
            <a
              href={cancelHref}
              className="inline-flex items-center gap-1.5 text-[13px] font-medium text-muted-foreground hover:text-foreground transition-colors mb-7"
            >
              <ArrowLeft size={14} /> Cancel and go back
            </a>
          ) : (
            <button
              type="button"
              onClick={() => window.history.back()}
              className="inline-flex items-center gap-1.5 text-[13px] font-medium text-muted-foreground hover:text-foreground transition-colors mb-7"
            >
              <ArrowLeft size={14} /> Go back
            </button>
          )}

          <div className="space-y-2.5 mb-9">
            <div className="text-sm font-semibold tracking-widest uppercase" style={{ color: accentColor }}>
              {PORTAL_LABEL[portal] ?? "portal"}
            </div>
            <h1 className="font-[family-name:var(--font-display)] text-[28px] sm:text-[32px] font-semibold text-foreground leading-tight">
              {mode === "register" ? "Sign up with Google" : "Sign in with Google"}
            </h1>
            <p className="text-sm text-muted-foreground">
              {mode === "register" ? "We'll bring your name and email back to finish registering." : "You'll be sent straight back in, signed in."}
            </p>
          </div>

          <div className="min-h-[64px] flex items-center">
            {(status === "loading" || status === "signing-in") && (
              <div className="flex items-center gap-3 text-muted-foreground">
                <Loader2 size={18} className="animate-spin shrink-0" style={{ color: accentColor }} />
                <span className="text-[14px] font-medium">
                  {status === "signing-in" ? "Signing you in…" : "Getting things ready…"}
                </span>
              </div>
            )}

            {status === "error" && (
              <div className="flex items-start gap-3 rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3.5 w-full">
                <XCircle size={18} className="text-destructive shrink-0 mt-0.5" />
                <p className="text-[13.5px] text-destructive leading-relaxed">{error}</p>
              </div>
            )}

            <div ref={buttonRef} className="w-full flex justify-start" style={{ display: status === "ready" ? "flex" : "none" }} />
          </div>

          {destination && (
            <p className="text-[12.5px] text-muted-foreground mt-8 pt-6 border-t border-border">
              Returning you to <span className="font-medium text-foreground">{destination}</span> once you're signed in.
            </p>
          )}
        </div>
      </main>

    </div>
  );
}

export default function GoogleAuthBridgePage() {
  return (
    <Suspense fallback={<div className="min-h-screen flex items-center justify-center bg-background"><Loader2 size={24} className="animate-spin text-primary" /></div>}>
      <GoogleAuthBridgeContent />
    </Suspense>
  );
}
