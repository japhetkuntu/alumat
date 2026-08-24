"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Eye, EyeOff } from "lucide-react";

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

export default function PlatformLoginPage() {
  const { login } = useAuth();
  const router = useRouter();
  const [showPassword, setShowPassword] = useState(false);

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
      <div className="mb-8 md:hidden flex flex-col items-center gap-3">
        <div className="w-12 h-12 rounded-xl flex items-center justify-center bg-primary">
          <span className="text-white text-[16px] font-bold">P</span>
        </div>
        <div className="text-center">
          <p className="text-[15px] font-semibold" style={{ color: "var(--foreground)" }}>Platform Portal</p>
        </div>
      </div>

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
              placeholder="you@yourplatform.com"
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
      </div>
    </div>
  );
}
