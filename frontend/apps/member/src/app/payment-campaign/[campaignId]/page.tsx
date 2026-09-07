"use client";

import { useState, useSyncExternalStore } from "react";
import { useParams, useSearchParams } from "next/navigation";
import { useQuery, useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Progress } from "@alumni/ui";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@alumni/ui";
import {
  Copy, Check, Share2, MessageCircle, Twitter, Facebook, Send, Linkedin, Mail, MessageSquare,
  Users, Target, Calendar, ChevronDown, ChevronUp,
  Loader2, Lock, ArrowRight, ExternalLink,
} from "@alumni/ui";
import { getCampaignById, initiatePaystackPayment, initiatePaystackPaymentGuest } from "@/lib/member-api";
import { useAuth } from "@/hooks/use-auth";
import { handleApiError } from "@/lib/api-client";
import { formatCurrency, formatDate, cn } from "@alumni/ui";
import { YouTubeEmbed } from "@alumni/ui";

// Hydration-safe read of the current page URL (see useHostname()): server and
// the first client render both see "", avoiding a mismatch, then React syncs
// to the real value right after mount.
function useShareUrl() {
  return useSyncExternalStore(
    () => () => {},
    () => window.location.href,
    () => ""
  );
}

export default function PublicCampaignContributionPage() {
  const { campaignId } = useParams() as { campaignId: string };
  const searchParams = useSearchParams();
  const sharedByMemberId = searchParams.get("ref") || undefined;
  const { user, isMember } = useAuth();
  const [email, setEmail] = useState("");
  const [amount, setAmount] = useState("");
  const [showEmail, setShowEmail] = useState(false);
  const rawShareUrl = useShareUrl();
  // Carries the current viewer's own member id as `?ref=` on shared links, so
  // a guest who pays through a shared link can be attributed back to whoever
  // shared it (see the payment mutation below) — logged-out viewers just
  // share the plain URL, since they have no id to attach.
  const shareUrl = rawShareUrl && user?.id
    ? `${rawShareUrl}${rawShareUrl.includes("?") ? "&" : "?"}ref=${user.id}`
    : rawShareUrl;
  const [copied, setCopied] = useState(false);
  const [shareOpen, setShareOpen] = useState(false);
  const [statusModalOpen, setStatusModalOpen] = useState(false);
  const [paymentStatus, setPaymentStatus] = useState("Ready to initiate payment");

  const copyShareUrl = async () => {
    if (!shareUrl) return;
    await navigator.clipboard.writeText(shareUrl);
    setCopied(true);
    toast.success("Link copied!");
    setTimeout(() => setCopied(false), 2000);
  };

  const { data: campaign, isLoading } = useQuery({
    queryKey: ["pub-campaign", campaignId],
    queryFn: () => getCampaignById(campaignId),
    enabled: Boolean(campaignId),
  });

  // Prefill the amount field when the campaign loads — done during render
  // (React's documented alternative to an effect for this case) rather than
  // in a useEffect, guarded so it only fires once per distinct `campaign`
  // reference.
  const [syncedCampaign, setSyncedCampaign] = useState(campaign);
  if (campaign && campaign !== syncedCampaign) {
    setSyncedCampaign(campaign);
    setAmount(campaign.isMembershipCampaign ? String(campaign.amountPerMember) : "");
  }

  const payMutation = useMutation({
    mutationFn: () => {
      setPaymentStatus("Initializing payment…");
      setStatusModalOpen(true);
      const amountToPay = campaign?.isMembershipCampaign
        ? campaign.amountPerMember
        : Number(amount || campaign?.amountPerMember || 0);
      // Built client-side so the Paystack redirect lands back on THIS
      // institution's own subdomain, not the backend's shared fallback host.
      const callbackUrl = `${window.location.origin}/contributions/callback`;
      // A logged-in viewer pays as themselves, even on this public link —
      // only a logged-out payer goes through the guest flow, which attributes
      // the payment to whoever shared the link (via ?ref=) when present.
      if (isMember) {
        return initiatePaystackPayment({ campaignId, amount: amountToPay, callbackUrl });
      }
      return initiatePaystackPaymentGuest({ campaignId, amount: amountToPay, email, callbackUrl, sharedByMemberId });
    },
    onSuccess: (result: { authorizationUrl: string; reference: string }) => {
      setPaymentStatus("Payment initiated — redirecting…");
      toast.success("Redirecting to secure payment…");
      setTimeout(() => { window.location.href = result.authorizationUrl; }, 400);
    },
    onError: (error) => {
      const err = handleApiError(error);
      setPaymentStatus(`Failed: ${err}`);
      toast.error("Payment failed", { description: err });
      setStatusModalOpen(false);
    },
  });

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background">
        <div className="flex flex-col items-center gap-4 text-muted-foreground">
          <div className="w-10 h-10 rounded-full border-4 border-primary/30 border-t-primary animate-spin" />
          <p className="text-sm font-medium">Loading…</p>
        </div>
      </div>
    );
  }

  if (!campaign) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background">
        <div className="text-center space-y-3 p-8">
          <div className="text-5xl mb-4">🔍</div>
          <h1 className="font-[family-name:var(--font-display)] text-2xl font-bold">Not found</h1>
          <p className="text-muted-foreground max-w-sm mx-auto">This may have ended or the link might be incorrect. Check with the organiser for the correct link.</p>
        </div>
      </div>
    );
  }

  const pct = campaign.targetAmount > 0 ? Math.min(Math.round((campaign.collectedAmount / campaign.targetAmount) * 100), 100) : 0;
  const numericAmount = Number(amount || campaign.amountPerMember);
  const isValidAmount = numericAmount > 0;
  const isClosed = campaign.status !== "Active";
  const isMembershipFixed = campaign.isMembershipCampaign;
  const encodedShare = encodeURIComponent(shareUrl);
  const shareText = encodeURIComponent(`Support "${campaign.title}" — every contribution counts!`);
  const shareTextPlain = `Support "${campaign.title}" — every contribution counts!`;

  const sharePlatforms = [
    {
      name: "WhatsApp",
      icon: MessageCircle,
      href: `https://wa.me/?text=${shareText}%20${encodedShare}`,
      hover: "hover:bg-[#25D366]/10 hover:border-[#25D366]/30 hover:text-[#25D366]",
    },
    {
      name: "Telegram",
      icon: Send,
      href: `https://t.me/share/url?url=${encodedShare}&text=${shareText}`,
      hover: "hover:bg-[#26A5E4]/10 hover:border-[#26A5E4]/30 hover:text-[#26A5E4]",
    },
    {
      name: "X / Twitter",
      icon: Twitter,
      href: `https://twitter.com/intent/tweet?text=${shareText}&url=${encodedShare}`,
      hover: "hover:bg-foreground/5 hover:border-foreground/20 hover:text-foreground",
    },
    {
      name: "Facebook",
      icon: Facebook,
      href: `https://www.facebook.com/sharer/sharer.php?u=${encodedShare}`,
      hover: "hover:bg-[#1877F2]/10 hover:border-[#1877F2]/30 hover:text-[#1877F2]",
    },
    {
      name: "LinkedIn",
      icon: Linkedin,
      href: `https://www.linkedin.com/sharing/share-offsite/?url=${encodedShare}`,
      hover: "hover:bg-[#0A66C2]/10 hover:border-[#0A66C2]/30 hover:text-[#0A66C2]",
    },
    {
      name: "Email",
      icon: Mail,
      href: `mailto:?subject=${encodeURIComponent(`Support "${campaign.title}"`)}&body=${shareText}%20${encodedShare}`,
      hover: "hover:bg-amber-500/10 hover:border-amber-500/30 hover:text-amber-600",
    },
    {
      name: "SMS",
      icon: MessageSquare,
      href: `sms:?body=${shareText}%20${encodedShare}`,
      hover: "hover:bg-emerald-500/10 hover:border-emerald-500/30 hover:text-emerald-600",
    },
  ];

  const shareGrid = (
    <div className="grid grid-cols-4 gap-2.5">
      {sharePlatforms.map(({ name, icon: Icon, href, hover }) => (
        <a
          key={name}
          href={href}
          target="_blank"
          rel="noreferrer"
          className={cn(
            "flex flex-col items-center justify-center gap-1.5 rounded-xl border border-border/40 py-3 text-muted-foreground transition-all",
            hover
          )}
        >
          <Icon size={18} />
          <span className="text-[10.5px] font-bold leading-none text-center">{name}</span>
        </a>
      ))}
      <button
        type="button"
        onClick={copyShareUrl}
        className={cn(
          "flex flex-col items-center justify-center gap-1.5 rounded-xl border border-border/40 py-3 text-muted-foreground transition-all",
          copied ? "bg-success/10 border-success/30 text-success" : "hover:bg-primary/5 hover:border-primary/30 hover:text-primary"
        )}
      >
        {copied ? <Check size={18} /> : <Copy size={18} />}
        <span className="text-[10.5px] font-bold leading-none text-center">{copied ? "Copied!" : "Copy link"}</span>
      </button>
    </div>
  );

  return (
    <div className="min-h-screen bg-background">
      {/* Top accent bar */}
      <div className="h-1.5 w-full bg-gradient-to-r from-primary via-primary/70 to-primary/40" />

      <div className="max-w-5xl mx-auto px-4 py-6 sm:py-8">
        <div className="grid grid-cols-1 lg:grid-cols-[1fr_400px] gap-5 lg:gap-8 items-start">

          {/* ═══════════════ LEFT: campaign story ═══════════════ */}
          <div className="space-y-5 lg:order-1 order-2">

            {/* Compact hero */}
            <div className="animate-in fade-in slide-in-from-bottom-4 duration-700">
              {campaign.bannerImageUrl && (
                <div className="rounded-2xl overflow-hidden shadow-xl mb-4">
                  <img src={campaign.bannerImageUrl} alt={campaign.title} className="w-full max-h-48 object-cover" />
                </div>
              )}
              <div className="flex items-center gap-2 mb-2">
                <Badge variant={campaign.status === "Active" ? "success" : "secondary"} className="text-[10px] font-black uppercase tracking-widest">
                  {campaign.status}
                </Badge>
                {campaign.isMembershipCampaign && (
                  <Badge variant="outline" className="text-[10px] font-black uppercase tracking-widest">Membership</Badge>
                )}
              </div>
              <h1 className="font-[family-name:var(--font-display)] text-2xl sm:text-3xl font-bold tracking-tight leading-tight">{campaign.title}</h1>
              {campaign.description && (
                <p className="mt-2.5 text-muted-foreground leading-relaxed text-[14.5px]">{campaign.description}</p>
              )}
            </div>

            {/* Progress card */}
            <Card className="border-border/40 shadow-md shadow-black/5 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-100">
              <CardContent className="p-5 space-y-4">
                <div className="grid grid-cols-3 gap-3">
                  <div className="flex flex-col items-center text-center p-2.5 rounded-xl bg-primary/5">
                    <span className="text-[10.5px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Raised</span>
                    <span className="text-base font-black text-primary">{formatCurrency(campaign.collectedAmount)}</span>
                  </div>
                  <div className="flex flex-col items-center text-center p-2.5 rounded-xl bg-muted/30">
                    <span className="text-[10.5px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Target</span>
                    <span className="text-base font-black">{formatCurrency(campaign.targetAmount)}</span>
                  </div>
                  <div className="flex flex-col items-center text-center p-2.5 rounded-xl bg-muted/30">
                    <span className="text-[10.5px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Backers</span>
                    <span className="text-base font-black">{campaign.paidCount}</span>
                  </div>
                </div>
                <div className="space-y-2">
                  <div className="flex items-center justify-between text-sm">
                    <span className="font-bold text-primary">{pct}% funded</span>
                    <span className="text-muted-foreground text-[12px]">
                      <Calendar size={12} className="inline mr-1 -mt-0.5" />
                      Ends {formatDate(campaign.deadline)}
                    </span>
                  </div>
                  <Progress value={pct} className="h-3" />
                </div>
                <div className="flex items-center gap-2 text-[12px] text-muted-foreground pt-1">
                  <Users size={13} className="shrink-0" />
                  <span>{campaign.paidCount} contributor{campaign.paidCount === 1 ? "" : "s"} have supported this campaign</span>
                </div>
              </CardContent>
            </Card>

            {/* Video */}
            {campaign.youtubeVideoUrl && (
              <div className="rounded-2xl overflow-hidden shadow-lg animate-in fade-in duration-700 delay-150">
                <YouTubeEmbed url={campaign.youtubeVideoUrl} />
              </div>
            )}

            {/* Share — full card, desktop only (mobile gets the compact bar below the pay card) */}
            <Card className="hidden lg:block border-border/40 shadow-md shadow-black/5 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-200">
              <CardContent className="p-5 space-y-4">
                <div className="flex items-center gap-2">
                  <div className="w-8 h-8 rounded-xl bg-success/10 flex items-center justify-center">
                    <Share2 size={15} className="text-success" />
                  </div>
                  <div>
                    <p className="font-black text-[15px]">Share {isMembershipFixed ? "these dues" : "this fundraiser"}</p>
                    <p className="text-[12px] text-muted-foreground">Help spread the word — every share counts</p>
                  </div>
                </div>
                {shareGrid}
              </CardContent>
            </Card>

            {/* Footer (desktop only, mobile footer is at the very end) */}
            <p className="hidden lg:block text-center text-[11px] text-muted-foreground pb-2">
              Powered by the Alumni Portal · Payments secured online
            </p>
          </div>

          {/* ═══════════════ RIGHT: sticky payment card ═══════════════ */}
          <div className="lg:order-2 order-1 lg:sticky lg:top-6">
            <Card className="border-border/40 shadow-xl shadow-black/10 animate-in fade-in slide-in-from-bottom-4 duration-500">
              <CardContent className="p-5 sm:p-6 space-y-4">
                <div className="flex items-center gap-2">
                  <div className="w-8 h-8 rounded-xl bg-primary/10 flex items-center justify-center">
                    <Target size={16} className="text-primary" />
                  </div>
                  <h2 className="text-lg font-black">Make a contribution</h2>
                </div>

                {isClosed ? (
                  <div className="rounded-xl bg-muted/40 border border-border/40 p-5 text-center space-y-2">
                    <p className="font-bold text-muted-foreground">{isMembershipFixed ? "These dues are" : "This fundraiser is"} no longer accepting contributions.</p>
                    <p className="text-sm text-muted-foreground">Thank you to everyone who supported!</p>
                  </div>
                ) : (
                  <div className="space-y-4">
                    {/* Amount */}
                    <div className="space-y-2">
                      <label className="block text-[13px] font-bold">
                        Amount <span className="text-muted-foreground font-normal">(GHS)</span>
                      </label>
                      {isMembershipFixed ? (
                        <div>
                          <div className="flex items-center h-14 rounded-xl border border-border/60 bg-muted/30 px-4 gap-3">
                            <Lock size={15} className="text-muted-foreground shrink-0" />
                            <span className="text-xl font-black text-foreground">{formatCurrency(campaign.amountPerMember)}</span>
                          </div>
                          <p className="mt-1.5 text-[11px] text-muted-foreground">Fixed dues amount set by the association.</p>
                        </div>
                      ) : (
                        <div className="relative">
                          <span className="absolute left-4 top-1/2 -translate-y-1/2 text-muted-foreground font-bold text-sm">GHS</span>
                          <Input
                            type="number"
                            min={0.01}
                            step={0.01}
                            value={amount}
                            onChange={(e) => setAmount(e.target.value)}
                            placeholder={String(campaign.amountPerMember)}
                            className="h-14 pl-14 text-lg font-bold rounded-xl"
                            inputMode="decimal"
                          />
                        </div>
                      )}
                    </div>

                    {/* Email — collapsible */}
                    <div className="space-y-2">
                      <button
                        type="button"
                        onClick={() => setShowEmail((v) => !v)}
                        className="flex items-center gap-2 text-[13px] text-muted-foreground hover:text-foreground transition-colors group"
                      >
                        {showEmail ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
                        <span className="font-medium group-hover:underline underline-offset-2">
                          {showEmail ? "Hide email field" : "Add your email (optional)"}
                        </span>
                      </button>
                      {showEmail && (
                        <div className="animate-in slide-in-from-top-2 duration-200 space-y-1.5">
                          <Input
                            type="email"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                            placeholder="your@email.com"
                            className="h-12 rounded-xl"
                            autoComplete="email"
                          />
                          <p className="text-[11px] text-muted-foreground">
                            Optional. Used only to send you a payment receipt.
                          </p>
                        </div>
                      )}
                    </div>

                    {/* Pay button */}
                    <Button
                      size="lg"
                      className="w-full h-14 text-base font-black rounded-xl shadow-lg shadow-primary/20 gap-2"
                      disabled={payMutation.isPending || !isValidAmount}
                      onClick={() => payMutation.mutate()}
                    >
                      {payMutation.isPending ? (
                        <><Loader2 size={18} className="animate-spin" /> Processing…</>
                      ) : (
                        <>Pay {isValidAmount ? formatCurrency(numericAmount) : ""} online <ArrowRight size={18} /></>
                      )}
                    </Button>

                    {/* Sign-in nudge */}
                    <p className="text-center text-[12px] text-muted-foreground">
                      Are you a member?{" "}
                      <a href="/login" className="text-primary font-semibold hover:underline underline-offset-2">
                        Sign in for full credit
                      </a>
                    </p>
                  </div>
                )}

                {/* Compact share row — visible on all sizes under the pay card;
                    opens the full share dialog for the richer platform grid. */}
                <div className="pt-3 border-t border-border/40 flex items-center gap-2">
                  <div className="flex-1 flex items-center h-10 rounded-xl border border-border/60 bg-muted/20 px-3 gap-2 min-w-0">
                    <ExternalLink size={13} className="text-muted-foreground shrink-0" />
                    <span className="text-[12px] text-muted-foreground truncate font-mono">{shareUrl}</span>
                  </div>
                  <Button
                    size="sm"
                    variant="outline"
                    className="h-10 px-3.5 rounded-xl font-bold gap-1.5 shrink-0"
                    onClick={() => setShareOpen(true)}
                  >
                    <Share2 size={14} /> Share
                  </Button>
                </div>
              </CardContent>
            </Card>

            {/* Mobile-only share section (desktop has the full card in the left column) */}
            <Card className="lg:hidden mt-5 border-border/40 shadow-md shadow-black/5 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-200">
              <CardContent className="p-5 space-y-4">
                <div className="flex items-center gap-2">
                  <div className="w-8 h-8 rounded-xl bg-success/10 flex items-center justify-center">
                    <Share2 size={15} className="text-success" />
                  </div>
                  <div>
                    <p className="font-black text-[15px]">Share {isMembershipFixed ? "these dues" : "this fundraiser"}</p>
                    <p className="text-[12px] text-muted-foreground">Help spread the word — every share counts</p>
                  </div>
                </div>
                {shareGrid}
              </CardContent>
            </Card>

            <p className="lg:hidden text-center text-[11px] text-muted-foreground pt-5 pb-2">
              Powered by the Alumni Portal · Payments secured online
            </p>
          </div>
        </div>
      </div>

      {/* ── Share dialog (triggered from the compact "Share" button next to the pay card) ── */}
      <Dialog open={shareOpen} onOpenChange={setShareOpen}>
        <DialogContent size="default">
          <DialogHeader>
            <DialogTitle>Share {isMembershipFixed ? "these dues" : "this fundraiser"}</DialogTitle>
            <DialogDescription>{shareTextPlain}</DialogDescription>
          </DialogHeader>
          <div className="mt-1">{shareGrid}</div>
        </DialogContent>
      </Dialog>

      {/* ── Payment status modal ──────────────────────────────── */}
      <Dialog open={statusModalOpen} onOpenChange={setStatusModalOpen}>
        <DialogContent size="default">
          <DialogHeader>
            <DialogTitle>Processing payment</DialogTitle>
            <DialogDescription>Please wait while we redirect you to the payment page.</DialogDescription>
          </DialogHeader>
          <div className="mt-2 space-y-4">
            <div className="flex items-center gap-3 rounded-xl border border-primary/20 bg-primary/5 p-4">
              <Loader2 size={18} className="animate-spin text-primary shrink-0" />
              <p className="text-sm font-semibold text-primary">{paymentStatus}</p>
            </div>
            <p className="text-xs text-muted-foreground">
              If your browser doesn&apos;t redirect automatically, complete the payment in the payment window and return here.
            </p>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setStatusModalOpen(false)}>Dismiss</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
