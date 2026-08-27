"use client";

import { useState, useRef, useEffect, useMemo, Suspense } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery } from "@tanstack/react-query";
import { z } from "zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import {
  Eye, EyeOff, ArrowLeft, CheckCircle2, Clock,
  CreditCard, Loader2, ChevronRight,
} from "lucide-react";

import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { PhoneInput } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { memberClient, publicMemberClient, handleApiError, applyServerFieldErrors } from "@/lib/api-client";
import { getDepartments, getBatches, getCurrentMembershipCampaign, type Department, type Batch } from "@/lib/member-api";
import type { Campaign } from "@/types";
import { cn } from "@alumni/ui";
import { AuthMobileBrand } from "@/components/member/auth-mobile-brand";

// Dev-only: lets a local build reach a specific institution without real
// wildcard-subdomain DNS (see TenantResolutionMiddleware / X-Institution-Slug).
// Ignored by the backend outside Development, and hidden here in production
// builds since real members reach their institution via its actual subdomain.
const SHOW_WORKSPACE_FIELD = process.env.NODE_ENV === "development";

const currentYear = new Date().getFullYear();
const GRAD_YEAR_START = 1952;
const gradYears = Array.from(
  { length: currentYear - GRAD_YEAR_START + 1 },
  (_, i) => currentYear - i,
);

// Whether Student ID is required varies per institution (platform-configured
// via RequireStudentId — see the theme fetch below), so the schema is built
// dynamically instead of being a fixed module-level constant.
function buildSchema(requireStudentId: boolean) {
  return z
    .object({
      firstName: z.string().min(2, "First name is required"),
      lastName: z.string().min(2, "Last name is required"),
      email: z.string().email("Enter a valid email"),
      phone: z.string()
        .min(9, "Enter a valid mobile number")
        .regex(/^[+0-9\s-]+$/, "Enter a valid mobile number"),
      studentId: requireStudentId ? z.string().min(1, "Student ID is required") : z.string().optional(),
      graduationYear: z.coerce.number().min(GRAD_YEAR_START).max(currentYear),
      departmentId: z.string().optional(),
      password: z.string().min(8, "Password must be at least 8 characters"),
      confirmPassword: z.string(),
    })
    .refine((d) => d.password === d.confirmPassword, {
      message: "Passwords do not match",
      path: ["confirmPassword"],
    });
}

type FormData = z.infer<ReturnType<typeof buildSchema>>;
type Step = "form" | "otp" | "pending";

/* ─────────────────────────────────────────────────────────────────────────
   STEP INDICATOR
   ───────────────────────────────────────────────────────────────────────── */
const STEPS: { key: Step; label: string }[] = [
  { key: "form",    label: "Your details" },
  { key: "otp",     label: "Verify email" },
  { key: "pending", label: "Activate"     },
];

