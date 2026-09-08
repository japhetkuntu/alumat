"use client";

import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { EyeOff } from "@alumni/ui";

import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { handleApiError } from "@/lib/api-client";
import { AuthMobileBrand } from "@/components/institution/auth-brand-mark";
import { buildGoogleBridgeUrl, useGoogleBridgeTheme } from "@/lib/google-bridge-theme";

const schema = z.object({
  email:    z.string().email("Enter a valid email"),
  password: z.string().min(8, "Password must be at least 8 characters"),
});
type FormData = z.infer<typeof schema>;

// Dev-only: lets a local build reach a specific institution without real
// wildcard-subdomain DNS (see TenantResolutionMiddleware / X-Institution-Slug).
// Ignored by the backend outside Development, and hidden here in production
// builds since real customers reach their institution via its actual subdomain.
const SHOW_WORKSPACE_FIELD = process.env.NODE_ENV === "development";

// Mirrors the slug into a cookie (not just localStorage) so the server-side
// theme fetch in layout.tsx — which runs before any client JS and has no
// access to localStorage — can also forward X-Institution-Slug and render
// the right institution's colors/branding on first paint, not just after
// client-side API calls kick in.
function persistWorkspaceSlug(slug: string) {
  const trimmed = slug.trim().toLowerCase();
  if (trimmed) {
    localStorage.setItem("institution_slug", trimmed);
    document.cookie = `institution_slug=${trimmed}; path=/; max-age=2592000; samesite=lax`;
  } else {
    localStorage.removeItem("institution_slug");
    document.cookie = "institution_slug=; path=/; max-age=0";
  }
}

/** URL fragment (never sent to any server) the Google sign-in bridge hands
 *  back with the already-issued session — see the member app's /google-auth. */
