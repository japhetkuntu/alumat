"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { toast } from "sonner";

import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { institutionClient, handleApiError } from "@/lib/api-client";

const schema = z.object({ email: z.string().email("Enter a valid email") });
type FormData = z.infer<typeof schema>;

export default function AdminForgotPasswordPage() {
  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting, isSubmitSuccessful },
  } = useForm<FormData>({ resolver: zodResolver(schema) });

  const submittedEmail = watch("email");

  async function onSubmit(data: FormData) {
    try {
      await institutionClient.post("/auth/forgot-password", data);
    } catch (err) {
      toast.error(handleApiError(err));
    }
  }

  if (isSubmitSuccessful) {
    return (
      <div className="w-full max-w-[440px] mx-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
        <p className="text-[13px] font-bold tracking-[0.08em] uppercase" style={{ color: "var(--muted-foreground)" }}>
          Account recovery
        </p>
        <h1 className="mt-2" style={{ fontSize: "34px", fontWeight: 700, color: "var(--foreground)" }}>
          Check your inbox
        </h1>
        <p className="mt-3 text-[15px]" style={{ color: "var(--muted-foreground)", lineHeight: 1.55 }}>
          If an eligible staff account exists for <b>{submittedEmail}</b>, reset instructions were sent. Links expire after 30 minutes.
        </p>
        <Link href="/login" className="mt-6 inline-block text-[13.5px] font-bold hover:underline" style={{ color: "#1e3a8a" }}>
          &larr; Return to sign in
        </Link>
      </div>
    );
  }

  return (
    <div className="w-full max-w-[440px] mx-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
      <p className="text-[13px] font-bold tracking-[0.08em] uppercase" style={{ color: "var(--muted-foreground)" }}>
        Account recovery
      </p>
      <h1 className="mt-2 leading-[1.15]" style={{ fontSize: "34px", fontWeight: 700, color: "var(--foreground)" }}>
        Reset your password
      </h1>
      <p className="mt-3 text-[15px]" style={{ color: "var(--muted-foreground)", lineHeight: 1.55 }}>
        Enter your staff email and we&apos;ll send reset instructions if the account is eligible.
      </p>

      <form onSubmit={handleSubmit(onSubmit)} className="mt-8">
        <div className="mb-6">
          <Label htmlFor="email" className="text-[13.5px] font-bold mb-2 block" style={{ color: "var(--foreground)" }}>
            Email address
          </Label>
          <Input
            id="email"
            type="email"
            placeholder="you@example.com"
            className="h-12 text-[15px] px-4"
            {...register("email")}
            aria-invalid={!!errors.email}
            aria-describedby={errors.email ? "email-error" : undefined}
          />
          {errors.email && <p id="email-error" className="text-[12.5px] font-medium text-destructive mt-1.5">{errors.email.message}</p>}
        </div>
        <Button type="submit" className="w-full font-bold text-[15px]" style={{ height: 48 }} isLoading={isSubmitting} loadingText="Sending…">
          Send reset link
        </Button>
      </form>

      <div
        className="mt-6 p-4 rounded-[8px] text-[13px] leading-relaxed"
        style={{ background: "var(--muted)", color: "var(--muted-foreground)" }}
      >
        <b className="text-foreground">Set a new password</b>
        <br />
        Use at least 8 characters. Reset links expire after 30 minutes and only work for this institution&apos;s staff accounts.
      </div>

      <Link href="/login" className="mt-6 block text-center text-[13.5px] font-bold hover:underline" style={{ color: "#1e3a8a" }}>
        &larr; Return to sign in
      </Link>
    </div>
  );
}
