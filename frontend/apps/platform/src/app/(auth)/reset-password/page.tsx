"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { CheckCircle2 } from "lucide-react";

import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";

const schema = z.object({ email: z.string().email("Enter a valid email") });
type FormData = z.infer<typeof schema>;

export default function ResetPasswordPage() {
  const [sent, setSent] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({ resolver: zodResolver(schema) });

  async function onSubmit(data: FormData) {
    await new Promise((r) => setTimeout(r, 500));
    setSent(data.email);
  }

  if (sent) {
    return (
      <div className="w-full max-w-[420px] mx-auto text-center space-y-4 animate-in fade-in slide-in-from-bottom-4 duration-500">
        <div className="w-14 h-14 rounded-full bg-green-100 flex items-center justify-center mx-auto">
          <CheckCircle2 className="text-green-600" size={26} />
        </div>
        <h1 className="text-[22px] font-bold" style={{ color: "var(--foreground)" }}>Check your inbox</h1>
        <p className="text-[14px]" style={{ color: "var(--muted-foreground)" }}>
          If an eligible platform-staff account exists for <b>{sent}</b>, reset instructions were sent. Links expire after 30 minutes.
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
        <p className="text-[11.5px] font-bold tracking-[0.1em] uppercase" style={{ color: "var(--primary)" }}>
          Account recovery
        </p>
        <h1 className="text-[26px] font-bold leading-tight" style={{ color: "var(--foreground)" }}>
          Reset your password
        </h1>
        <p className="text-[14px]" style={{ color: "var(--muted-foreground)" }}>
          Enter your work email and we&apos;ll send instructions if the account is eligible.
        </p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div className="space-y-1.5">
          <Label htmlFor="email" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
            Work email
          </Label>
          <Input id="email" type="email" placeholder="you@yourplatform.com" {...register("email")} className="h-12 text-[15px]" />
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
