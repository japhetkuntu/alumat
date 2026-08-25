"use client";

import { Suspense, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { toast } from "sonner";
import { CheckCircle2 } from "lucide-react";

import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { platformClient, handleApiError } from "@/lib/api-client";

const requestSchema = z.object({ email: z.string().email("Enter a valid email") });
type RequestFormData = z.infer<typeof requestSchema>;

const resetSchema = z
  .object({
    newPassword: z.string().min(8, "Password must be at least 8 characters"),
    confirm: z.string(),
  })
  .refine((d) => d.newPassword === d.confirm, { message: "Passwords do not match", path: ["confirm"] });
type ResetFormData = z.infer<typeof resetSchema>;

// Requests a reset link — shown when the page is opened directly (no token
// in the URL yet), e.g. from the login page's "Forgot password?" link.
function RequestLinkForm() {
  const [sent, setSent] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RequestFormData>({ resolver: zodResolver(requestSchema) });

  async function onSubmit(data: RequestFormData) {
    try {
      await platformClient.post("/auth/forgot-password", data);
      setSent(data.email);
    } catch (err) {
      toast.error(handleApiError(err));
    }
  }

  if (sent) {
    return (
      <div className="w-full max-w-[420px] mx-auto text-center space-y-4 animate-in fade-in slide-in-from-bottom-4 duration-500">
        <div className="w-14 h-14 rounded-full bg-green-100 flex items-center justify-center mx-auto">
          <CheckCircle2 className="text-green-600" size={26} />
        </div>
        <h1 className="text-[22px] font-bold" style={{ color: "var(--foreground)" }}>Check your inbox</h1>
        <p className="text-[14px]" style={{ color: "var(--muted-foreground)" }}>
          If an eligible platform-staff account exists for <b>{sent}</b>, reset instructions were sent. Links expire after 24 hours.
        </p>
        <Link href="/login" className="text-[13px] font-semibold hover:underline" style={{ color: "var(--primary)" }}>
          &larr; Return to sign in
        </Link>
      </div>
    );
  }

  return (
    <div className="w-full max-w-[420px] mx-auto space-y-7 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="space-y-2">
        <p className="text-[11.5px] font-bold tracking-[0.1em] uppercase" style={{ color: "var(--primary)" }}>Account recovery</p>
        <h1 className="text-[26px] font-bold leading-tight" style={{ color: "var(--foreground)" }}>Reset your password</h1>
        <p className="text-[14px]" style={{ color: "var(--muted-foreground)" }}>
          Enter your work email and we&apos;ll send instructions if the account is eligible.
        </p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div className="space-y-1.5">
          <Label htmlFor="email" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>Work email</Label>
          <Input id="email" type="email" placeholder="you@alumunion.com" {...register("email")} className="h-12 text-[15px]" />
          {errors.email && <p className="text-[12px] font-medium text-destructive">{errors.email.message}</p>}
        </div>
        <Button type="submit" className="w-full font-semibold text-[15px]" style={{ height: 52 }} isLoading={isSubmitting} loadingText="Sending…">
          Send reset link
        </Button>
      </form>

      <Link href="/login" className="block text-center text-[13px] font-semibold hover:underline" style={{ color: "var(--primary)" }}>
        &larr; Return to sign in
      </Link>
    </div>
  );
}

// Sets a new password — shown once a token+email arrive from the emailed link.
function SetNewPasswordForm({ token, email }: { token: string; email: string }) {
  const router = useRouter();
  const [done, setDone] = useState(false);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ResetFormData>({ resolver: zodResolver(resetSchema) });

  async function onSubmit(data: ResetFormData) {
    try {
      await platformClient.post("/auth/reset-password", { token, email, newPassword: data.newPassword });
      setDone(true);
      toast.success("Password reset — sign in with your new password.");
      setTimeout(() => router.push("/login"), 1800);
    } catch (err) {
      toast.error(handleApiError(err));
    }
  }

  if (done) {
    return (
      <div className="w-full max-w-[420px] mx-auto text-center space-y-4 animate-in fade-in slide-in-from-bottom-4 duration-500">
        <div className="w-14 h-14 rounded-full bg-green-100 flex items-center justify-center mx-auto">
          <CheckCircle2 className="text-green-600" size={26} />
        </div>
        <h1 className="text-[22px] font-bold" style={{ color: "var(--foreground)" }}>Password reset</h1>
        <p className="text-[14px]" style={{ color: "var(--muted-foreground)" }}>Taking you to sign in…</p>
      </div>
    );
  }

  return (
    <div className="w-full max-w-[420px] mx-auto space-y-7 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <div className="space-y-2">
        <p className="text-[11.5px] font-bold tracking-[0.1em] uppercase" style={{ color: "var(--primary)" }}>Account recovery</p>
        <h1 className="text-[26px] font-bold leading-tight" style={{ color: "var(--foreground)" }}>Set a new password</h1>
        <p className="text-[14px]" style={{ color: "var(--muted-foreground)" }}>Resetting the password for <b>{email}</b>.</p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div className="space-y-1.5">
          <Label htmlFor="newPassword" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>New password</Label>
          <Input id="newPassword" type="password" placeholder="At least 8 characters" {...register("newPassword")} className="h-12 text-[15px]" />
          {errors.newPassword && <p className="text-[12px] font-medium text-destructive">{errors.newPassword.message}</p>}
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="confirm" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>Confirm new password</Label>
          <Input id="confirm" type="password" placeholder="Repeat new password" {...register("confirm")} className="h-12 text-[15px]" />
          {errors.confirm && <p className="text-[12px] font-medium text-destructive">{errors.confirm.message}</p>}
        </div>
        <Button type="submit" className="w-full font-semibold text-[15px]" style={{ height: 52 }} isLoading={isSubmitting} loadingText="Resetting…">
          Reset password
        </Button>
      </form>

      <Link href="/login" className="block text-center text-[13px] font-semibold hover:underline" style={{ color: "var(--primary)" }}>
        &larr; Return to sign in
      </Link>
    </div>
  );
}

function ResetPasswordRouter() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token");
  const email = searchParams.get("email");
  if (token && email) return <SetNewPasswordForm token={token} email={email} />;
  return <RequestLinkForm />;
}

export default function ResetPasswordPage() {
  return (
    <Suspense fallback={null}>
      <ResetPasswordRouter />
    </Suspense>
  );
}