function StepIndicator({ step }: { step: Step }) {
  const current = STEPS.findIndex((s) => s.key === step);
  return (
    <div className="flex items-center justify-center gap-0 mb-8">
      {STEPS.map((s, i) => {
        const done   = i < current;
        const active = i === current;
        return (
          <div key={s.key} className="flex items-center">
            {/* Circle */}
            <div className="flex flex-col items-center gap-1.5 shrink-0">
              <div
                className={cn(
                  "w-7 h-7 rounded-full flex items-center justify-center text-[11px] font-bold border-2 transition-all duration-300",
                  done   && "bg-primary border-primary text-white",
                  active && "border-primary bg-primary/10 text-primary",
                  !done && !active && "border-border bg-background text-muted-foreground",
                )}
              >
                {done ? <CheckCircle2 size={13} /> : i + 1}
              </div>
              <span
                className={cn(
                  "text-[10px] font-medium leading-none whitespace-nowrap",
                  active && "text-primary",
                  done   && "text-foreground/60",
                  !done && !active && "text-muted-foreground",
                )}
              >
                {s.label}
              </span>
            </div>
            {/* Connector */}
            {i < STEPS.length - 1 && (
              <div
                className={cn(
                  "w-16 h-[2px] mx-2 mt-[-12px] rounded-full transition-all duration-500 shrink-0",
                  done ? "bg-primary" : "bg-border",
                )}
              />
            )}
          </div>
        );
      })}
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   HELPERS
   ───────────────────────────────────────────────────────────────────────── */
function formatCurrency(amount: number) {
  return new Intl.NumberFormat("en-GH", {
    style: "currency", currency: "GHS", minimumFractionDigits: 2,
  }).format(amount);
}

function formatDeadline(dateStr: string) {
  return new Intl.DateTimeFormat("en-GH", {
    day: "numeric", month: "long", year: "numeric",
  }).format(new Date(dateStr));
}

/* ─────────────────────────────────────────────────────────────────────────
   MEMBERSHIP CAMPAIGN CARD
   ───────────────────────────────────────────────────────────────────────── */
function MembershipCampaignCard({ campaign, email }: { campaign: Campaign; email: string }) {
  const [now] = useState(() => Date.now());
  const daysLeft = Math.max(
    0,
    Math.ceil((new Date(campaign.deadline).getTime() - now) / 86_400_000),
  );

  return (
    <div className="rounded-xl border border-border overflow-hidden bg-primary/5">
      {campaign.bannerImageUrl && (
        <img
          src={campaign.bannerImageUrl}
          alt={campaign.title}
          className="w-full h-24 object-cover"
        />
      )}
      <div className="p-4 space-y-3">
        {/* Header row */}
        <div className="flex items-start justify-between gap-3">
          <div className="flex-1 min-w-0 space-y-1">
            <Badge variant="default">Membership {campaign.membershipYear}</Badge>
            <h3 className="text-[14px] font-semibold leading-snug" style={{ color: "var(--foreground)" }}>
              {campaign.title}
            </h3>
          </div>
          <div className="shrink-0 text-right">
            <p className="text-[20px] font-bold" style={{ color: "var(--primary)" }}>
              {formatCurrency(campaign.amountPerMember)}
            </p>
            <p className="text-[10px]" style={{ color: "var(--muted-foreground)" }}>per member</p>
          </div>
        </div>

        {campaign.description && (
          <p className="text-[12.5px] leading-relaxed line-clamp-2" style={{ color: "var(--muted-foreground)" }}>
            {campaign.description}
          </p>
        )}

        {/* Meta */}
        <div className="flex items-center gap-1.5 text-[12px]" style={{ color: "var(--muted-foreground)" }}>
          <Clock size={11} className="shrink-0" />
          <span>Closes {formatDeadline(campaign.deadline)}</span>
          {daysLeft > 0 && (
            <span
              className="font-semibold ml-1"
              style={{ color: daysLeft <= 7 ? "var(--destructive)" : "var(--warning)" }}
            >
              · {daysLeft}d left
            </span>
          )}
        </div>

        {/* CTA */}
        <Link
          href={`/activate-membership/${campaign.id}${email ? `?email=${encodeURIComponent(email)}` : ""}`}
          className="flex items-center justify-center gap-2 w-full rounded-lg px-4 py-2.5 text-[13.5px] font-semibold text-white transition-opacity hover:opacity-90"
          style={{ background: "var(--primary)" }}
        >
          <CreditCard size={14} />
          Activate — {formatCurrency(campaign.amountPerMember)}
        </Link>

        <p className="text-center text-[11px]" style={{ color: "var(--muted-foreground)" }}>
          Payment confirms your alumni status and speeds up approval.
        </p>
      </div>
    </div>
  );
}

function MembershipCampaignSection({ email }: { email: string }) {
  const [campaign, setCampaign] = useState<Campaign | null | undefined>(undefined);

  useEffect(() => {
    getCurrentMembershipCampaign().then(setCampaign).catch(() => setCampaign(null));
  }, []);

  if (campaign === undefined) {
    return (
      <div className="rounded-xl border p-5 flex items-center gap-3 text-[13px]"
        style={{ borderColor: "var(--border)", color: "var(--muted-foreground)" }}>
        <Loader2 size={15} className="animate-spin shrink-0" />
        Checking for active membership dues…
      </div>
    );
  }

  if (!campaign) {
    return (
      <div className="rounded-xl border p-4" style={{ borderColor: "var(--border)", background: "var(--secondary)" }}>
        <p className="text-[13px] font-semibold mb-1" style={{ color: "var(--foreground)" }}>
          Membership activation
        </p>
        <p className="text-[12.5px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
          No active membership dues for {currentYear} yet. You can activate your membership from your
          dashboard once approved, or contact the alumni office directly.
        </p>
      </div>
    );
  }

  return <MembershipCampaignCard campaign={campaign} email={email} />;
}

/* ─────────────────────────────────────────────────────────────────────────
   FIELD ERROR
   ───────────────────────────────────────────────────────────────────────── */
function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return (
    <p className="text-[12px] font-medium text-destructive animate-in fade-in slide-in-from-top-1">
      {message}
    </p>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   PASSWORD INPUT
   ───────────────────────────────────────────────────────────────────────── */
function PasswordInput({
  id, placeholder, show, onToggle, registration, error,
}: {
  id: string;
  placeholder: string;
  show: boolean;
  onToggle: () => void;
  registration: ReturnType<ReturnType<typeof useForm<FormData>>["register"]>;
  error?: string;
}) {
  return (
    <div className="space-y-1.5">
      <div className="relative">
        <Input
          id={id}
          type={show ? "text" : "password"}
          placeholder={placeholder}
          error={!!error}
          className="h-11 text-[14px] pr-11"
          {...registration}
        />
        <button
          type="button"
          onClick={onToggle}
          className="absolute right-0 top-0 h-full w-11 flex items-center justify-center transition-colors"
          style={{ color: "var(--muted-foreground)" }}
          aria-label={show ? "Hide password" : "Show password"}
        >
          {show ? <EyeOff size={16} /> : <Eye size={16} />}
        </button>
      </div>
      <FieldError message={error} />
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   REGISTER FORM
   ───────────────────────────────────────────────────────────────────────── */
function RegisterForm() {
  const router = useRouter();
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
  const [batches, setBatches] = useState<Batch[]>([]);
  // Starts "" to match SSR (localStorage isn't available server-side), then
  // syncs from any previously-saved workspace slug once mounted client-side.
  const [workspaceSlug, setWorkspaceSlug] = useState("");
  const inputRefs = useRef<(HTMLInputElement | null)[]>([]);

  useEffect(() => {
    if (SHOW_WORKSPACE_FIELD) {
      setWorkspaceSlug(localStorage.getItem("institution_slug") ?? "");
    }
  }, []);

  function updateWorkspaceSlug(value: string) {
    setWorkspaceSlug(value);
    const trimmed = value.trim().toLowerCase();
    if (trimmed) {
      localStorage.setItem("institution_slug", trimmed);
      // Mirrored into a cookie so the server-side theme fetch in layout.tsx
      // (no access to localStorage) can also forward X-Institution-Slug.
      document.cookie = `institution_slug=${trimmed}; path=/; max-age=2592000; samesite=lax`;
    } else {
      localStorage.removeItem("institution_slug");
      document.cookie = "institution_slug=; path=/; max-age=0";
    }
  }

  useEffect(() => {
    getDepartments().then(setDepartments).catch(() => {});
    getBatches().then(setBatches).catch(() => {});
  }, []);

  // Institutions that haven't set up their own batches yet fall back to the
  // platform's generic year range, so registration never breaks for them.
  const graduationYearOptions = batches.length > 0
    ? batches.map((b) => ({
        value: String(b.year),
        // Always surface the year alongside the batch name — a member who's
        // forgotten what their institution called their batch can still find
        // themselves by year. Skip the suffix if the name already contains
        // the year (e.g. "Batch of 2020") to avoid "Batch of 2020 (2020)".
        label: b.name.includes(String(b.year)) ? b.name : `${b.name} (${b.year})`,
      }))
    : gradYears.map((y) => ({ value: String(y), label: String(y) }));

  // Whether Student ID is required is per-institution config (RequireStudentId),
  // defaulting to true (the previous, always-required behavior) until the
  // real value loads, so the field never briefly appears optional by mistake.
  const { data: theme } = useQuery({
    queryKey: ["m-register-institution-theme"],
    queryFn: async () => {
      const res = await publicMemberClient.get<{ data: { requireStudentId: boolean } }>("/public/institution/theme");
      return res.data.data;
    },
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
  const requireStudentId = theme?.requireStudentId ?? true;
  const schema = useMemo(() => buildSchema(requireStudentId), [requireStudentId]);

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    trigger,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  async function onSubmit(data: FormData) {
    try {
      await memberClient.post("/auth/register", data);
      setEmail(data.email);
      setStep("otp");
      setResendsLeft(3);
      toast.success("Verification code sent to your email");
    } catch (err) {
      const appliedToField = applyServerFieldErrors<FormData>(err, setError);
      if (!appliedToField) toast.error(handleApiError(err));
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

  /* ── Sub-step navigation ── */
  async function nextFromStep1() {
    const ok = await trigger(["firstName", "lastName", "email", "phone"]);
    if (ok) setFormSubStep(2);
  }
  async function nextFromStep2() {
    const ok = await trigger(["studentId", "graduationYear"]);
    if (ok) setFormSubStep(3);
  }

  return (
    <div className="w-full animate-in fade-in slide-in-from-bottom-4 duration-500">

      <AuthMobileBrand fallbackTagline="Create your alumni account" />

      {/* Step indicator */}
      <StepIndicator step={step} />

      {/* ══════════════════════════════════════════════
          STEP: FORM
      ══════════════════════════════════════════════ */}
      {step === "form" && (
        <>
          {/* Heading */}
          <div className="mb-6">
            <p className="text-[11px] font-bold tracking-[0.12em] uppercase mb-2" style={{ color: "var(--primary)" }}>
              {formSubStep === 1 && "Step 1 of 3"}
              {formSubStep === 2 && "Step 2 of 3"}
              {formSubStep === 3 && "Step 3 of 3"}
            </p>
            <h1
              className="font-[family-name:var(--font-display)] mb-1"
              style={{ fontSize: "1.65rem", fontWeight: 700, letterSpacing: "-0.02em", color: "var(--foreground)", lineHeight: 1.2 }}
            >
              {formSubStep === 1 && "Personal details"}
              {formSubStep === 2 && "Alumni information"}
              {formSubStep === 3 && "Secure your account"}
            </h1>
            <p className="text-[13.5px]" style={{ color: "var(--muted-foreground)" }}>
              {formSubStep === 1 && "Start with your basic contact information."}
              {formSubStep === 2 && "Help us verify your connection to your institution."}
              {formSubStep === 3 && "Choose a strong password to protect your account."}
            </p>
          </div>

          {/* Sub-step progress — minimal dots */}
          <div className="flex gap-1.5 mb-6">
            {([1, 2, 3] as const).map((n) => (
              <div
                key={n}
                className={cn(
                  "h-1 rounded-full transition-all duration-300",
                  n < formSubStep  ? "flex-1 bg-primary" :
                  n === formSubStep ? "flex-[2] bg-primary/50" :
                                      "flex-1 bg-border",
                )}
              />
            ))}
          </div>

          <form onSubmit={handleSubmit(onSubmit)}>

            {/* ── Sub-step 1: Personal info ── */}
            {formSubStep === 1 && (
              <div className="space-y-4 animate-in fade-in slide-in-from-right-3 duration-250">
                {SHOW_WORKSPACE_FIELD && (
                  <div className="space-y-1.5">
                    <Label htmlFor="workspace" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                      Workspace (dev only)
                    </Label>
                    <Input
                      id="workspace"
                      placeholder="greenfield"
                      className="h-11 text-[14px]"
                      value={workspaceSlug}
                      onChange={(e) => updateWorkspaceSlug(e.target.value)}
                    />
                    <p className="text-[12px] text-muted-foreground">
                      Institution slug — stands in for real subdomain routing until wildcard DNS is set up.
                    </p>
                  </div>
                )}
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1.5">
                    <Label htmlFor="firstName" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                      First name
                    </Label>
                    <Input id="firstName" placeholder="Kwame" autoFocus error={!!errors.firstName}
                      className="h-11 text-[14px]" {...register("firstName")} />
                    <FieldError message={errors.firstName?.message} />
                  </div>
                  <div className="space-y-1.5">
                    <Label htmlFor="lastName" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                      Last name
                    </Label>
                    <Input id="lastName" placeholder="Mensah" error={!!errors.lastName}
                      className="h-11 text-[14px]" {...register("lastName")} />
                    <FieldError message={errors.lastName?.message} />
                  </div>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="email" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    Email address
                  </Label>
                  <Input id="email" type="email" placeholder="you@example.com" error={!!errors.email}
                    className="h-11 text-[14px]" {...register("email")} />
                  <FieldError message={errors.email?.message} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="phone" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    Mobile number
                  </Label>
                  <PhoneInput id="phone" error={!!errors.phone}
                    value={watch("phone") ?? ""}
                    onChange={(val) => setValue("phone", val, { shouldValidate: true })} />
                  <FieldError message={errors.phone?.message} />
                  <p className="text-[11.5px]" style={{ color: "var(--muted-foreground)" }}>
                    Used to reach you by SMS & WhatsApp for important updates.
                  </p>
                </div>
                <Button type="button" className="w-full text-[14px] font-semibold mt-1" style={{ height: 44 }}
                  onClick={nextFromStep1}>
                  Continue <ChevronRight size={15} className="ml-1" />
                </Button>
              </div>
            )}

            {/* ── Sub-step 2: Alumni info ── */}
            {formSubStep === 2 && (
              <div className="space-y-4 animate-in fade-in slide-in-from-right-3 duration-250">
                <div className="space-y-1.5">
                  <Label htmlFor="studentId" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    Student ID{!requireStudentId && <span className="font-normal text-muted-foreground"> (optional)</span>}
                  </Label>
                  <Input id="studentId" placeholder="ENG/20/0001" autoFocus error={!!errors.studentId}
                    className="h-11 text-[14px]" {...register("studentId")} />
                  <FieldError message={errors.studentId?.message} />
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1.5">
                    <Label className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                      Graduation year
                    </Label>
                    <FormSelect
                      value={watch("graduationYear") ? String(watch("graduationYear")) : ""}
                      onValueChange={(v) => setValue("graduationYear", Number(v) as unknown as number, { shouldValidate: true })}
                      placeholder="Select year"
                      options={graduationYearOptions}
                    />
                    <FieldError message={errors.graduationYear?.message} />
                  </div>
                  {departments.length > 0 && (
                    <div className="space-y-1.5">
                      <Label className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                        Department{" "}
                        <span className="font-normal" style={{ color: "var(--muted-foreground)" }}>(optional)</span>
                      </Label>
                      <FormSelect
                        value={watch("departmentId") ?? ""}
                        onValueChange={(v) => setValue("departmentId", v, { shouldValidate: true })}
                        placeholder="Select"
                        options={departments.map((d) => ({ value: d.id, label: d.name }))}
                      />
                    </div>
                  )}
                </div>
                <div className="flex gap-3 mt-1">
                  <Button type="button" variant="outline" className="flex-1 text-[14px] font-medium" style={{ height: 44 }}
                    onClick={() => setFormSubStep(1)}>
                    <ArrowLeft size={14} className="mr-1.5" /> Back
                  </Button>
                  <Button type="button" className="flex-[2] text-[14px] font-semibold" style={{ height: 44 }}
                    onClick={nextFromStep2}>
                    Continue <ChevronRight size={15} className="ml-1" />
                  </Button>
                </div>
              </div>
            )}

            {/* ── Sub-step 3: Password ── */}
            {formSubStep === 3 && (
              <div className="space-y-4 animate-in fade-in slide-in-from-right-3 duration-250">
                <div className="space-y-1.5">
                  <Label htmlFor="password" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    Password
                  </Label>
                  <PasswordInput
                    id="password"
                    placeholder="Min. 8 characters"
                    show={showPassword}
                    onToggle={() => setShowPassword(s => !s)}
                    registration={register("password")}
                    error={errors.password?.message}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="confirmPassword" className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                    Confirm password
                  </Label>
                  <PasswordInput
                    id="confirmPassword"
                    placeholder="Repeat your password"
                    show={showConfirmPassword}
                    onToggle={() => setShowConfirmPassword(s => !s)}
                    registration={register("confirmPassword")}
                    error={errors.confirmPassword?.message}
                  />
                </div>
                <div className="flex gap-3 mt-1">
                  <Button type="button" variant="outline" className="flex-1 text-[14px] font-medium" style={{ height: 44 }}
                    onClick={() => setFormSubStep(2)}>
                    <ArrowLeft size={14} className="mr-1.5" /> Back
                  </Button>
                  <Button type="submit" className="flex-[2] text-[14px] font-semibold" style={{ height: 44 }}
                    isLoading={isSubmitting} loadingText="Creating account…">
                    Create account
                  </Button>
                </div>
                <p className="text-[12px] text-center" style={{ color: "var(--muted-foreground)" }}>
                  By creating an account, you agree to our{" "}
                  <Link href="/terms" target="_blank" className="font-semibold hover:underline" style={{ color: "var(--primary)" }}>Terms</Link>{" "}
                  and{" "}
                  <Link href="/privacy" target="_blank" className="font-semibold hover:underline" style={{ color: "var(--primary)" }}>Privacy Policy</Link>.
                </p>
              </div>
            )}

          </form>

          <p className="text-center text-[13px] mt-6" style={{ color: "var(--muted-foreground)" }}>
            Already have an account?{" "}
            <Link href="/login" className="font-semibold hover:underline" style={{ color: "var(--primary)" }}>
              Sign in
            </Link>
          </p>
        </>
      )}

      {/* ══════════════════════════════════════════════
          STEP: OTP
      ══════════════════════════════════════════════ */}
      {step === "otp" && (
        <div className="animate-in fade-in slide-in-from-bottom-3 duration-300">
          <div className="mb-7">
            <p className="text-[11px] font-bold tracking-[0.12em] uppercase mb-2" style={{ color: "var(--primary)" }}>
              Step 2 of 3
            </p>
            <h1 className="font-[family-name:var(--font-display)] mb-1"
              style={{ fontSize: "1.65rem", fontWeight: 700, letterSpacing: "-0.02em", color: "var(--foreground)", lineHeight: 1.2 }}>
              Check your email
            </h1>
            <p className="text-[13.5px]" style={{ color: "var(--muted-foreground)" }}>
              We sent a 6-digit code to{" "}
              <span className="font-semibold" style={{ color: "var(--foreground)" }}>{email}</span>.
              Enter it below.
            </p>
          </div>

          {/* OTP inputs */}
          <div className="flex justify-center gap-2 mb-6" onPaste={handleOtpPaste}>
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
                className={cn(
                  "h-12 w-12 rounded-xl border text-center text-xl font-bold transition-all duration-150",
                  "focus:outline-none focus:ring-2 focus:ring-offset-0",
                  digit
                    ? "border-primary bg-primary/5 text-primary"
                    : "border-input bg-background text-foreground focus:border-primary focus:ring-primary/20",
                )}
                style={{ fontVariantNumeric: "tabular-nums" }}
              />
            ))}
          </div>

          <Button
            className="w-full text-[14px] font-semibold mb-4"
            style={{ height: 44 }}
            onClick={verifyOtp}
            isLoading={verifying}
            loadingText="Verifying…"
            disabled={otp.join("").length !== 6}
          >
            Verify &amp; continue
          </Button>

          <div className="text-center text-[13px] mb-5" style={{ color: "var(--muted-foreground)" }}>
            Didn&apos;t receive it?{" "}
            {resendsLeft > 0 ? (
              <button type="button" onClick={resendOtp} disabled={resending}
                className="font-semibold hover:underline disabled:opacity-50 transition-opacity"
                style={{ color: "var(--primary)" }}>
                {resending ? "Sending…" : `Resend (${resendsLeft} left)`}
              </button>
            ) : (
              <span className="font-semibold text-destructive">No resend attempts left</span>
            )}
          </div>

          <button
            type="button"
            onClick={() => { setStep("form"); setFormSubStep(1); setOtp(["", "", "", "", "", ""]); }}
            className="flex items-center gap-1.5 text-[13px] mx-auto transition-colors hover:underline"
            style={{ color: "var(--muted-foreground)" }}
          >
            <ArrowLeft size={13} /> Back to registration
          </button>
        </div>
      )}

      {/* ══════════════════════════════════════════════
          STEP: PENDING
      ══════════════════════════════════════════════ */}
      {step === "pending" && (
        <div className="animate-in fade-in slide-in-from-bottom-3 duration-300 space-y-5">
          <div className="mb-2">
            <h1 className="font-[family-name:var(--font-display)] mb-1"
              style={{ fontSize: "1.65rem", fontWeight: 700, letterSpacing: "-0.02em", color: "var(--foreground)", lineHeight: 1.2 }}>
              You&apos;re almost in.
            </h1>
            <p className="text-[13.5px]" style={{ color: "var(--muted-foreground)" }}>
              Email verified. Complete the step below to speed up approval.
            </p>
          </div>

          {/* Status card */}
          <div className="rounded-xl border divide-y" style={{ borderColor: "var(--border)" }}>
            <div className="flex items-center gap-3 p-4">
              <div className="shrink-0 w-8 h-8 rounded-full flex items-center justify-center bg-success/10 border border-success/25">
                <CheckCircle2 size={16} className="text-success" />
              </div>
              <div>
                <p className="text-[13.5px] font-semibold" style={{ color: "var(--foreground)" }}>Email verified</p>
                <p className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>{email}</p>
              </div>
            </div>
            <div className="flex items-center gap-3 p-4">
              <div className="shrink-0 w-8 h-8 rounded-full flex items-center justify-center bg-warning/10 border border-warning/25">
                <Clock size={16} className="text-warning" />
              </div>
              <div>
                <p className="text-[13.5px] font-semibold" style={{ color: "var(--foreground)" }}>Awaiting admin review</p>
                <p className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>Usually within 24 hours</p>
              </div>
            </div>
          </div>

          {/* Membership activation */}
          <div>
            <p className="text-[13px] font-semibold mb-1" style={{ color: "var(--foreground)" }}>
              Activate your membership
            </p>
            <p className="text-[12.5px] mb-3" style={{ color: "var(--muted-foreground)" }}>
              Paying the activation fee confirms your alumni status and helps us approve you faster.
            </p>
            <MembershipCampaignSection email={email} />
          </div>

          {/* Footer actions */}
          <div className="flex flex-col sm:flex-row gap-3 pt-1">
            <Button variant="outline" className="flex-1 text-[13.5px] font-medium" style={{ height: 42 }}
              onClick={() => { setStep("form"); setFormSubStep(1); setOtp(["", "", "", "", "", ""]); }}>
              Register another account
            </Button>
            <Button className="flex-1 text-[13.5px] font-semibold" style={{ height: 42 }}
              onClick={() => router.push("/login")}>
              Go to sign in
            </Button>
          </div>
        </div>
      )}

    </div>
  );
}

export default function RegisterPage() {
  return (
    <Suspense fallback={
      <div className="w-full flex items-center justify-center py-20">
        <Loader2 size={26} className="animate-spin" style={{ color: "var(--primary)" }} />
      </div>
    }>
      <RegisterForm />
    </Suspense>
  );
}
