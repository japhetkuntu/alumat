"use client";

import { useState, Suspense } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { toast } from "sonner";
import { Eye, EyeOff, Loader2 } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useAuth } from "@/hooks/use-auth";
import { handleApiError } from "@/lib/api-client";

const schema = z.object({
  email: z.string().email("Enter a valid email"),
  password: z.string().min(8, "Password must be at least 8 characters"),
});

type FormData = z.infer<typeof schema>;

function LoginForm() {
  const { login } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const prefillEmail = searchParams.get("email") ?? "";
  const [showPassword, setShowPassword] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { email: prefillEmail },
  });

  async function onSubmit(data: FormData) {
    try {
      await login(data);
      router.push("/dashboard");
      toast.success("Welcome back!", {
        description: "You've successfully signed in to your alumni account.",
      });
    } catch (err) {
      toast.error("Sign in failed", {
        description: handleApiError(err),
      });
    }
  }

  return (
    <div className="w-full max-w-[420px] mx-auto animate-in fade-in slide-in-from-bottom-6 duration-700">
      {/* Mobile logo (visible when banner is hidden) */}
      <div className="mb-10 text-center md:hidden">
        <div className="w-14 h-14 rounded-2xl overflow-hidden mx-auto mb-5 shadow-sm border border-white/15 bg-white/10">
          <img src="/umat-logo.svg" alt="UMaT Logo" width={56} height={56} className="object-contain" />
        </div>
        <h1 className="text-2xl font-bold tracking-tight text-foreground">UMaT Alumni</h1>
        <p className="text-muted-foreground text-sm mt-1">Connecting gold-standard graduates</p>
      </div>

      <div className="space-y-8">
        <div className="space-y-3 text-center">
          <div className="text-sm font-semibold tracking-widest text-primary/90 uppercase">Member portal</div>
          <h1 className="text-[26px] font-bold text-foreground ">Sign in to your account</h1>
          <p className="text-sm text-muted-foreground">Access events, connections, and member resources.</p>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
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
            New to UMaT?{' '}
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
