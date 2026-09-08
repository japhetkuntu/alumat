"use client";

import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Eye, EyeOff } from "@alumni/ui";

import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { handleApiError } from "@/lib/api-client";

const schema = z.object({
  email: z.string().email("Enter a valid email"),
  password: z.string().min(1, "Password is required"),
});
type FormData = z.infer<typeof schema>;

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
  // Always the fixed base domain, never this portal's own subdomain — that's
  // the one origin actually registered with Google.
  const baseDomain = process.env.NEXT_PUBLIC_BASE_DOMAIN;
  const bridgeUrl = baseDomain ? `https://${baseDomain}/google-auth` : undefined;
  const [origin, setOrigin] = useState("");
  useEffect(() => { setOrigin(window.location.origin); }, []);
  if (!bridgeUrl || !origin) return null;
  const href = `${bridgeUrl}?portal=platform&return=${encodeURIComponent(origin)}`;
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

export default function PlatformLoginPage() {
  const { login, setSession } = useAuth();
  const router = useRouter();
  const [showPassword, setShowPassword] = useState(false);

  useGoogleAuthRelay((user, tokens) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    setSession(user as any, tokens as any);
    toast.success("Welcome back.", { description: "You're signed in to the Platform Portal." });
    router.push("/dashboard");
  });

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({ resolver: zodResolver(schema) });

  async function onSubmit(data: FormData) {
    try {
      await login(data);
      router.push("/dashboard");
      toast.success("Welcome back.", {
        description: "You're signed in to the Platform Portal.",
      });
    } catch (err) {
      toast.error("Sign-in failed", { description: handleApiError(err) });
    }
  }

  return (
    <div className="w-full max-w-[420px] mx-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="space-y-7">
        <div className="space-y-2">
          <p className="text-[11.5px] font-bold tracking-[0.1em] uppercase" style={{ color: "var(--primary)" }}>
            Platform Portal
          </p>
          <h1
            className="font-[family-name:var(--font-display)] leading-tight"
            style={{ fontSize: "clamp(1.5rem, 3vw, 1.75rem)", fontWeight: 700, color: "var(--foreground)", letterSpacing: "-0.01em" }}
          >
            Sign in to manage the platform
          </h1>
          <p className="text-[14px]" style={{ color: "var(--muted-foreground)" }}>
            Internal tool for platform operations, support, and billing staff.
          </p>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="email" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
              Email address
            </Label>
            <Input
              id="email"
              type="email"
              placeholder="you@alumunion.com"
              autoComplete="email"
              {...register("email")}
              className="h-12 text-[15px]"
            />
            {errors.email && (
              <p className="text-[12px] font-medium text-destructive">{errors.email.message}</p>
            )}
          </div>

          <div className="space-y-1.5">
            <div className="flex items-center justify-between">
              <Label htmlFor="password" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                Password
              </Label>
              <Link href="/reset-password" className="text-[12.5px] font-semibold hover:underline" style={{ color: "var(--primary)" }}>
                Forgot password?
              </Link>
            </div>
            <div className="relative">
              <Input
                id="password"
                type={showPassword ? "text" : "password"}
                placeholder="••••••••"
                autoComplete="current-password"
                {...register("password")}
                className="h-12 text-[15px] pr-12"
              />
              <button
                type="button"
                onClick={() => setShowPassword((v) => !v)}
                className="absolute right-0 top-0 h-full w-12 flex items-center justify-center"
                style={{ color: "var(--muted-foreground)" }}
                aria-label={showPassword ? "Hide password" : "Show password"}
              >
                {showPassword ? <EyeOff size={16} /> : <Eye size={16} />}
              </button>
            </div>
            {errors.password && (
              <p className="text-[12px] font-medium text-destructive">{errors.password.message}</p>
            )}
          </div>

          <Button type="submit" className="w-full font-semibold text-[15px] mt-1" style={{ height: 52 }} isLoading={isSubmitting} loadingText="Signing in…">
            Sign in
          </Button>
        </form>

        <div className="flex items-center gap-3">
          <div className="h-px flex-1" style={{ background: "var(--border)" }} />
          <span className="text-[11px] font-semibold uppercase tracking-wide" style={{ color: "var(--muted-foreground)" }}>or</span>
          <div className="h-px flex-1" style={{ background: "var(--border)" }} />
        </div>

        <GoogleSignInButton />
      </div>
    </div>
  );
}
