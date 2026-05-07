"use client";

import { useState, useRef, useEffect, Suspense } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { toast } from "sonner";
import { Eye, EyeOff, ArrowLeft, CheckCircle2, Clock, CreditCard, Loader2 } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { FormSelect } from "@/components/ui/select";
import { memberClient, handleApiError } from "@/lib/api-client";
import { getDepartments, getCurrentMembershipCampaign, type Department } from "@/lib/member-api";
import type { Campaign } from "@/types";

const currentYear = new Date().getFullYear();
const GRAD_YEAR_START = 1952;
const gradYears = Array.from(
  { length: currentYear - GRAD_YEAR_START + 1 },
  (_, i) => currentYear - i,
);

const schema = z.object({
  firstName: z.string().min(2, "First name is required"),
  lastName: z.string().min(2, "Last name is required"),
  email: z.string().email("Enter a valid email"),
  phone: z.string().optional(),
  studentId: z.string().min(1, "Student ID is required"),
  graduationYear: z.coerce.number().min(GRAD_YEAR_START).max(currentYear),
  departmentId: z.string().optional(),
  referralCode: z.string().optional(),
  password: z.string().min(8, "Password must be at least 8 characters"),
  confirmPassword: z.string(),
}).refine((d) => d.password === d.confirmPassword, {
  message: "Passwords do not match",
  path: ["confirmPassword"],
});

type FormData = z.infer<typeof schema>;
type Step = "form" | "otp" | "pending";

const STEPS: { key: Step; label: string; short: string }[] = [
  { key: "form", label: "Create account", short: "Account" },
  { key: "otp", label: "Verify email", short: "Verify" },
  { key: "pending", label: "Activate", short: "Activate" },
];

function StepIndicator({ step }: { step: Step }) {
  const current = STEPS.findIndex((s) => s.key === step);
  return (
    <div className="flex items-center w-full mb-8">
      {STEPS.map((s, i) => {
        const done = i < current;
        const active = i === current;
        return (
          <div key={s.key} className="flex items-center flex-1 min-w-0">
            <div className="flex flex-col items-center gap-1.5 flex-shrink-0">
              <div
                className={[
                  "flex items-center justify-center w-8 h-8 rounded-full border-2 text-xs font-bold transition-all duration-300",
                  done
                    ? "bg-primary border-primary text-primary-foreground"
                    : active
                    ? "bg-primary/10 border-primary text-primary"
                    : "bg-muted/40 border-border text-muted-foreground",
                ].join(" ")}
              >
                {done ? <CheckCircle2 size={14} /> : i + 1}
              </div>
              <span
                className={[
                  "text-[10px] font-medium leading-none whitespace-nowrap",
                  active ? "text-primary" : done ? "text-foreground/70" : "text-muted-foreground",
                ].join(" ")}
              >
                <span className="hidden sm:inline">{s.label}</span>
                <span className="sm:hidden">{s.short}</span>
              </span>
            </div>
            {i < STEPS.length - 1 && (
              <div
                className={[
                  "flex-1 h-[2px] mx-2 mt-[-12px] rounded-full transition-all duration-500",
                  done ? "bg-primary" : "bg-border",
                ].join(" ")}
              />
            )}
          </div>
        );
      })}
    </div>
  );
}

function formatCurrency(amount: number) {
  return new Intl.NumberFormat("en-GH", {
    style: "currency",
    currency: "GHS",
    minimumFractionDigits: 2,
  }).format(amount);
}

function formatDeadline(dateStr: string) {
  return new Intl.DateTimeFormat("en-GH", {
    day: "numeric",
    month: "long",
    year: "numeric",
  }).format(new Date(dateStr));
}

