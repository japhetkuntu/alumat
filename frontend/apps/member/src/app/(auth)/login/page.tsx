"use client";

import { useState, Suspense } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { toast } from "sonner";
import { Eye, EyeOff, Loader2 } from "lucide-react";
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

function LoginForm() {
  const { login } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const prefillEmail = searchParams.get("email") ?? "";
  const [showPassword, setShowPassword] = useState(false);
  const [workspaceSlug, setWorkspaceSlug] = useState("");

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
