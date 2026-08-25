"use client";

import { useState, useSyncExternalStore, Suspense } from "react";
import { useParams, useSearchParams } from "next/navigation";
import { useQuery, useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  CheckCircle2,
  CreditCard,
  Lock,
  AlertCircle,
  Loader2,
  ShieldCheck,
  Calendar,
  Users,
} from "lucide-react";
import Link from "next/link";

import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { getCampaignById, initiatePaystackPaymentGuest } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { formatCurrency, formatDate } from "@alumni/ui";

// Hydration-safe read of the browser origin (see useHostname()): server and
// the first client render both see "", avoiding a mismatch, then React syncs
// to the real value right after mount.
function useOrigin() {
  return useSyncExternalStore(
    () => () => {},
    () => window.location.origin,
    () => ""
  );
}

function ActivateMembershipContent() {
  const { campaignId } = useParams() as { campaignId: string };
  const searchParams = useSearchParams();
  const emailFromUrl = searchParams.get("email") ?? "";

  const [email] = useState(emailFromUrl);
  const callbackOrigin = useOrigin();
  const [now] = useState(() => Date.now());

  const { data: campaign, isLoading, isError } = useQuery({
    queryKey: ["campaign", campaignId],
    queryFn: () => getCampaignById(campaignId),
    enabled: Boolean(campaignId),
  });

  const payMutation = useMutation({
    mutationFn: () => {
      const callbackUrl = `${callbackOrigin}/activate-membership/callback`;
      return initiatePaystackPaymentGuest({
        campaignId,
        amount: campaign?.amountPerMember ?? 0,
        email,
        callbackUrl,
      });
    },
    onSuccess: (result: { authorizationUrl: string; reference: string }) => {
      toast.success("Redirecting to secure checkout…");
      setTimeout(() => {
        window.location.href = result.authorizationUrl;
      }, 400);
    },
    onError: (error) => {
      toast.error("Payment initiation failed", { description: handleApiError(error) });
    },
  });

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background">
        <div className="flex flex-col items-center gap-4 text-muted-foreground">
          <Loader2 size={32} className="animate-spin text-primary" />
          <p className="text-sm">Loading membership dues details…</p>
        </div>
      </div>
    );
  }

  if (isError || !campaign) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background p-6">
        <div className="max-w-sm w-full text-center space-y-4">
          <div className="w-14 h-14 bg-destructive/10 rounded-2xl flex items-center justify-center mx-auto">
            <AlertCircle size={28} className="text-destructive" />
          </div>
          <h2 className="text-xl font-bold text-foreground">Not found</h2>
          <p className="text-sm text-muted-foreground">
            These membership dues may no longer be active or the link is invalid.
          </p>
          <Link
            href="/login"
            className="inline-block px-6 py-2.5 rounded-xl bg-primary text-primary-foreground text-sm font-semibold transition hover:bg-primary/90"
          >
            Go to login
          </Link>
        </div>
      </div>
    );
  }

  const daysLeft = Math.max(
    0,
    Math.ceil((new Date(campaign.deadline).getTime() - now) / 86_400_000),
  );
  const isExpired = daysLeft === 0;
  const canPay = campaign.allowOnlinePayments && !isExpired && !payMutation.isPending && Boolean(email);

  return (
    <div className="min-h-screen bg-background">
      {/* Top bar */}
      <header className="bg-card border-b border-border px-4 sm:px-[6vw] h-[76px] flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 bg-primary rounded-xl flex items-center justify-center flex-shrink-0">
            <span className="text-xs font-bold text-primary-foreground">UM</span>
          </div>
          <span className="font-[family-name:var(--font-display)] font-semibold text-sm text-foreground">
            Alumni Portal
          </span>
        </div>
        <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
          <ShieldCheck size={13} className="text-success" />
          <span className="hidden sm:inline">Secure payment ·</span>
          <Link href="/login" className="text-primary hover:underline font-semibold">
            Already have an account? Sign in
          </Link>
        </div>
      </header>

      <main className="max-w-5xl mx-auto px-4 sm:px-6 py-8 sm:py-10">
        <div className="grid grid-cols-1 lg:grid-cols-[1.3fr_1fr] gap-6 items-stretch">

          {/* Membership dues panel */}
          <section className="rounded-[22px] bg-primary text-primary-foreground p-7 sm:p-10 flex flex-col">
            {campaign.bannerImageUrl && (
              <div className="relative -mx-7 sm:-mx-10 -mt-7 sm:-mt-10 mb-6 h-36 sm:h-44 overflow-hidden rounded-t-[22px]">
                <img
                  src={campaign.bannerImageUrl}
                  alt={campaign.title}
                  className="w-full h-full object-cover"
                />
                <div className="absolute inset-0 bg-gradient-to-t from-primary/60 to-transparent" />
              </div>
            )}
            <span className="inline-block self-start rounded-full px-3 py-1.5 bg-white/15 text-xs font-semibold">
              Membership {campaign.membershipYear ?? ""}
            </span>
            <h1 className="font-[family-name:var(--font-display)] text-[28px] sm:text-[38px] font-bold leading-tight my-4">
              {campaign.title}
            </h1>
            {campaign.description && (
              <p className="text-primary-foreground/85 leading-relaxed line-clamp-3">
                {campaign.description}
              </p>
            )}

            <div className="flex flex-wrap gap-6 sm:gap-8 mt-7">
              <div>
                <strong className="block text-lg sm:text-xl font-bold">
                  {formatCurrency(campaign.amountPerMember)}
                </strong>
                <span className="text-xs text-primary-foreground/70">Amount per member</span>
              </div>
              <div className="flex items-center gap-1">
                <Calendar size={13} className="opacity-70" />
                <div>
                  <strong className="block text-lg sm:text-xl font-bold">{daysLeft} days</strong>
                  <span className="text-xs text-primary-foreground/70">Remaining</span>
                </div>
              </div>
              <div className="flex items-center gap-1">
                <Users size={13} className="opacity-70" />
                <div>
                  <strong className="block text-lg sm:text-xl font-bold">{campaign.paidCount}</strong>
                  <span className="text-xs text-primary-foreground/70">Paid members</span>
                </div>
              </div>
            </div>

            {isExpired && (
              <div className="flex items-center gap-2 px-3 py-2 mt-6 rounded-xl bg-white/10 text-xs font-medium">
                <AlertCircle size={13} />
                These dues are closed. Contact the alumni office for assistance.
              </div>
            )}
          </section>

          {/* Payment card */}
          <section className="rounded-[22px] border border-border bg-card p-6 sm:p-7 space-y-5">
            <div>
              <h2 className="font-[family-name:var(--font-display)] text-xl sm:text-[25px] font-semibold text-foreground">
                Activate your membership
              </h2>
              <p className="text-sm text-muted-foreground mt-1">What happens after payment?</p>
            </div>

            <ul className="space-y-2.5">
              {[
                "Your account is automatically activated",
                "A unique alumni membership number is assigned to you",
                "You gain full access to the alumni portal",
                "Your membership status is immediately confirmed",
              ].map((item) => (
                <li key={item} className="flex items-start gap-2.5 text-xs text-muted-foreground">
                  <CheckCircle2 size={14} className="text-success flex-shrink-0 mt-0.5" />
                  {item}
                </li>
              ))}
            </ul>

            {daysLeft > 0 && daysLeft <= 7 && (
              <div className="flex items-center gap-2 px-3 py-2 rounded-xl bg-warning/10 border border-warning/20 text-warning text-xs font-medium">
                <AlertCircle size={13} />
                Only {daysLeft} day{daysLeft === 1 ? "" : "s"} left to activate
              </div>
            )}

            {/* Email — readonly */}
            <div className="space-y-1.5">
              <Label htmlFor="email" className="text-xs font-semibold">
                Your email address
              </Label>
              <div className="relative">
                <Input
                  id="email"
                  type="email"
                  value={email}
                  readOnly
                  className="h-12 pr-10 bg-muted/40 text-muted-foreground cursor-not-allowed select-all"
                  aria-label="email address"
                />
                <Lock
                  size={14}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground/60"
                />
              </div>
              <p className="text-[11px] text-muted-foreground">
                This is the email you registered with. Payment will be linked to your account.
              </p>
            </div>

            {/* Amount — readonly */}
            <div className="space-y-1.5">
              <Label className="text-xs font-semibold">Amount to pay (GHS)</Label>
              <Input
                type="text"
                value={formatCurrency(campaign.amountPerMember)}
                readOnly
                className="h-12 bg-muted/40 text-foreground font-semibold cursor-not-allowed"
              />
              <p className="text-[11px] text-muted-foreground">
                Fixed membership activation fee for {campaign.membershipYear ?? "this year"}.
              </p>
            </div>

            {!campaign.allowOnlinePayments && (
              <div className="flex items-start gap-2 px-3 py-3 rounded-xl bg-warning/10 border border-warning/20 text-warning text-xs">
                <AlertCircle size={14} className="flex-shrink-0 mt-0.5" />
                <span>Online payments are currently disabled for these dues. Please contact the alumni office.</span>
              </div>
            )}

            <Button
              className="w-full h-14 text-base font-bold gap-2 rounded-xl shadow"
              disabled={!canPay}
              onClick={() => payMutation.mutate()}
            >
              {payMutation.isPending ? (
                <>
                  <Loader2 size={18} className="animate-spin" />
                  Connecting to secure payment…
                </>
              ) : (
                <>
                  <CreditCard size={18} />
                  Pay {formatCurrency(campaign.amountPerMember)} online
                </>
              )}
            </Button>

            <div className="flex items-center justify-center gap-2 text-[11px] text-muted-foreground">
              <ShieldCheck size={13} className="text-success" />
              256-bit encrypted · No card details stored
            </div>
          </section>
        </div>
      </main>
    </div>
  );
}

export default function ActivateMembershipPage() {
  return (
    <Suspense
      fallback={
        <div className="min-h-screen flex items-center justify-center">
          <Loader2 size={32} className="animate-spin text-primary" />
        </div>
      }
    >
      <ActivateMembershipContent />
    </Suspense>
  );
}