function MembershipCampaignCard({ campaign, email }: { campaign: Campaign; email: string }) {
  const daysLeft = Math.max(
    0,
    Math.ceil((new Date(campaign.deadline).getTime() - Date.now()) / 86_400_000),
  );

  return (
    <div className="rounded-2xl border border-primary/25 bg-gradient-to-br from-primary/5 via-primary/5 to-primary/10 overflow-hidden">
      {campaign.bannerImageUrl && (
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={campaign.bannerImageUrl}
          alt={campaign.title}
          className="w-full h-28 object-cover"
        />
      )}
      <div className="p-4 space-y-4">
        <div className="flex items-start justify-between gap-3">
          <div className="flex-1 min-w-0">
            <p className="text-[11px] font-semibold text-primary/80 uppercase tracking-widest mb-1">
              Membership Activation {campaign.membershipYear}
            </p>
            <h3 className="text-base font-bold text-foreground leading-tight line-clamp-2">{campaign.title}</h3>
          </div>
          <div className="flex-shrink-0 text-right">
            <p className="text-xl font-extrabold text-primary">{formatCurrency(campaign.amountPerMember)}</p>
            <p className="text-[10px] text-muted-foreground">per member</p>
          </div>
        </div>

        {campaign.description && (
          <p className="text-xs text-muted-foreground leading-relaxed line-clamp-3">{campaign.description}</p>
        )}

        <div className="flex items-center gap-4 text-xs text-muted-foreground">
          <div className="flex items-center gap-1.5">
            <Clock size={11} className="flex-shrink-0" />
            <span>Closes {formatDeadline(campaign.deadline)}</span>
          </div>
          {daysLeft > 0 && (
            <span className={["font-semibold", daysLeft <= 7 ? "text-destructive" : "text-amber-600 dark:text-amber-400"].join(" ")}>
              {daysLeft}d left
            </span>
          )}
        </div>

        <Link
          href={`/activate-membership/${campaign.id}${email ? `?email=${encodeURIComponent(email)}` : ""}`}
          className="flex items-center justify-center gap-2 w-full rounded-xl bg-primary px-4 py-3 text-sm font-semibold text-primary-foreground shadow-sm transition-all hover:bg-primary/90 active:scale-[0.98]"
        >
          <CreditCard size={15} />
          Activate membership — {formatCurrency(campaign.amountPerMember)}
        </Link>

        <p className="text-center text-[10px] text-muted-foreground leading-relaxed">
          Payment acts as proof of your alumni status and speeds up account approval.
        </p>
      </div>
    </div>
  );
}

function MembershipCampaignSection({ email }: { email: string }) {
  const [campaign, setCampaign] = useState<Campaign | null | undefined>(undefined);

  useEffect(() => {
    getCurrentMembershipCampaign()
      .then((c) => setCampaign(c))
      .catch(() => setCampaign(null));
  }, []);

  if (campaign === undefined) {
    return (
      <div className="rounded-2xl border border-border bg-muted/20 p-6 flex items-center justify-center gap-3 text-sm text-muted-foreground">
        <Loader2 size={16} className="animate-spin" />
        Checking membership campaigns...
      </div>
    );
  }

  if (!campaign) {
    return (
      <div className="rounded-2xl border border-secondary/30 bg-secondary/5 p-4 space-y-2">
        <p className="text-sm font-semibold text-foreground">Membership activation</p>
        <p className="text-xs text-muted-foreground leading-relaxed">
          There is no active membership campaign for {currentYear} yet. Once your account is approved, you can activate your membership from your dashboard or contact the alumni office.
        </p>
      </div>
    );
  }

  return <MembershipCampaignCard campaign={campaign} email={email} />;
}

export default function RegisterPage() {
  return (
    <Suspense>
      <RegisterForm />
    </Suspense>
  );
}

function RegisterForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [step, setStep] = useState<Step>("form");
  const [formSubStep, setFormSubStep] = useState<1 | 2 | 3>(1);
  const [email, setEmail] = useState("");
  const [otp, setOtp] = useState(["", "", "", "", "", ""]);
  const [verifying, setVerifying] = useState(false);
  const [resending, setResending] = useState(false);
  const [resendsLeft, setResendsLeft] = useState(3);
  const [departments, setDepartments] = useState<Department[]>([]);
  const inputRefs = useRef<(HTMLInputElement | null)[]>([]);

  useEffect(() => {
    getDepartments().then(setDepartments).catch(() => {});
  }, []);

  const refCode = searchParams.get("ref") ?? "";

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    trigger,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { referralCode: refCode },
  });

  async function onSubmit(data: FormData) {
    try {
      await memberClient.post("/auth/register", data);
      setEmail(data.email);
      setStep("otp");
      setResendsLeft(3);
      toast.success("Verification code sent to your email");
    } catch (err) {
      toast.error(handleApiError(err));
    }
  }

  function handleOtpChange(index: number, value: string) {
    if (!/^\d*$/.test(value)) return;
    const next = [...otp];
    next[index] = value.slice(-1);
    setOtp(next);
    if (value && index < 5) inputRefs.current[index + 1]?.focus();
  }

  function handleOtpKeyDown(index: number, e: React.KeyboardEvent) {
    if (e.key === "Backspace" && !otp[index] && index > 0) {
      inputRefs.current[index - 1]?.focus();
    }
  }

  function handleOtpPaste(e: React.ClipboardEvent) {
    e.preventDefault();
    const pasted = e.clipboardData.getData("text").replace(/\D/g, "").slice(0, 6);
    if (!pasted) return;
    const next = [...otp];
    for (let i = 0; i < 6; i++) next[i] = pasted[i] || "";
    setOtp(next);
    inputRefs.current[Math.min(pasted.length, 5)]?.focus();
  }

  async function verifyOtp() {
    const code = otp.join("");
    if (code.length !== 6) { toast.error("Enter the complete 6-digit code"); return; }
    setVerifying(true);
    try {
      await memberClient.post("/auth/verify-otp", { email, otp: code });
      toast.success("Email verified! Your account is pending admin approval.");
      setStep("pending");
    } catch (err) {
      toast.error(handleApiError(err));
    } finally {
      setVerifying(false);
    }
  }

  async function resendOtp() {
    setResending(true);
    try {
      const res = await memberClient.post("/auth/resend-otp", { email });
      toast.success(res.data?.message ?? "Verification code resent");
      setResendsLeft((p) => p - 1);
      setOtp(["", "", "", "", "", ""]);
      inputRefs.current[0]?.focus();
    } catch (err) {
      toast.error(handleApiError(err));
    } finally {
      setResending(false);
    }
  }

  return (
    <div className="w-full animate-in fade-in slide-in-from-bottom-6 duration-700">
      {/* Mobile logo */}
      <div className="mb-8 text-center md:hidden">
        <div className="w-14 h-14 bg-primary rounded-2xl flex items-center justify-center mx-auto mb-4 shadow-sm border border-white/15">
          <span className="text-xl font-bold text-primary-foreground">UM</span>
        </div>
        <h1 className="text-2xl font-bold tracking-tight text-foreground">UMaT Alumni</h1>
        <p className="text-muted-foreground text-sm mt-1">Connecting gold-standard graduates</p>
      </div>

      <div className="space-y-6">
        {/* Step indicator */}
        <StepIndicator step={step} />

        {/* Step heading */}
        <div className="space-y-1.5">
          <div className="text-xs font-semibold tracking-widest text-primary/80 uppercase">Member portal</div>
          {step === "form" && (
            <>
              <h1 className="text-[26px] font-bold text-foreground">
                {formSubStep === 1 && "Personal info"}
                {formSubStep === 2 && "Your alumni info"}
                {formSubStep === 3 && "Secure your account"}
              </h1>
              <p className="text-sm text-muted-foreground">
                {formSubStep === 1 && "Let's start with the basics."}
                {formSubStep === 2 && "Help us verify your alumni status."}
                {formSubStep === 3 && "Choose a strong password for your account."}
              </p>
            </>
          )}
          {step === "otp" && (
            <>
              <h1 className="text-[26px] font-bold text-foreground">Verify your email</h1>
              <p className="text-sm text-muted-foreground">
                We sent a 6-digit code to <span className="font-medium text-foreground">{email}</span>. Enter it below to confirm your address.
              </p>
            </>
          )}
          {step === "pending" && (
            <>
              <h1 className="text-[26px] font-bold text-foreground">Almost there!</h1>
              <p className="text-sm text-muted-foreground">Your email is verified. Complete the steps below to speed up your approval.</p>
            </>
          )}
        </div>

        {/* ── Step 1: Registration form (3 sub-steps) ───────────── */}
        {step === "form" && (
          <form
            onSubmit={handleSubmit(onSubmit)}
            className="space-y-5"
          >
            {/* Sub-step progress */}
            <div className="flex items-center gap-2">
              {([1, 2, 3] as const).map((n) => (
                <div
                  key={n}
                  className={[
                    "h-1 flex-1 rounded-full transition-all duration-300",
                    n < formSubStep
                      ? "bg-primary"
                      : n === formSubStep
                      ? "bg-primary/50"
                      : "bg-border",
                  ].join(" ")}
                />
              ))}
              <span className="text-[11px] text-muted-foreground font-medium whitespace-nowrap ml-1">
                {formSubStep} / 3
              </span>
            </div>

            {/* Sub-step 1 — Personal info */}
            {formSubStep === 1 && (
              <div className="space-y-4 animate-in fade-in slide-in-from-right-4 duration-300">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="firstName">First name</Label>
                    <Input id="firstName" placeholder="Kwame" autoFocus {...register("firstName")} />
                    {errors.firstName && <p className="text-xs text-destructive">{errors.firstName.message}</p>}
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="lastName">Last name</Label>
                    <Input id="lastName" placeholder="Mensah" {...register("lastName")} />
                    {errors.lastName && <p className="text-xs text-destructive">{errors.lastName.message}</p>}
                  </div>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="email">Email address</Label>
                  <Input id="email" type="email" placeholder="you@example.com" {...register("email")} />
                  {errors.email && <p className="text-xs text-destructive">{errors.email.message}</p>}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="phone">
                    Phone number <span className="text-muted-foreground font-normal">(optional)</span>
                  </Label>
                  <Input id="phone" type="tel" placeholder="+233 XX XXX XXXX" {...register("phone")} />
                </div>
                <Button
                  type="button"
                  className="w-full"
                  onClick={async () => {
                    const ok = await trigger(["firstName", "lastName", "email"]);
                    if (ok) setFormSubStep(2);
                  }}
                >
                  Continue
                </Button>
              </div>
            )}

            {/* Sub-step 2 — Alumni info */}
            {formSubStep === 2 && (
              <div className="space-y-4 animate-in fade-in slide-in-from-right-4 duration-300">
                <div className="space-y-2">
                  <Label htmlFor="studentId">Student ID</Label>
                  <Input id="studentId" placeholder="e.g. UMaT/ENG/20/0001" autoFocus {...register("studentId")} />
                  {errors.studentId && <p className="text-xs text-destructive">{errors.studentId.message}</p>}
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="graduationYear">Graduation year</Label>
                    <FormSelect
                      value={watch("graduationYear") ? String(watch("graduationYear")) : ""}
                      onValueChange={(v) =>
                        setValue("graduationYear", Number(v) as unknown as number, { shouldValidate: true })
                      }
                      placeholder="Select year"
                      options={gradYears.map((y) => ({ value: String(y), label: String(y) }))}
                    />
                    {errors.graduationYear && <p className="text-xs text-destructive">{errors.graduationYear.message}</p>}
                  </div>
                  {departments.length > 0 && (
                    <div className="space-y-2">
                      <Label htmlFor="departmentId">
                        Department <span className="text-muted-foreground font-normal">(optional)</span>
                      </Label>
                      <FormSelect
                        value={watch("departmentId") ?? ""}
                        onValueChange={(v) => setValue("departmentId", v, { shouldValidate: true })}
                        placeholder="Select department"
                        options={departments.map((d) => ({ value: d.id, label: d.name }))}
                      />
                    </div>
                  )}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="referralCode">
                    Referral code <span className="text-muted-foreground font-normal">(optional)</span>
                  </Label>
                  <Input
                    id="referralCode"
                    placeholder="e.g. KWAMEN-A1B2C3"
                    {...register("referralCode")}
                    readOnly={!!refCode}
                    className={refCode ? "bg-muted" : ""}
                  />
                  {refCode && (
                    <p className="text-xs text-muted-foreground">You were referred by a fellow alumnus!</p>
                  )}
                </div>
                <div className="flex gap-3">
                  <Button
                    type="button"
                    variant="outline"
                    className="flex-1"
                    onClick={() => setFormSubStep(1)}
                  >
                    <ArrowLeft size={14} className="mr-1" /> Back
                  </Button>
                  <Button
                    type="button"
                    className="flex-1"
                    onClick={async () => {
                      const ok = await trigger(["studentId", "graduationYear"]);
                      if (ok) setFormSubStep(3);
                    }}
                  >
                    Continue
                  </Button>
                </div>
              </div>
            )}

            {/* Sub-step 3 — Password */}
            {formSubStep === 3 && (
              <div className="space-y-4 animate-in fade-in slide-in-from-right-4 duration-300">
                <div className="space-y-2">
                  <Label htmlFor="password">Password</Label>
                  <div className="relative">
                    <Input
                      id="password"
                      type={showPassword ? "text" : "password"}
                      placeholder="Min 8 characters"
                      autoFocus
                      {...register("password")}
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword(!showPassword)}
                      className="absolute right-1 top-1/2 -translate-y-1/2 flex items-center justify-center h-8 w-8 rounded-md text-muted-foreground hover:text-foreground hover:bg-accent/50 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/30"
                      aria-label={showPassword ? "Hide password" : "Show password"}
                    >
                      {showPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                    </button>
                  </div>
                  {errors.password && <p className="text-xs text-destructive">{errors.password.message}</p>}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="confirmPassword">Confirm password</Label>
                  <div className="relative">
                    <Input
                      id="confirmPassword"
                      type={showConfirmPassword ? "text" : "password"}
                      placeholder="Repeat your password"
                      {...register("confirmPassword")}
                    />
                    <button
                      type="button"
                      onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                      className="absolute right-1 top-1/2 -translate-y-1/2 flex items-center justify-center h-8 w-8 rounded-md text-muted-foreground hover:text-foreground hover:bg-accent/50 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/30"
                      aria-label={showConfirmPassword ? "Hide password" : "Show password"}
                    >
                      {showConfirmPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                    </button>
                  </div>
                  {errors.confirmPassword && <p className="text-xs text-destructive">{errors.confirmPassword.message}</p>}
                </div>
                <div className="flex gap-3">
                  <Button
                    type="button"
                    variant="outline"
                    className="flex-1"
                    onClick={() => setFormSubStep(2)}
                  >
                    <ArrowLeft size={14} className="mr-1" /> Back
                  </Button>
                  <Button type="submit" className="flex-1" isLoading={isSubmitting} loadingText="Submitting...">
                    Create account
                  </Button>
                </div>
              </div>
            )}
          </form>
        )}

        {/* ── Step 2: OTP Verification ───────────────────────────── */}
        {step === "otp" && (
          <div className="space-y-6">
            <div className="flex justify-center gap-2 sm:gap-3" onPaste={handleOtpPaste}>
              {otp.map((digit, i) => (
                <input
                  key={i}
                  ref={(el) => { inputRefs.current[i] = el; }}
                  type="text"
                  inputMode="numeric"
                  maxLength={1}
                  value={digit}
                  onChange={(e) => handleOtpChange(i, e.target.value)}
                  onKeyDown={(e) => handleOtpKeyDown(i, e)}
                  className="h-12 w-12 sm:h-14 sm:w-14 rounded-[10px] border border-input bg-background/50 text-center text-xl font-bold transition-all duration-150 ease-out focus:border-ring/50 focus:outline-none focus:ring-2 focus:ring-ring/15 focus:bg-background"
                />
              ))}
            </div>

            <Button
              className="w-full"
              onClick={verifyOtp}
              isLoading={verifying}
              loadingText="Verifying..."
              disabled={otp.join("").length !== 6}
            >
              Verify &amp; continue
            </Button>

            <div className="text-center text-sm text-muted-foreground">
              Didn&apos;t receive the code?{" "}
              {resendsLeft > 0 ? (
                <button
                  type="button"
                  onClick={resendOtp}
                  disabled={resending}
                  className="text-primary hover:underline font-medium disabled:opacity-50"
                >
                  {resending ? "Sending..." : `Resend code (${resendsLeft} left)`}
                </button>
              ) : (
                <span className="text-destructive font-medium">No resend attempts remaining</span>
              )}
            </div>

            <button
              type="button"
              onClick={() => { setStep("form"); setFormSubStep(1); setOtp(["", "", "", "", "", ""]); }}
              className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground mx-auto transition-colors"
            >
              <ArrowLeft size={14} /> Back to registration form
            </button>
          </div>
        )}

        {/* ── Step 3: Pending approval + Membership activation ────── */}
        {step === "pending" && (
          <div className="space-y-5">
            {/* Status checklist */}
            <div className="rounded-2xl border border-border bg-card p-5 space-y-3">
              <div className="flex items-start gap-3">
                <div className="mt-0.5 flex-shrink-0 flex items-center justify-center w-9 h-9 rounded-full bg-green-500/10 border border-green-500/20">
                  <CheckCircle2 size={18} className="text-green-500" />
                </div>
                <div>
                  <p className="font-semibold text-foreground text-sm">Email verified</p>
                  <p className="text-xs text-muted-foreground mt-0.5">
                    Registration submitted for <span className="font-medium text-foreground">{email}</span>
                  </p>
                </div>
              </div>
              <div className="h-px bg-border" />
              <div className="flex items-start gap-3">
                <div className="mt-0.5 flex-shrink-0 flex items-center justify-center w-9 h-9 rounded-full bg-amber-500/10 border border-amber-500/20">
                  <Clock size={18} className="text-amber-500" />
                </div>
                <div>
                  <p className="font-semibold text-foreground text-sm">Awaiting admin approval</p>
                  <p className="text-xs text-muted-foreground mt-0.5">
                    Our team reviews your details. This usually takes up to 24 hours.
                  </p>
                </div>
              </div>
            </div>

            {/* Membership activation section */}
            <div className="space-y-3">
              <div>
                <p className="text-sm font-semibold text-foreground">Activate your membership</p>
                <p className="text-xs text-muted-foreground mt-0.5">
                  Paying the membership activation fee confirms your alumni status and helps us approve your account faster.
                </p>
              </div>
              <MembershipCampaignSection email={email} />
            </div>

            {/* Footer actions */}
            <div className="flex flex-col gap-3 sm:flex-row sm:justify-end pt-1">
              <Button
                variant="outline"
                className="w-full sm:w-auto"
                onClick={() => { setStep("form"); setFormSubStep(1); setOtp(["", "", "", "", "", ""]); }}
              >
                Register another account
              </Button>
              <Button className="w-full sm:w-auto" onClick={() => router.push("/login")}>
                Go to login
              </Button>
            </div>
          </div>
        )}

        {step === "form" && (
          <p className="text-center text-sm text-muted-foreground">
            Already have an account?{" "}
            <Link href="/login" className="text-primary hover:underline font-medium">
              Sign in
            </Link>
          </p>
        )}
      </div>
    </div>
  );
}
