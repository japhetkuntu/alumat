"use client";

import { Suspense, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { toast } from "sonner";
import { EyeOff } from "@alumni/ui";

import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { institutionClient, handleApiError } from "@/lib/api-client";

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
  const [showPw, setShowPw] = useState(false);
  const [done, setDone] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({ resolver: zodResolver(schema) });

  async function onSubmit(data: FormData) {
    try {
      await institutionClient.post("/auth/reset-password", { token, email, newPassword: data.newPassword });
      setDone(true);
      toast.success("Password reset — sign in with your new password.");
      setTimeout(() => router.push("/login"), 1800);
    } catch (err) {
      toast.error(handleApiError(err));
    }
  }

  if (!token || !email) {
    return (
      <div className="w-full max-w-[460px] mx-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
        <p className="text-[13px] font-bold tracking-[0.08em] uppercase" style={{ color: "var(--muted-foreground)" }}>Account recovery</p>
        <h1 className="mt-2 leading-[1.15]" style={{ fontSize: "34px", fontWeight: 700, color: "var(--foreground)" }}>Invalid reset link</h1>
        <p className="mt-3 text-[15px]" style={{ color: "var(--muted-foreground)", lineHeight: 1.55 }}>
          This password reset link is missing or incomplete. Request a new one to continue.
        </p>
        <Link href="/forgot-password" className="mt-6 inline-block text-[13.5px] font-bold hover:underline" style={{ color: "#1e3a8a" }}>
          Request a new link
        </Link>
      </div>
    );
  }

  if (done) {
    return (
      <div className="w-full max-w-[460px] mx-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
        <p className="text-[13px] font-bold tracking-[0.08em] uppercase" style={{ color: "var(--muted-foreground)" }}>Account recovery</p>
        <h1 className="mt-2" style={{ fontSize: "34px", fontWeight: 700, color: "var(--foreground)" }}>Password reset</h1>
        <p className="mt-3 text-[15px]" style={{ color: "var(--muted-foreground)", lineHeight: 1.55 }}>Taking you to sign in…</p>
      </div>
    );
  }

  return (
    <div className="w-full max-w-[460px] mx-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
      <p className="text-[13px] font-bold tracking-[0.08em] uppercase" style={{ color: "var(--muted-foreground)" }}>Account recovery</p>
      <h1 className="mt-2 leading-[1.15]" style={{ fontSize: "34px", fontWeight: 700, color: "var(--foreground)" }}>Set a new password</h1>
      <p className="mt-3 text-[15px]" style={{ color: "var(--muted-foreground)", lineHeight: 1.55 }}>
        Resetting the password for <b>{email}</b>.
      </p>

      <form onSubmit={handleSubmit(onSubmit)} className="mt-8">
        <div className="mb-6">
          <Label htmlFor="newPassword" className="text-[13.5px] font-bold mb-2 block" style={{ color: "var(--foreground)" }}>New password</Label>
          <div className="relative">
            <Input
              id="newPassword"
              type={showPw ? "text" : "password"}
              placeholder="At least 8 characters"
              className="h-12 text-[15px] px-4 pr-16"
              {...register("newPassword")}
            />
            <button type="button" onClick={() => setShowPw(!showPw)}
              className="absolute right-2 top-1/2 -translate-y-1/2 text-[13px] font-semibold px-2 py-1.5" style={{ color: "var(--muted-foreground)" }}>
              {showPw ? <EyeOff size={16} /> : "Show"}
            </button>
          </div>
          {errors.newPassword && <p className="text-[12.5px] font-medium text-destructive mt-1.5">{errors.newPassword.message}</p>}
        </div>
        <div className="mb-7">
          <Label htmlFor="confirm" className="text-[13.5px] font-bold mb-2 block" style={{ color: "var(--foreground)" }}>Confirm new password</Label>
          <Input
            id="confirm"
            type={showPw ? "text" : "password"}
            placeholder="Repeat new password"
            className="h-12 text-[15px] px-4"
            {...register("confirm")}
          />
          {errors.confirm && <p className="text-[12.5px] font-medium text-destructive mt-1.5">{errors.confirm.message}</p>}
        </div>
        <Button type="submit" className="w-full font-bold text-[15px]" style={{ height: 48 }} isLoading={isSubmitting} loadingText="Resetting…">
          Reset password
        </Button>
      </form>

      <Link href="/login" className="mt-6 block text-center text-[13.5px] font-bold hover:underline" style={{ color: "#1e3a8a" }}>
        &larr; Return to sign in
      </Link>
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
