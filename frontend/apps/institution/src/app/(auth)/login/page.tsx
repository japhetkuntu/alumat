"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { EyeOff } from "lucide-react";

import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { handleApiError } from "@/lib/api-client";

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

export default function AdminLoginPage() {
  const { login } = useAuth();
  const router    = useRouter();
  const [showPassword, setShowPassword] = useState(false);
  const [workspaceSlug, setWorkspaceSlug] = useState("");

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

      {/* Mobile logo — only visible when the left panel is hidden */}
      <div className="mb-10 md:hidden flex flex-col items-center gap-3">
        <div className="w-12 h-12 rounded-[10px] bg-primary text-white flex items-center justify-center font-extrabold text-[20px]">
          U
        </div>
        <div className="text-center">
          <p className="text-[15px] font-semibold" style={{ color: "var(--foreground)" }}>Institution Portal</p>
          <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>Institution Portal</p>
        </div>
      </div>

      {/* Mark, matching the mockup's colored letter mark */}
      <div className="hidden md:flex w-[52px] h-[52px] rounded-[10px] bg-primary text-white items-center justify-center font-extrabold text-[24px] mb-6">
        U
      </div>

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
      </form>
    </div>
  );
}
