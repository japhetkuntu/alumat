"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { toast } from "sonner";

import { CheckCircle2 } from "@alumni/ui";

import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { memberClient, handleApiError } from "@/lib/api-client";

const schema = z.object({ email: z.string().email("Enter a valid email") });
type FormData = z.infer<typeof schema>;

export default function ForgotPasswordPage() {
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting, isSubmitSuccessful },
  } = useForm<FormData>({ resolver: zodResolver(schema) });

  async function onSubmit(data: FormData) {
    try {
      await memberClient.post("/auth/forgot-password", data);
    } catch (err) {
      toast.error(handleApiError(err));
    }
  }

  return (
    <div className="w-full max-w-[420px] mx-auto animate-in fade-in slide-in-from-bottom-6 duration-700">
      <div className="space-y-8">
        <div className="space-y-2.5">
          <div className="text-sm font-semibold tracking-widest text-primary/90 uppercase">Account recovery</div>
          <h1 className="font-[family-name:var(--font-display)] text-[32px] sm:text-[34px] font-semibold text-foreground leading-tight">
            Forgot your password?
          </h1>
          <p className="text-sm text-muted-foreground">
            Enter your email and we&apos;ll send a reset link if an account exists.
          </p>
        </div>

        {isSubmitSuccessful ? (
          <div className="rounded-xl bg-success/10 p-4 flex items-start gap-3">
            <CheckCircle2 size={18} className="text-success shrink-0 mt-0.5" />
            <div className="text-sm">
              <p className="font-semibold text-success">Check your email</p>
              <p className="text-success/80 mt-0.5">
                If an account exists, you&apos;ll receive instructions shortly.
              </p>
            </div>
          </div>
        ) : (
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
            <div className="space-y-1.5">
              <Label htmlFor="email" className="text-[13px] font-semibold text-foreground/80">
                Email address
              </Label>
              <Input
                id="email"
                type="email"
                placeholder="you@example.com"
                autoComplete="email"
                error={!!errors.email}
                className="h-12 text-[15px]"
                {...register("email")}
              />
              {errors.email && (
                <p className="text-[12px] font-medium text-destructive animate-in fade-in slide-in-from-top-1">
                  {errors.email.message}
                </p>
              )}
            </div>
            <Button
              type="submit"
              className="w-full h-13 text-[15px] font-semibold shadow-sm hover:shadow-md transition-all"
              style={{ height: "52px" }}
              isLoading={isSubmitting}
              loadingText="Sending..."
            >
              Send reset link
            </Button>
          </form>
        )}

        <p className="text-center text-sm text-muted-foreground">
          <Link href="/login" className="text-primary hover:underline font-semibold">
            Back to sign in
          </Link>
        </p>
      </div>
    </div>
  );
}