function useGoogleAuthRelay(onSession: (user: unknown, tokens: unknown) => void) {
  useEffect(() => {
    const hash = window.location.hash;
    if (!hash.startsWith("#auth=")) return;
    try {
      const { user, tokens } = JSON.parse(atob(decodeURIComponent(hash.slice("#auth=".length))));
      history.replaceState(null, "", window.location.pathname + window.location.search);
      onSession(user, tokens);
    } catch {
      history.replaceState(null, "", window.location.pathname + window.location.search);
    }
    // Runs once on mount only — the fragment is consumed and stripped immediately.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
}

function GoogleSignInButton() {
  // Always the fixed base domain, never this portal's own (per-institution)
  // subdomain — that's the one origin actually registered with Google.
  const baseDomain = process.env.NEXT_PUBLIC_BASE_DOMAIN;
  const bridgeUrl = baseDomain ? `https://${baseDomain}/google-auth` : undefined;
  const [origin, setOrigin] = useState("");
  useEffect(() => { setOrigin(window.location.origin); }, []);
  const { data: theme } = useGoogleBridgeTheme();
  if (!bridgeUrl || !origin) return null;
  const href = buildGoogleBridgeUrl(bridgeUrl, { portal: "institution", return: origin, theme });
  return (
    <a
      href={href}
      className="flex items-center justify-center gap-2.5 w-full h-12 border text-[14px] font-semibold transition-colors hover:bg-muted/50"
      style={{ borderColor: "var(--border)", color: "var(--foreground)" }}
    >
      <svg width="16" height="16" viewBox="0 0 48 48" aria-hidden="true">
        <path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3c-1.6 4.7-6.1 8-11.3 8-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.9 1.2 8 3.1l5.7-5.7C34.6 6 29.6 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.3-.1-2.7-.4-3.5z" />
        <path fill="#FF3D00" d="M6.3 14.7l6.6 4.8C14.6 15.9 18.9 13 24 13c3.1 0 5.9 1.2 8 3.1l5.7-5.7C34.6 6 29.6 4 24 4 16.3 4 9.7 8.3 6.3 14.7z" />
        <path fill="#4CAF50" d="M24 44c5.5 0 10.4-1.9 14.3-5.1l-6.6-5.6c-2 1.5-4.6 2.5-7.7 2.5-5.2 0-9.6-3.3-11.3-8l-6.6 5.1C9.6 39.6 16.3 44 24 44z" />
        <path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.3-2.2 4.2-4.1 5.6l6.6 5.6C41.4 36.1 44 30.5 44 24c0-1.3-.1-2.7-.4-3.5z" />
      </svg>
      Continue with Google
    </a>
  );
}

export default function AdminLoginPage() {
  const { login, setSession } = useAuth();
  const router    = useRouter();
  const [showPassword, setShowPassword] = useState(false);
  const [workspaceSlug, setWorkspaceSlug] = useState("");

  useGoogleAuthRelay((user, tokens) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    setSession(user as any, tokens as any);
    toast.success("Welcome back.", { description: "You're signed in to the Institution Portal." });
    router.push("/dashboard");
  });

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({ resolver: zodResolver(schema) });

  async function onSubmit(data: FormData) {
    // Only writes when non-empty — leaving this field blank means "don't
    // touch it", not "clear whatever slug is already set" (e.g. via the
    // console or a previous login), so it can't silently wipe it out.
    if (SHOW_WORKSPACE_FIELD && workspaceSlug.trim()) {
      persistWorkspaceSlug(workspaceSlug);
    }
    try {
      await login(data);
      toast.success("Welcome back.", {
        description: "You're signed in to the Institution Portal.",
      });
      // A hard navigation (not router.push) so the root layout's server-side
      // theme fetch re-runs and picks up the workspace cookie set above —
      // client-side route transitions reuse the already-rendered root layout.
      if (SHOW_WORKSPACE_FIELD) window.location.assign("/dashboard");
      else router.push("/dashboard");
    } catch (err) {
      toast.error("Sign-in failed", { description: handleApiError(err) });
    }
  }

  return (
    <div className="w-full max-w-[460px] mx-auto animate-in fade-in slide-in-from-bottom-4 duration-500">

      <AuthMobileBrand />

      <div className="space-y-3">
        <p
          className="text-[13px] font-bold tracking-[0.08em] uppercase"
          style={{ color: "var(--muted-foreground)" }}
        >
          Institution Portal
        </p>
        <h1
          className="leading-[1.15]"
          style={{ fontSize: "34px", fontWeight: 700, color: "var(--foreground)", letterSpacing: "-0.01em" }}
        >
          Sign in to your staff portal
        </h1>
        <p className="text-[15px]" style={{ color: "var(--muted-foreground)", lineHeight: 1.55 }}>
          Use your institution staff account. Member accounts are redirected to the member portal.
        </p>
      </div>

      {/* Form */}
      <form onSubmit={handleSubmit(onSubmit)} className="mt-10">

        {SHOW_WORKSPACE_FIELD && (
          <div className="mb-6">
            <Label htmlFor="workspace" className="text-[13.5px] font-bold mb-2 block" style={{ color: "var(--foreground)" }}>
              Workspace (dev only)
            </Label>
            <Input
              id="workspace"
              placeholder="greenfield"
              value={workspaceSlug}
              onChange={(e) => setWorkspaceSlug(e.target.value)}
              className="h-12 text-[15px] px-4"
            />
            <p className="text-[12.5px] mt-1.5" style={{ color: "var(--muted-foreground)" }}>
              Institution slug — stands in for real subdomain routing until wildcard DNS is set up.
            </p>
          </div>
        )}

        {/* Email */}
        <div className="mb-6">
          <Label htmlFor="email" className="text-[13.5px] font-bold mb-2 block" style={{ color: "var(--foreground)" }}>
            Email address
          </Label>
          <Input
            id="email"
            type="email"
            placeholder="admin@example.com"
            autoComplete="email"
            {...register("email")}
            className="h-12 text-[15px] px-4"
            aria-invalid={!!errors.email}
            aria-describedby={errors.email ? "email-error" : undefined}
          />
          {errors.email && (
            <p id="email-error" className="text-[12.5px] font-medium text-destructive mt-1.5 animate-in fade-in slide-in-from-top-1">
              {errors.email.message}
            </p>
          )}
        </div>

        {/* Password */}
        <div className="mb-7">
          <Label htmlFor="password" className="text-[13.5px] font-bold mb-2 block" style={{ color: "var(--foreground)" }}>
            Password
          </Label>
          <div className="relative">
            <Input
              id="password"
              type={showPassword ? "text" : "password"}
              placeholder="••••••••"
              autoComplete="current-password"
              {...register("password")}
              className="h-12 text-[15px] px-4 pr-16"
              aria-invalid={!!errors.password}
              aria-describedby={errors.password ? "password-error" : undefined}
            />
            <button
              type="button"
              onClick={() => setShowPassword(v => !v)}
              className="absolute right-2 top-1/2 -translate-y-1/2 text-[13px] font-semibold px-2 py-1.5 transition-colors"
              style={{ color: "var(--muted-foreground)" }}
              aria-label={showPassword ? "Hide password" : "Show password"}
            >
              {showPassword ? <EyeOff size={16} aria-hidden="true" /> : "Show"}
            </button>
          </div>
          {errors.password && (
            <p id="password-error" className="text-[12.5px] font-medium text-destructive mt-1.5 animate-in fade-in slide-in-from-top-1">
              {errors.password.message}
            </p>
          )}
        </div>

        {/* Remember + forgot row */}
        <div className="flex items-center justify-between text-[13.5px] mb-7">
          <label className="flex items-center gap-2 text-foreground cursor-pointer">
            <input type="checkbox" className="h-4 w-4 rounded border-input" />
            Remember this device
          </label>
          <Link
            href="/forgot-password"
            className="font-bold hover:underline"
            style={{ color: "#1e3a8a" }}
          >
            Forgot password?
          </Link>
        </div>

        {/* Submit */}
        <Button
          type="submit"
          className="w-full font-bold text-[15px]"
          style={{ height: 48 }}
          isLoading={isSubmitting}
          loadingText="Signing in…"
        >
          Sign in
        </Button>

        <div className="flex items-center gap-3 mt-7">
          <div className="h-px flex-1" style={{ background: "var(--border)" }} />
          <span className="text-[11px] font-semibold uppercase tracking-wide" style={{ color: "var(--muted-foreground)" }}>or</span>
          <div className="h-px flex-1" style={{ background: "var(--border)" }} />
        </div>

        <div className="mt-7">
          <GoogleSignInButton />
        </div>
      </form>
    </div>
  );
}
