"use client";

import { Suspense, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { toast } from "sonner";
import { CheckCircle2, Eye, EyeOff } from "lucide-react";

import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { memberClient, handleApiError } from "@/lib/api-client";

const schema = z
  .object({
    newPassword: z.string().min(8, "Password must be at least 8 characters"),
    confirm: z.string(),
  })
  .refine((d) => d.newPassword === d.confirm, { message: "Passwords do not match", path: ["confirm"] });
type FormData = z.infer<typeof schema>;

function ResetPasswordForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";
  const email = searchParams.get("email") ?? "";
  const [showPassword, setShowPassword] = useState(false);
  const [done, setDone] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({ resolver: zodResolver(schema) });

  async function onSubmit(data: FormData) {
    try {
      await memberClient.post("/auth/reset-password", { token, email, newPassword: data.newPassword });
      setDone(true);
      toast.success("Password reset — sign in with your new password.");
      setTimeout(() => router.push("/login"), 1800);
    } catch (err) {
      toast.error(handleApiError(err));
    }
  }

  if (!token || !email) {
    return (
      <div className="w-full max-w-[420px] mx-auto animate-in fade-in slide-in-from-bottom-6 duration-700 text-center space-y-4">
        <h1 className="font-[family-name:var(--font-display)] text-[28px] font-semibold text-foreground">Invalid reset link</h1>
        <p className="text-sm text-muted-foreground">
          This password reset link is missing or incomplete. Request a new one below.
        </p>
        <Link href="/forgot-password" className="inline-block text-primary hover:underline font-semibold text-sm">
          Request a new link
        </Link>
      </div>
    );
  }

  if (done) {
    return (
      <div className="w-full max-w-[420px] mx-auto text-center space-y-4 animate-in fade-in slide-in-from-bottom-4 duration-500">
        <div className="w-14 h-14 rounded-full bg-success/10 flex items-center justify-center mx-auto">
          <CheckCircle2 className="text-success" size={26} />
        </div>
        <h1 className="font-[family-name:var(--font-display)] text-[26px] font-semibold text-foreground">Password reset</h1>
        <p className="text-sm text-muted-foreground">Taking you to sign in…</p>
      </div>
    );
  }

  return (
    <div className="w-full max-w-[420px] mx-auto animate-in fade-in slide-in-from-bottom-6 duration-700 space-y-8">
      <div className="space-y-2.5">
        <div className="text-sm font-semibold tracking-widest text-primary/90 uppercase">Account recovery</div>
        <h1 className="font-[family-name:var(--font-display)] text-[32px] sm:text-[34px] font-semibold text-foreground leading-tight">
          Set a new password
        </h1>
        <p className="text-sm text-muted-foreground">Resetting the password for <b>{email}</b>.</p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
        <div className="space-y-1.5">
          <Label htmlFor="newPassword" className="text-[13px] font-semibold text-foreground/80">New password</Label>
          <div className="relative">
            <Input
              id="newPassword"
              type={showPassword ? "text" : "password"}
              placeholder="At least 8 characters"
              error={!!errors.newPassword}
              className="h-12 text-[15px] pr-11"
              {...register("newPassword")}
            />
            <button
              type="button"
              onClick={() => setShowPassword((v) => !v)}
              className="absolute right-0 top-0 h-full w-11 flex items-center justify-center text-muted-foreground hover:text-foreground"
              aria-label={showPassword ? "Hide password" : "Show password"}
            >
              {showPassword ? <EyeOff size={16} /> : <Eye size={16} />}
            </button>
          </div>
          {errors.newPassword && <p className="text-[12px] font-medium text-destructive">{errors.newPassword.message}</p>}
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="confirm" className="text-[13px] font-semibold text-foreground/80">Confirm new password</Label>
          <Input
            id="confirm"
            type={showPassword ? "text" : "password"}
            placeholder="Repeat new password"
            error={!!errors.confirm}
            className="h-12 text-[15px]"
            {...register("confirm")}
          />
          {errors.confirm && <p className="text-[12px] font-medium text-destructive">{errors.confirm.message}</p>}
        </div>
        <Button
          type="submit"
          className="w-full text-[15px] font-semibold"
          style={{ height: "52px" }}
          isLoading={isSubmitting}
          loadingText="Resetting…"
        >
          Reset password
        </Button>
      </form>

      <p className="text-center text-sm text-muted-foreground">
        <Link href="/login" className="text-primary hover:underline font-semibold">Back to sign in</Link>
      </p>
    </div>
  );
}

export default function ResetPasswordPage() {
  return (
    <Suspense fallback={null}>
      <ResetPasswordForm />
    </Suspense>
  );
}
