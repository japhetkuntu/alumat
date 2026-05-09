"use client";

import { useState, useEffect } from "react";
import { useParams } from "next/navigation";
import { useQuery, useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import {
  Copy, Check, Share2, MessageCircle, Twitter, Facebook,
  Users, Target, Calendar, ChevronDown, ChevronUp,
  Loader2, Lock, ArrowRight, ExternalLink,
} from "lucide-react";
import { getCampaignById, initiatePaystackPaymentGuest } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { formatCurrency, formatDate, cn } from "@/lib/utils";
import { YouTubeEmbed } from "@/components/ui/youtube-embed";

export default function PublicCampaignContributionPage() {
  const { campaignId } = useParams() as { campaignId: string };
  const [email, setEmail] = useState("");
  const [amount, setAmount] = useState("");
  const [showEmail, setShowEmail] = useState(false);
  const [shareUrl, setShareUrl] = useState("");
  const [copied, setCopied] = useState(false);
  const [statusModalOpen, setStatusModalOpen] = useState(false);
  const [paymentStatus, setPaymentStatus] = useState("Ready to initiate payment");

  useEffect(() => {
    if (typeof window !== "undefined") setShareUrl(window.location.href);
  }, []);

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

  useEffect(() => {
    if (campaign) {
      setAmount(campaign.isMembershipCampaign ? String(campaign.amountPerMember) : "");
    }
  }, [campaign]);

  const payMutation = useMutation({
    mutationFn: () => {
      setPaymentStatus("Initializing payment…");
      setStatusModalOpen(true);
      const amountToPay = campaign?.isMembershipCampaign
        ? campaign.amountPerMember
        : Number(amount || campaign?.amountPerMember || 0);
      return initiatePaystackPaymentGuest({ campaignId, amount: amountToPay, email });
    },
    onSuccess: (result: { authorizationUrl: string; reference: string }) => {
      setPaymentStatus("Payment initiated — redirecting…");
      toast.success("Redirecting to Paystack…");
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
      <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-50 to-slate-100">
        <div className="flex flex-col items-center gap-4 text-muted-foreground">
          <div className="w-10 h-10 rounded-full border-4 border-primary/30 border-t-primary animate-spin" />
          <p className="text-sm font-medium">Loading campaign…</p>
        </div>
      </div>
    );
  }

  if (!campaign) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-50 to-slate-100">
        <div className="text-center space-y-3 p-8">
          <div className="text-5xl mb-4">🔍</div>
          <h1 className="text-2xl font-bold">Campaign not found</h1>
          <p className="text-muted-foreground max-w-sm mx-auto">This campaign may have ended or the link might be incorrect. Check with the organiser for the correct link.</p>
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

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-white to-slate-100">
      {/* Top accent bar */}
      <div className="h-1.5 w-full bg-gradient-to-r from-primary via-primary/70 to-primary/40" />

      <div className="max-w-2xl mx-auto px-4 py-10 space-y-6">

        {/* ── Campaign Hero ─────────────────────────────────────── */}
        <div className="animate-in fade-in slide-in-from-bottom-4 duration-700">
          {campaign.bannerImageUrl && (
            <div className="rounded-2xl overflow-hidden shadow-xl mb-5">
              <img src={campaign.bannerImageUrl} alt={campaign.title} className="w-full max-h-56 object-cover" />
            </div>
          )}
          <div className="flex items-start justify-between gap-3 flex-wrap">
            <div>
              <div className="flex items-center gap-2 mb-2">
                <Badge variant={campaign.status === "Active" ? "success" : "secondary"} className="text-[10px] font-black uppercase tracking-widest">
                  {campaign.status}
                </Badge>
                {campaign.isMembershipCampaign && (
                  <Badge variant="outline" className="text-[10px] font-black uppercase tracking-widest">Membership</Badge>
                )}
              </div>
              <h1 className="text-2xl sm:text-3xl font-black tracking-tight leading-tight">{campaign.title}</h1>
            </div>
          </div>
          {campaign.description && (
            <p className="mt-3 text-muted-foreground leading-relaxed text-[15px]">{campaign.description}</p>
          )}
        </div>

        {/* ── Video ─────────────────────────────────────────────── */}
        {campaign.youtubeVideoUrl && (
          <div className="rounded-2xl overflow-hidden shadow-lg animate-in fade-in duration-700 delay-100">
            <YouTubeEmbed url={campaign.youtubeVideoUrl} />
          </div>
        )}

        {/* ── Progress card ─────────────────────────────────────── */}
        <Card className="border-border/40 shadow-lg shadow-black/5 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-100">
          <CardContent className="p-6 space-y-5">
            <div className="grid grid-cols-3 gap-3">
              <div className="flex flex-col items-center text-center p-3 rounded-xl bg-primary/5">
                <span className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Raised</span>
                <span className="text-lg font-black text-primary">{formatCurrency(campaign.collectedAmount)}</span>
              </div>
              <div className="flex flex-col items-center text-center p-3 rounded-xl bg-muted/30">
                <span className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Target</span>
                <span className="text-lg font-black">{formatCurrency(campaign.targetAmount)}</span>
              </div>
              <div className="flex flex-col items-center text-center p-3 rounded-xl bg-muted/30">
                <span className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Backers</span>
                <span className="text-lg font-black">{campaign.paidCount}</span>
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

        {/* ── Contribute card ───────────────────────────────────── */}
        <Card className="border-border/40 shadow-xl shadow-black/5 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-200">
          <CardContent className="p-6 space-y-5">
            <div className="flex items-center gap-2">
              <div className="w-8 h-8 rounded-xl bg-primary/10 flex items-center justify-center">
                <Target size={16} className="text-primary" />
              </div>
              <h2 className="text-lg font-black">Make a contribution</h2>
            </div>

            {isClosed ? (
              <div className="rounded-xl bg-muted/40 border border-border/40 p-5 text-center space-y-2">
                <p className="font-bold text-muted-foreground">This campaign is no longer accepting contributions.</p>
                <p className="text-sm text-muted-foreground">Thank you to everyone who supported!</p>
              </div>
            ) : !campaign.allowOnlinePayments ? (
              <div className="rounded-xl bg-amber-50 border border-amber-200 p-5 text-center space-y-2">
                <p className="font-bold text-amber-800">Online payments are not enabled for this campaign.</p>
                <p className="text-sm text-amber-700">Please contact the organiser for alternative payment instructions.</p>
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
                      <p className="mt-1.5 text-[11px] text-muted-foreground">Fixed membership fee set by the association.</p>
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
                    <>Pay {isValidAmount ? formatCurrency(numericAmount) : ""} with Paystack <ArrowRight size={18} /></>
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
          </CardContent>
        </Card>

        {/* ── Share card ────────────────────────────────────────── */}
        <Card className="border-border/40 shadow-lg shadow-black/5 animate-in fade-in slide-in-from-bottom-4 duration-700 delay-300">
          <CardContent className="p-6 space-y-4">
            <div className="flex items-center gap-2">
              <div className="w-8 h-8 rounded-xl bg-emerald-500/10 flex items-center justify-center">
                <Share2 size={15} className="text-emerald-600" />
              </div>
              <div>
                <p className="font-black text-[15px]">Share this campaign</p>
                <p className="text-[12px] text-muted-foreground">Help spread the word</p>
              </div>
            </div>

            {/* Copy URL row */}
            <div className="flex items-center gap-2">
              <div className="flex-1 flex items-center h-10 rounded-xl border border-border/60 bg-muted/20 px-3 gap-2 min-w-0">
                <ExternalLink size={13} className="text-muted-foreground shrink-0" />
                <span className="text-[12px] text-muted-foreground truncate font-mono">{shareUrl}</span>
              </div>
              <Button
                size="sm"
                variant={copied ? "default" : "outline"}
                className={cn(
                  "h-10 px-4 rounded-xl font-bold gap-1.5 shrink-0 transition-all duration-300",
                  copied && "bg-emerald-500 hover:bg-emerald-500 text-white border-emerald-500"
                )}
                onClick={copyShareUrl}
              >
                {copied ? <><Check size={14} /> Copied!</> : <><Copy size={14} /> Copy</>}
              </Button>
            </div>

            {/* Social share buttons */}
            <div className="grid grid-cols-3 gap-2">
              <a
                href={`https://wa.me/?text=${shareText}%20${encodedShare}`}
                target="_blank"
                rel="noreferrer"
                className="flex items-center justify-center gap-2 h-10 rounded-xl border border-border/40 hover:bg-emerald-50 hover:border-emerald-200 hover:text-emerald-700 transition-all text-[13px] font-bold text-muted-foreground"
              >
                <MessageCircle size={15} />
                WhatsApp
              </a>
              <a
                href={`https://twitter.com/intent/tweet?text=${shareText}&url=${encodedShare}`}
                target="_blank"
                rel="noreferrer"
                className="flex items-center justify-center gap-2 h-10 rounded-xl border border-border/40 hover:bg-sky-50 hover:border-sky-200 hover:text-sky-600 transition-all text-[13px] font-bold text-muted-foreground"
              >
                <Twitter size={15} />
                Twitter
              </a>
              <a
                href={`https://www.facebook.com/sharer/sharer.php?u=${encodedShare}`}
                target="_blank"
                rel="noreferrer"
                className="flex items-center justify-center gap-2 h-10 rounded-xl border border-border/40 hover:bg-blue-50 hover:border-blue-200 hover:text-blue-700 transition-all text-[13px] font-bold text-muted-foreground"
              >
                <Facebook size={15} />
                Facebook
              </a>
            </div>
          </CardContent>
        </Card>

        {/* Footer */}
        <p className="text-center text-[11px] text-muted-foreground pb-6">
          Powered by UMaT Alumni Portal · Payments secured by Paystack
        </p>
      </div>

      {/* ── Payment status modal ──────────────────────────────── */}
      <Dialog open={statusModalOpen} onOpenChange={setStatusModalOpen}>
        <DialogContent size="default">
          <DialogHeader>
            <DialogTitle>Processing payment</DialogTitle>
            <DialogDescription>Please wait while we redirect you to Paystack.</DialogDescription>
          </DialogHeader>
          <div className="mt-2 space-y-4">
            <div className="flex items-center gap-3 rounded-xl border border-primary/20 bg-primary/5 p-4">
              <Loader2 size={18} className="animate-spin text-primary shrink-0" />
              <p className="text-sm font-semibold text-primary">{paymentStatus}</p>
            </div>
            <p className="text-xs text-muted-foreground">
              If your browser doesn&apos;t redirect automatically, complete the payment in the Paystack window and return here.
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
