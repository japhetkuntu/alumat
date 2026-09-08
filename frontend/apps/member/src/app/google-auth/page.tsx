"use client";

import { useCallback, useEffect, useRef, useState, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { Loader2, XCircle, ShieldCheck } from "@alumni/ui";

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
        type: "standard", theme: "outline", size: "large", width: 280,
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
    <div className="min-h-screen flex items-center justify-center bg-background px-4">
      <div className="w-full max-w-sm rounded-2xl border p-8 text-center space-y-5" style={{ borderColor: "var(--border)" }}>
        <div className="h-11 w-11 rounded-full flex items-center justify-center mx-auto bg-primary/10">
          <ShieldCheck size={20} className="text-primary" />
        </div>
        <div>
          <h1 className="text-[16px] font-semibold" style={{ color: "var(--foreground)" }}>
            {mode === "register" ? "Sign up with Google" : "Sign in with Google"}
          </h1>
          <p className="text-[13px] mt-1" style={{ color: "var(--muted-foreground)" }}>
            {mode === "register" ? "Continuing your registration for " : "Continuing to your "}{PORTAL_LABEL[portal] ?? "portal"}
          </p>
        </div>

        {(status === "loading" || status === "signing-in") && (
          <div className="flex items-center justify-center gap-2 py-4">
            <Loader2 size={18} className="animate-spin text-primary" />
            <span className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>
              {status === "signing-in" ? "Signing you in…" : "Loading…"}
            </span>
          </div>
        )}

        {status === "error" && (
          <div className="flex flex-col items-center gap-2 py-2">
            <XCircle size={20} className="text-destructive" />
            <p className="text-[13px] text-destructive">{error}</p>
          </div>
        )}

        <div ref={buttonRef} className="flex justify-center" style={{ display: status === "ready" ? "flex" : "none" }} />
      </div>
    </div>
  );
}

export default function GoogleAuthBridgePage() {
  return (
    <Suspense fallback={<div className="min-h-screen flex items-center justify-center"><Loader2 size={28} className="animate-spin text-primary" /></div>}>
      <GoogleAuthBridgeContent />
    </Suspense>
  );
}
