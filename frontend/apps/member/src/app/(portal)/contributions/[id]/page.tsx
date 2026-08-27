"use client";

import { useState } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import { useParams, useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import {
  CreditCard, Loader2, ArrowLeft, Calendar, Target,
  Users, CheckCircle2, Award, Copy, Check,
  Share2, ExternalLink, AlertCircle,
} from "lucide-react";
import Link from "next/link";
import { toast } from "sonner";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Progress } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { YouTubeEmbed } from "@alumni/ui";
import { formatCurrency, formatDate, cn } from "@alumni/ui";
import {
  getCampaignById, getMyProfile,
  initiatePaystackPayment, renewMembership, getMyContributions,
} from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { EmptyState } from "@alumni/ui";
import type { Campaign } from "@/types";
import { PaymentRedirectOverlay } from "@/components/member/payment-redirect-overlay";

export default function CampaignDetailPage() {
  useAuth();
  const { id } = useParams() as { id: string };
  const router = useRouter();
  const [amount, setAmount]         = useState("");
  const [preset, setPreset]         = useState<number | null>(null);
  const [shareCopied, setShareCopied] = useState(false);
  const [redirecting, setRedirecting] = useState(false);

  const { data: campaign, isLoading } = useQuery({
    queryKey: ["campaign", id],
    queryFn:  () => getCampaignById(id),
  });

  const { data: myContributions } = useQuery({
    queryKey: ["m-contributions", { campaignId: id }],
    queryFn:  () => getMyContributions({ campaignId: id }),
  });

  const { data: profile } = useQuery({
    queryKey: ["m-profile"],
    queryFn:  getMyProfile,
  });

  const payMut = useMutation({
    mutationFn: (payAmount: number) => {
      // Built client-side so the Paystack redirect lands back on THIS
      // institution's own subdomain, not the backend's shared fallback host.
      const callbackUrl = `${window.location.origin}/contributions/callback`;
      return campaign?.isMembershipCampaign
        ? renewMembership(id, 1, "online", callbackUrl)
        : initiatePaystackPayment({ campaignId: id, amount: payAmount, callbackUrl });
    },
    onSuccess: (data: { authorizationUrl?: string }) => {
      if (data?.authorizationUrl) {
        window.location.href = data.authorizationUrl;
      } else {
        setRedirecting(false);
        toast.success("Payment initiated.");
      }
    },
    onError: (e) => { setRedirecting(false); toast.error(handleApiError(e)); },
  });

  /* ── Loading ── */
  if (isLoading) {
    return (
      <div className="p-6 lg:p-10 max-w-5xl mx-auto space-y-6 animate-pulse">
        <div className="h-6 w-28 rounded-lg" style={{ background: "var(--secondary)" }} />
        <div className="h-64 w-full rounded-2xl" style={{ background: "var(--secondary)" }} />
        <div className="space-y-3">
          <div className="h-8 w-2/3 rounded-lg" style={{ background: "var(--secondary)" }} />
          <div className="h-4 w-full rounded-lg" style={{ background: "var(--secondary)" }} />
          <div className="h-4 w-4/5 rounded-lg" style={{ background: "var(--secondary)" }} />
        </div>
      </div>
    );
  }

  if (!campaign) return (
    <div className="p-6 lg:p-10 max-w-5xl mx-auto">
      <Button variant="ghost" size="sm" className="mb-6 font-semibold gap-2" onClick={() => router.push("/contributions")}>
        <ArrowLeft size={15} /> Back
      </Button>
      <EmptyState
        icon={<CreditCard size={40} />}
        title="Not found"
        description="This may have ended or the link is incorrect."
      />
    </div>
  );

  /* ── Derived values ── */
  const hasPaid        = myContributions?.results.some(c => c.status === "Confirmed" || c.status === "Pending");
  const isMembership   = !!campaign.isMembershipCampaign;
  const membershipPaid = isMembership && hasPaid;
  const isPensioner    = profile?.employmentStatus === "Pensioner";
  const memberAmount   = isPensioner && campaign.pensionerAmountPerMember != null
    ? campaign.pensionerAmountPerMember : campaign.amountPerMember;

  const pct = isMembership && campaign.totalEligibleMembers
    ? Math.round((campaign.paidCount / campaign.totalEligibleMembers) * 100)
    : campaign.targetAmount > 0
      ? Math.round((campaign.collectedAmount / campaign.targetAmount) * 100) : 0;

  const displayAmount  = isMembership ? String(memberAmount) : (amount || String(campaign.amountPerMember ?? ""));
  const numericAmount  = Number(displayAmount);
  const isValidAmount  = numericAmount > 0;
  const isActive       = campaign.status === "Active";
  const canPay         = isActive && isValidAmount && !membershipPaid;

  const publicLink = typeof window !== "undefined"
    ? `${window.location.origin}/payment-campaign/${id}`
    : `/payment-campaign/${id}`;

  function copyLink() {
    void navigator.clipboard.writeText(publicLink);
    setShareCopied(true);
    toast.success("Link copied to clipboard.");
    setTimeout(() => setShareCopied(false), 2500);
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto pb-20">

      {/* ── Nav row ── */}
      <div className="flex items-center justify-between mb-6 sm:mb-8">
        <button
          onClick={() => router.push("/contributions")}
          className="flex items-center gap-1.5 text-[13.5px] font-semibold transition-colors hover:underline"
          style={{ color: "var(--muted-foreground)" }}
        >
          <ArrowLeft size={15} /> Back
        </button>
        <Badge variant={isActive ? "success" : "secondary"} className="text-[11px] font-bold uppercase tracking-wide">
          {campaign.status}
        </Badge>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 lg:gap-12">

        {/* ════════════════════════════════════════════
            LEFT — main content
        ════════════════════════════════════════════ */}
        <div className="lg:col-span-7 space-y-8">

          {/* Title + meta */}
          <div>
            {isMembership && (
              <p className="text-[11px] font-bold tracking-[0.12em] uppercase mb-2" style={{ color: "var(--primary)" }}>
                Membership dues
              </p>
            )}
            <h1
              className="font-[family-name:var(--font-display)] leading-tight mb-4"
              style={{ fontSize: "clamp(1.5rem, 3.5vw, 2.25rem)", fontWeight: 700, letterSpacing: "-0.02em", color: "var(--foreground)" }}
            >
              {campaign.title}
            </h1>
            <div className="flex flex-wrap gap-x-5 gap-y-2">
              {[
                { icon: Calendar, text: `Closes ${formatDate(campaign.deadline)}` },
                { icon: Target,   text: `Goal: ${formatCurrency(campaign.targetAmount)}` },
                { icon: Users,    text: `${campaign.paidCount} contributor${campaign.paidCount !== 1 ? "s" : ""}` },
              ].map(({ icon: Icon, text }) => (
                <div key={text} className="flex items-center gap-1.5 text-[13.5px]" style={{ color: "var(--muted-foreground)" }}>
                  <Icon size={14} style={{ color: "var(--primary)" }} />
                  {text}
                </div>
              ))}
            </div>
          </div>

          {/* Banner / video */}
          <div className="rounded-2xl overflow-hidden border" style={{ borderColor: "var(--border)" }}>
            {campaign.youtubeVideoUrl ? (
              <YouTubeEmbed url={campaign.youtubeVideoUrl} />
            ) : campaign.bannerImageUrl ? (
              <img
                src={campaign.bannerImageUrl}
                alt={campaign.title}
                className="w-full object-cover"
                style={{ maxHeight: 420 }}
              />
            ) : (
              <div
                className="flex items-center justify-center"
                style={{ height: 240, background: "var(--secondary)" }}
              >
                <CreditCard size={48} style={{ color: "var(--muted-foreground)", opacity: 0.25 }} />
              </div>
            )}
          </div>

          {/* Progress — shown in main content on mobile, sidebar on desktop */}
          <div className="lg:hidden">
            <ProgressBlock campaign={campaign} isMembership={isMembership} pct={pct} />
          </div>

          {/* Description */}
          <div>
            <h2
              className="font-[family-name:var(--font-display)] mb-4 pb-3 border-b"
              style={{ fontSize: "1.1rem", fontWeight: 700, color: "var(--foreground)", borderColor: "var(--border)" }}
            >
              {isMembership ? "About these dues" : "About this fundraiser"}
            </h2>
            <p
              className="whitespace-pre-wrap leading-[1.85]"
              style={{ fontSize: "0.9625rem", color: "var(--muted-foreground)" }}
            >
              {campaign.description || `No details have been provided for this ${isMembership ? "membership dues item" : "fundraiser"} yet. Please contact the alumni office for more information.`}
            </p>
          </div>

          {/* Share link */}
          <div
            className="rounded-2xl border p-4 sm:p-5"
            style={{ borderColor: "var(--color-border-info)", background: "var(--color-background-info)" }}
          >
            <div className="flex items-center gap-2.5 mb-3">
              <div
                className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0"
                style={{ background: "var(--background)", border: "1px solid var(--color-border-info)" }}
              >
                <Share2 size={14} style={{ color: "var(--primary)" }} />
              </div>
              <div>
                <p className="text-[13.5px] font-semibold" style={{ color: "var(--foreground)" }}>
                  Share this {isMembership ? "dues link" : "fundraiser"}
                </p>
                <p className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>
                  Anyone with this link can contribute — no sign-in needed
                </p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <div
                className="flex-1 flex items-center h-9 rounded-lg border px-3 gap-2 min-w-0 overflow-hidden"
                style={{ background: "var(--background)", borderColor: "var(--border)" }}
              >
                <ExternalLink size={11} style={{ color: "var(--muted-foreground)" }} className="shrink-0" />
                <span className="text-[11.5px] font-mono truncate" style={{ color: "var(--muted-foreground)" }}>
                  {publicLink}
                </span>
              </div>
              <Button
                size="sm"
                variant={shareCopied ? "default" : "outline"}
                className={cn("shrink-0 gap-1.5 font-semibold text-[12.5px]", shareCopied && "bg-success hover:bg-success border-success text-white")}
                style={{ height: 36 }}
                onClick={copyLink}
              >
                {shareCopied ? <><Check size={12} /> Copied</> : <><Copy size={12} /> Copy</>}
              </Button>
              <a href={publicLink} target="_blank" rel="noreferrer">
                <Button size="sm" variant="ghost" className="shrink-0 gap-1 font-semibold text-[12.5px]"
                  style={{ height: 36, color: "var(--primary)" }}>
                  <ExternalLink size={12} /> Preview
                </Button>
              </a>
            </div>
          </div>

        </div>

        {/* ════════════════════════════════════════════
            RIGHT — sticky sidebar
        ════════════════════════════════════════════ */}
        <aside className="lg:col-span-5">
          <div
            className="rounded-2xl border overflow-hidden lg:sticky lg:top-24"
            style={{ borderColor: "var(--border)", background: "var(--background)" }}
          >
            {/* Top accent */}
            <div className="h-1 w-full" style={{ background: "var(--primary)" }} />

            <div className="p-5 sm:p-6 space-y-6">

              {/* Progress */}
              <div className="hidden lg:block">
                <ProgressBlock campaign={campaign} isMembership={isMembership} pct={pct} />
              </div>

              {/* Amount + deadline info row */}
              <div
                className="grid grid-cols-2 divide-x rounded-xl overflow-hidden"
                style={{ background: "var(--secondary)", borderColor: "var(--border)", border: "1px solid var(--border)" }}
              >
                <div className="p-4">
                  <p className="text-[11px] font-semibold uppercase tracking-wide mb-1" style={{ color: "var(--muted-foreground)" }}>
                    {isMembership ? "Your amount" : "Per member"}
                  </p>
                  <p
                    className="font-[family-name:var(--font-display)] font-bold tabular-nums"
                    style={{ fontSize: "1.25rem", color: "var(--foreground)", letterSpacing: "-0.02em" }}
                  >
                    {formatCurrency(memberAmount)}
                  </p>
                  {isPensioner && isMembership && (
                    <p className="text-[11px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>Pensioner rate</p>
                  )}
                </div>
                <div className="p-4">
                  <p className="text-[11px] font-semibold uppercase tracking-wide mb-1" style={{ color: "var(--muted-foreground)" }}>
                    Deadline
                  </p>
                  <p className="text-[15px] font-semibold" style={{ color: "var(--foreground)" }}>
                    {formatDate(campaign.deadline)}
                  </p>
                  <DaysLeft deadline={campaign.deadline} />
                </div>
              </div>

              {/* Membership paid state */}
              {membershipPaid && (
                <div className="flex items-center gap-3 p-4 rounded-xl bg-success/10 border border-success/20">
                  <CheckCircle2 size={18} className="text-success shrink-0" />
                  <div>
                    <p className="text-[14px] font-semibold text-success">Membership paid — thank you!</p>
                    <p className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>
                      This is a one-time payment per membership year.
                    </p>
                  </div>
                </div>
              )}

              {/* Certificate link */}
              {membershipPaid && (
                <Link href="/membership-certificate">
                  <Button variant="outline" className="w-full gap-2 font-semibold text-[13.5px] border-warning/50 text-warning hover:bg-warning/10"
                    style={{ height: 42 }}>
                    <Award size={15} /> View membership certificate
                  </Button>
                </Link>
              )}

              {/* Already contributed notice (non-membership) */}
              {hasPaid && !isMembership && (
                <div
                  className="flex items-center gap-2 px-3 py-2.5 rounded-lg"
                  style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
                >
                  <CheckCircle2 size={14} style={{ color: "var(--primary)" }} className="shrink-0" />
                  <p className="text-[13px] font-semibold" style={{ color: "var(--color-text-info)" }}>
                    You&apos;ve contributed to this fundraiser before.
                  </p>
                </div>
              )}

              {/* Amount input — non-membership only */}
              {!isMembership && isActive && (
                <div className="space-y-3">
                  <p className="text-[12px] font-bold uppercase tracking-wide" style={{ color: "var(--muted-foreground)" }}>
                    Choose an amount (GHS)
                  </p>
                  {/* Preset buttons */}
                  <div className="grid grid-cols-3 gap-2">
                    {([1, 2, 5] as const).map((multiplier) => {
                      const val = multiplier * campaign.amountPerMember;
                      const active = preset === multiplier;
                      return (
                        <button
                          key={multiplier}
                          onClick={() => { setPreset(multiplier); setAmount(String(val)); }}
                          className={cn(
                            "rounded-xl py-2.5 text-[13px] font-semibold border transition-colors",
                            active
                              ? "text-white border-transparent"
                              : "border-border hover:border-primary/40",
                          )}
                          style={{
                            background: active ? "var(--primary)" : "var(--background)",
                            color: active ? "white" : "var(--foreground)",
                          }}
                        >
                          <span className="block text-[10px] mb-0.5 font-bold opacity-60">
                            {multiplier}×
                          </span>
                          {formatCurrency(val)}
                        </button>
                      );
                    })}
                  </div>
                  <Input
                    type="number"
                    inputMode="decimal"
                    min={0.01}
                    step="0.01"
                    value={displayAmount}
                    onChange={e => { setPreset(null); setAmount(e.target.value); }}
                    className="h-12 text-[16px] font-semibold"
                    placeholder={`Min. ${formatCurrency(campaign.amountPerMember)}`}
                  />
                </div>
              )}

              {/* Pay button */}
              {isActive && (
                <Button
                  className="w-full font-bold text-[15px] gap-2"
                  style={{ height: 48 }}
                  disabled={!canPay || payMut.isPending || redirecting}
                  onClick={() => { setRedirecting(true); payMut.mutate(numericAmount); }}
                >
                  {payMut.isPending ? (
                    <><Loader2 size={16} className="animate-spin" /> Processing…</>
                  ) : membershipPaid ? (
                    <><CheckCircle2 size={16} /> Already paid</>
                  ) : (
                    <><CreditCard size={16} />
                      {isMembership
                        ? `Pay ${formatCurrency(memberAmount)}`
                        : hasPaid ? `Contribute again` : `Pay ${formatCurrency(numericAmount || campaign.amountPerMember)}`}
                    </>
                  )}
                </Button>
              )}

              {!isActive && (
                <div
                  className="flex items-center gap-2.5 p-3.5 rounded-xl"
                  style={{ background: "var(--secondary)", border: "1px solid var(--border)" }}
                >
                  <AlertCircle size={16} style={{ color: "var(--muted-foreground)" }} className="shrink-0" />
                  <p className="text-[13.5px]" style={{ color: "var(--muted-foreground)" }}>
                    This {isMembership ? "membership dues item" : "fundraiser"} is no longer accepting contributions.
                  </p>
                </div>
              )}

              <p className="text-center text-[12px]" style={{ color: "var(--muted-foreground)", opacity: 0.6 }}>
                Payments are processed securely online
              </p>

              {/* Manual payment instructions */}
              {campaign.allowManualPayments && (
                <div
                  className="rounded-xl border p-4 space-y-3"
                  style={{ borderColor: "var(--border)", background: "var(--secondary)" }}
                >
                  <p className="text-[13px] font-bold" style={{ color: "var(--foreground)" }}>
                    Pay manually
                  </p>
                  <p className="text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>
                    Use your member number as the payment reference:{" "}
                    <span className="font-bold font-mono" style={{ color: "var(--foreground)" }}>
                      {profile?.memberNumber ?? "—"}
                    </span>
                  </p>

                  {campaign.bankAccount && (
                    <div className="space-y-1 pt-2 border-t" style={{ borderColor: "var(--border)" }}>
                      <p className="text-[12px] font-bold uppercase tracking-wide" style={{ color: "var(--muted-foreground)" }}>Bank transfer</p>
                      {[
                        ["Account name",   campaign.bankAccount.accountName],
                        ["Account number", campaign.bankAccount.accountNumber],
                        ["Bank",           campaign.bankAccount.bankName],
                        ["Branch",         campaign.bankAccount.branch],
                      ].map(([label, value]) => value && (
                        <div key={label} className="flex items-center justify-between gap-3">
                          <span className="text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>{label}</span>
                          <span className="text-[12.5px] font-semibold text-right" style={{ color: "var(--foreground)" }}>{value}</span>
                        </div>
                      ))}
                    </div>
                  )}

                  {campaign.mobileMoneyAccount && (
                    <div className="space-y-1 pt-2 border-t" style={{ borderColor: "var(--border)" }}>
                      <p className="text-[12px] font-bold uppercase tracking-wide" style={{ color: "var(--muted-foreground)" }}>Mobile money</p>
                      {[
                        ["Provider", campaign.mobileMoneyAccount.provider],
                        ["Name",     campaign.mobileMoneyAccount.name],
                        ["Number",   campaign.mobileMoneyAccount.mobileMoneyNumber],
                      ].map(([label, value]) => value && (
                        <div key={label} className="flex items-center justify-between gap-3">
                          <span className="text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>{label}</span>
                          <span className="text-[12.5px] font-semibold text-right" style={{ color: "var(--foreground)" }}>{value}</span>
                        </div>
                      ))}
                    </div>
                  )}

                  {!campaign.bankAccount && !campaign.mobileMoneyAccount && (
                    <p className="text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>
                      No account details configured yet. Contact the alumni office.
                    </p>
                  )}
                </div>
              )}

            </div>
          </div>
        </aside>

      </div>

      <PaymentRedirectOverlay
        visible={redirecting}
        campaignTitle={campaign.title}
        amount={isMembership ? memberAmount : numericAmount}
      />
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   PROGRESS BLOCK — shared between mobile (main) and desktop (sidebar)
   ───────────────────────────────────────────────────────────────────────── */
function ProgressBlock({ campaign, isMembership, pct }: {
  campaign: Campaign; isMembership: boolean; pct: number;
}) {
  return (
    <div className="space-y-2">
      <div className="flex items-end justify-between gap-2">
        <p
          className="font-[family-name:var(--font-display)] font-bold tabular-nums leading-none"
          style={{ fontSize: "2rem", color: "var(--foreground)", letterSpacing: "-0.03em" }}
        >
          {pct}%
        </p>
        <p className="text-[12px] font-semibold mb-1" style={{ color: "var(--muted-foreground)" }}>
          {isMembership ? "members paid" : "of goal reached"}
        </p>
      </div>
      <Progress value={pct} className="h-2.5" />
      <div className="flex items-center justify-between text-[12.5px]" style={{ color: "var(--muted-foreground)" }}>
        <span className="font-semibold" style={{ color: "var(--primary)" }}>
          {isMembership
            ? `${campaign.paidCount} paid`
            : formatCurrency(campaign.collectedAmount)}
        </span>
        <span>
          {isMembership
            ? `${campaign.totalEligibleMembers ?? "?"} eligible`
            : `Goal: ${formatCurrency(campaign.targetAmount)}`}
        </span>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   DAYS LEFT — small plain-language countdown
   ───────────────────────────────────────────────────────────────────────── */
function DaysLeft({ deadline }: { deadline: string }) {
  const [now] = useState(() => Date.now());
  const days = Math.ceil((new Date(deadline).getTime() - now) / 86_400_000);
  if (days < 0) return <p className="text-[11.5px] text-destructive mt-0.5 font-medium">Closed</p>;
  if (days === 0) return <p className="text-[11.5px] text-destructive mt-0.5 font-medium">Closes today</p>;
  if (days <= 7)  return <p className="text-[11.5px] mt-0.5 font-medium text-warning">{days} day{days !== 1 ? "s" : ""} left</p>;
  return <p className="text-[11.5px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>{days} days left</p>;
}
