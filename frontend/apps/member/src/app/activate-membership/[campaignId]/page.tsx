"use client";

import { useEffect, useState, Suspense } from "react";
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

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getCampaignById, initiatePaystackPaymentGuest } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { formatCurrency, formatDate } from "@/lib/utils";

function ActivateMembershipContent() {
  const { campaignId } = useParams() as { campaignId: string };
  const searchParams = useSearchParams();
  const emailFromUrl = searchParams.get("email") ?? "";

  const [email] = useState(emailFromUrl);
  const [callbackOrigin, setCallbackOrigin] = useState("");

  useEffect(() => {
    if (typeof window !== "undefined") {
      setCallbackOrigin(window.location.origin);
    }
  }, []);

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
      toast.success("Redirecting to Paystack checkout…");
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
          <p className="text-sm">Loading campaign details…</p>
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
          <h2 className="text-xl font-bold text-foreground">Campaign not found</h2>
          <p className="text-sm text-muted-foreground">
            This membership campaign may no longer be active or the link is invalid.
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
    Math.ceil((new Date(campaign.deadline).getTime() - Date.now()) / 86_400_000),
  );
  const isExpired = daysLeft === 0;
  const canPay = campaign.allowOnlinePayments && !isExpired && !payMutation.isPending && Boolean(email);

  return (
    <div className="min-h-screen bg-gradient-to-b from-primary/5 via-background to-background">
      {/* Top bar */}
      <header className="sticky top-0 z-10 bg-background/80 backdrop-blur border-b border-border/50 px-4 py-3">
        <div className="max-w-lg mx-auto flex items-center gap-3">
          <div className="w-8 h-8 bg-primary rounded-xl flex items-center justify-center flex-shrink-0">
            <span className="text-xs font-bold text-primary-foreground">UM</span>
          </div>
          <span className="font-semibold text-sm text-foreground">UMaT Alumni</span>
          <div className="ml-auto flex items-center gap-1.5 text-xs text-muted-foreground">
            <ShieldCheck size={13} className="text-green-500" />
            Secured by Paystack
          </div>
        </div>
      </header>

      <main className="max-w-lg mx-auto px-4 py-8 space-y-6">
        {/* Hero card */}
        <div className="rounded-2xl overflow-hidden border border-primary/20 bg-card shadow-sm">
          {campaign.bannerImageUrl && (
            <div className="relative w-full h-36 sm:h-44 overflow-hidden">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={campaign.bannerImageUrl}
                alt={campaign.title}
                className="w-full h-full object-cover"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-black/50 to-transparent" />
            </div>
          )}
          <div className="p-5 space-y-4">
            <div>
              <p className="text-[10px] font-semibold text-primary/80 uppercase tracking-widest mb-1">
                Membership Activation {campaign.membershipYear ?? ""}
              </p>
              <h1 className="text-xl font-bold text-foreground leading-snug">{campaign.title}</h1>
            </div>

            {campaign.description && (
              <p className="text-sm text-muted-foreground leading-relaxed line-clamp-3">
                {campaign.description}
              </p>
            )}

            <div className="grid grid-cols-3 gap-3">
              <div className="rounded-xl bg-primary/5 border border-primary/15 p-3 text-center">
                <p className="text-[10px] text-muted-foreground mb-1">Amount</p>
                <p className="text-base font-extrabold text-primary leading-none">
                  {formatCurrency(campaign.amountPerMember)}
                </p>
              </div>
              <div className="rounded-xl bg-muted/40 border border-border/60 p-3 text-center">
                <p className="text-[10px] text-muted-foreground mb-1 flex items-center justify-center gap-0.5">
                  <Calendar size={9} /> Deadline
                </p>
                <p className="text-[11px] font-semibold text-foreground leading-snug">
                  {formatDate(campaign.deadline)}
                </p>
              </div>
              <div className="rounded-xl bg-muted/40 border border-border/60 p-3 text-center">
                <p className="text-[10px] text-muted-foreground mb-1 flex items-center justify-center gap-0.5">
                  <Users size={9} /> Paid
                </p>
                <p className="text-base font-bold text-foreground leading-none">{campaign.paidCount}</p>
              </div>
            </div>

            {daysLeft > 0 && daysLeft <= 7 && (
              <div className="flex items-center gap-2 px-3 py-2 rounded-xl bg-amber-500/10 border border-amber-500/20 text-amber-700 dark:text-amber-400 text-xs font-medium">
                <AlertCircle size={13} />
                Only {daysLeft} day{daysLeft === 1 ? "" : "s"} left to activate
              </div>
            )}
            {isExpired && (
              <div className="flex items-center gap-2 px-3 py-2 rounded-xl bg-destructive/10 border border-destructive/20 text-destructive text-xs font-medium">
                <AlertCircle size={13} />
                This campaign has closed. Contact the alumni office for assistance.
              </div>
            )}
          </div>
        </div>

        {/* What you get */}
        <div className="rounded-2xl border border-border bg-card p-5 space-y-3">
          <p className="text-sm font-semibold text-foreground">What happens after payment?</p>
          <ul className="space-y-2.5">
            {[
              "Your account is automatically activated",
              "A unique alumni membership number is assigned to you",
              "You gain full access to the alumni portal",
              "Your membership status is immediately confirmed",
            ].map((item) => (
              <li key={item} className="flex items-start gap-2.5 text-xs text-muted-foreground">
                <CheckCircle2 size={14} className="text-green-500 flex-shrink-0 mt-0.5" />
                {item}
              </li>
            ))}
          </ul>
        </div>

        {/* Payment form */}
        <div className="rounded-2xl border border-border bg-card p-5 space-y-5">
          <div>
            <h2 className="text-base font-bold text-foreground">Complete your activation</h2>
            <p className="text-xs text-muted-foreground mt-1">
              Review your details and proceed to pay securely via Paystack.
            </p>
          </div>

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
            <div className="flex items-start gap-2 px-3 py-3 rounded-xl bg-amber-500/10 border border-amber-500/20 text-amber-700 dark:text-amber-400 text-xs">
              <AlertCircle size={14} className="flex-shrink-0 mt-0.5" />
              <span>Online payments are currently disabled for this campaign. Please contact the alumni office.</span>
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
                Connecting to Paystack…
              </>
            ) : (
              <>
                <CreditCard size={18} />
                Pay {formatCurrency(campaign.amountPerMember)} with Paystack
              </>
            )}
          </Button>

          <div className="flex items-center justify-center gap-2 text-[11px] text-muted-foreground">
            <ShieldCheck size={13} className="text-green-500" />
            256-bit encrypted · No card details stored
          </div>
        </div>

        <p className="text-center text-xs text-muted-foreground pb-4">
          Already have an account?{" "}
          <Link href="/login" className="text-primary hover:underline font-medium">
            Sign in
          </Link>
        </p>
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
