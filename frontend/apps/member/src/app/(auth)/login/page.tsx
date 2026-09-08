"use client";

import { useEffect, useState, Suspense } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { toast } from "sonner";
import { Eye, EyeOff, Loader2 } from "@alumni/ui";
import { AuthMobileBrand } from "@/components/member/auth-mobile-brand";

import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { handleApiError } from "@/lib/api-client";

const schema = z.object({
  email: z.string().email("Enter a valid email"),
  password: z.string().min(8, "Password must be at least 8 characters"),
});

type FormData = z.infer<typeof schema>;

// Dev-only: lets a local build reach a specific institution without real
// wildcard-subdomain DNS (see TenantResolutionMiddleware / X-Institution-Slug).
// Ignored by the backend outside Development, and hidden here in production
// builds since real members reach their institution via its actual subdomain.
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
 *  back with the already-issued session — see apps/member/src/app/google-auth. */
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

function GoogleSignInButton({ portal }: { portal: "member" | "institution" | "platform" }) {
  // Always the fixed base domain, never this institution's own subdomain —
  // that's the one origin actually registered with Google. Matters here
  // even though this bridge page lives in this very app: a member visiting
  // via their institution's subdomain must still be sent to the base domain
  // for the Google Identity Services SDK to accept the origin.
  const baseDomain = process.env.NEXT_PUBLIC_BASE_DOMAIN;
  const bridgeUrl = baseDomain ? `https://${baseDomain}/google-auth` : undefined;
  const [origin, setOrigin] = useState("");
  useEffect(() => { setOrigin(window.location.origin); }, []);
  if (!bridgeUrl || !origin) return null;
  const href = `${bridgeUrl}?portal=${portal}&return=${encodeURIComponent(origin)}`;
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

function LoginForm() {
  const { login, setSession } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const prefillEmail = searchParams.get("email") ?? "";
  const [showPassword, setShowPassword] = useState(false);
  const [workspaceSlug, setWorkspaceSlug] = useState("");

  useGoogleAuthRelay((user, tokens) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    setSession(user as any, tokens as any);
    toast.success("Welcome back!", { description: "You've successfully signed in with Google." });
    router.push("/dashboard");
  });

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { email: prefillEmail },
  });

  async function onSubmit(data: FormData) {
    // Only writes when non-empty — leaving this field blank means "don't
    // touch it", not "clear whatever slug is already set" (e.g. via the
    // console or a previous login), so it can't silently wipe it out.
    if (SHOW_WORKSPACE_FIELD && workspaceSlug.trim()) {
      persistWorkspaceSlug(workspaceSlug);
    }
    try {
      await login(data);
      toast.success("Welcome back!", {
        description: "You've successfully signed in to your alumni account.",
      });
      // A hard navigation (not router.push) so the root layout's server-side
      // theme fetch re-runs and picks up the workspace cookie set above —
      // client-side route transitions reuse the already-rendered root layout.
      if (SHOW_WORKSPACE_FIELD) window.location.assign("/dashboard");
      else router.push("/dashboard");
    } catch (err) {
      toast.error("Sign in failed", {
        description: handleApiError(err),
      });
    }
  }

  return (
    <div className="w-full max-w-[420px] mx-auto animate-in fade-in slide-in-from-bottom-6 duration-700">
      <AuthMobileBrand fallbackTagline="Sign in to your alumni network" />

      <div className="space-y-8">
        <div className="space-y-2.5">
          <div className="text-sm font-semibold tracking-widest text-primary/90 uppercase">Member portal</div>
          <h1 className="font-[family-name:var(--font-display)] text-[32px] sm:text-[38px] font-semibold text-foreground leading-tight">
            Welcome back
          </h1>
          <p className="text-sm text-muted-foreground">Sign in to stay connected with your alumni community.</p>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
          {SHOW_WORKSPACE_FIELD && (
            <div className="space-y-1.5">
              <Label htmlFor="workspace" className="text-[13px] font-semibold text-foreground/80">
                Workspace (dev only)
              </Label>
              <Input
                id="workspace"
                placeholder="greenfield"
                value={workspaceSlug}
                onChange={(e) => setWorkspaceSlug(e.target.value)}
                className="h-12 text-[15px]"
              />
              <p className="text-[12px] text-muted-foreground">
                Institution slug — stands in for real subdomain routing until wildcard DNS is set up.
              </p>
            </div>
          )}

          <div className="space-y-1.5">
            <Label htmlFor="email" className="text-[13px] font-semibold text-foreground/80">
              Email address
            </Label>
            <Input
              id="email"
              type="email"
              placeholder="name@example.com"
              autoComplete="email"
              error={!!errors.email}
              {...register("email")}
              className="h-12 text-[15px]"
            />
            {errors.email && (
              <p className="text-[12px] font-medium text-destructive animate-in fade-in slide-in-from-top-1 field-error">
                {errors.email.message}
              </p>
            )}
          </div>

          <div className="space-y-1.5">
            <div className="flex items-center justify-between">
              <Label htmlFor="password" className="text-[13px] font-semibold text-foreground/80">
                Password
              </Label>
              <Link
                href="/forgot-password"
                className="text-[12px] text-primary hover:text-primary/80 transition-colors font-semibold"
              >
                Forgot password?
              </Link>
            </div>
            <div className="relative">
              <Input
                id="password"
                type={showPassword ? "text" : "password"}
                placeholder="••••••••"
                autoComplete="current-password"
                error={!!errors.password}
                {...register("password")}
                className="h-12 text-[15px] pr-12"
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                className="absolute right-0 top-0 h-full w-12 flex items-center justify-center text-muted-foreground hover:text-foreground transition-colors"
                aria-label={showPassword ? "Hide password" : "Show password"}
              >
                {showPassword ? <EyeOff size={17} /> : <Eye size={17} />}
              </button>
            </div>
            {errors.password && (
              <p className="text-[12px] font-medium text-destructive animate-in fade-in slide-in-from-top-1 field-error">
                {errors.password.message}
              </p>
            )}
          </div>

          <Button
            type="submit"
            className="w-full h-13 text-[15px] font-semibold shadow-sm hover:shadow-md transition-all mt-2"
            style={{ height: "52px" }}
            isLoading={isSubmitting}
            loadingText="Signing you in..."
          >
            Sign in
          </Button>
        </form>

        <div className="flex items-center gap-3">
          <div className="h-px flex-1" style={{ background: "var(--border)" }} />
          <span className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">or</span>
          <div className="h-px flex-1" style={{ background: "var(--border)" }} />
        </div>

        <GoogleSignInButton portal="member" />

        <div className="text-center text-sm text-muted-foreground space-y-3">
          <p>
            New here?{' '}
            <Link href="/register" className="text-primary hover:underline font-semibold">
              Create an account
            </Link>
          </p>
        </div>
      </div>
    </div>
  );
}

export default function LoginPage() {
  return (
    <Suspense fallback={<div className="w-full flex items-center justify-center py-20"><Loader2 size={28} className="animate-spin text-primary" /></div>}>
      <LoginForm />
    </Suspense>
  );
}
