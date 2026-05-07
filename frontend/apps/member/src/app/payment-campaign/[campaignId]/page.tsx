"use client";

import { useState, useEffect } from "react";
import { useParams } from "next/navigation";
import { useQuery, useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import { Share2, Copy, Link as LinkIcon, Twitter, Facebook } from "lucide-react";
import { getCampaignById, initiatePaystackPaymentGuest } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { formatCurrency, formatDate } from "@/lib/utils";

export default function PublicCampaignContributionPage() {
  const { campaignId } = useParams() as { campaignId: string };
  const [email, setEmail] = useState("");
  const [amount, setAmount] = useState("");
  const [shareUrl, setShareUrl] = useState("");
  const [isMembershipAmountFixed, setIsMembershipAmountFixed] = useState(false);
  const [statusModalOpen, setStatusModalOpen] = useState(false);
  const [paymentStatus, setPaymentStatus] = useState("Ready to initiate payment");

  useEffect(() => {
    if (typeof window !== "undefined") {
      setShareUrl(window.location.href);
    }
  }, []);

  const copyShareUrlToClipboard = async () => {
    if (!shareUrl) return;
    await navigator.clipboard.writeText(shareUrl);
    toast.success("Share link copied!");
  };

  const twitterShareUrl = `https://twitter.com/intent/tweet?text=${encodeURIComponent("Join this campaign and support our alumni: " + shareUrl)}`;
  const facebookShareUrl = `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(shareUrl)}`;
  const whatsappShareUrl = `https://wa.me/?text=${encodeURIComponent("Donate to our campaign: " + shareUrl)}`;

  const { data: campaign, isLoading } = useQuery({
    queryKey: ["campaign", campaignId],
    queryFn: () => getCampaignById(campaignId),
    enabled: Boolean(campaignId),
  });

  useEffect(() => {
    if (campaign) {
      if (campaign.isMembershipCampaign) {
        setIsMembershipAmountFixed(true);
        setAmount(String(campaign.amountPerMember));
      } else {
        setIsMembershipAmountFixed(false);
        setAmount("");
      }
    }
  }, [campaign]);

  const payMutation = useMutation({
    mutationFn: () => {
      setPaymentStatus("Initializing payment...");
      setStatusModalOpen(true);
      const amountToPay = campaign?.isMembershipCampaign
        ? campaign.amountPerMember
        : Number(amount || campaign?.amountPerMember || 0);
      return initiatePaystackPaymentGuest({
        campaignId,
        amount: amountToPay,
        email,
      });
    },
    onSuccess: (result: { authorizationUrl: string; reference: string }) => {
      setPaymentStatus("Payment initiated. Redirecting to Paystack...");
      toast.success("Redirecting to Paystack checkout...");
      setTimeout(() => {
        window.location.href = result.authorizationUrl;
      }, 400);
    },
    onError: (error) => {
      const err = handleApiError(error);
      setPaymentStatus(`Payment initiation failed: ${err}`);
      toast.error("Payment initiation failed", { description: err });
    },
  });

  if (isLoading) {
    return <div className="p-8 text-center">Loading campaign data...</div>;
  }

  if (!campaign) {
    return <div className="p-8 text-center text-destructive">Campaign not found.</div>;
  }

  const numericAmount = Number(amount || campaign.amountPerMember);
  const isValidAmount = numericAmount > 0;

  return (
    <div className="min-h-screen p-6 sm:p-10 bg-slate-50">
      <div className="max-w-3xl mx-auto space-y-6">
        <Card className="border border-border/50">
          <CardContent className="space-y-4">
            <h1 className="text-3xl font-bold">{campaign.title}</h1>
            <p className="text-sm text-muted-foreground">Ends {formatDate(campaign.deadline)}</p>
            <p className="text-lg font-semibold">Target: {formatCurrency(campaign.targetAmount)}</p>
            <p className="text-base">{campaign.description || "No campaign description available."}</p>
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div>Collected: {formatCurrency(campaign.collectedAmount)}</div>
              <div>Raised by: {campaign.paidCount} contributor{campaign.paidCount === 1 ? "" : "s"}</div>
            </div>
          </CardContent>
        </Card>

        <Card className="border border-border/50">
          <CardContent className="space-y-4">
            <h2 className="text-xl font-bold">Contribute as guest</h2>
            {!campaign.allowOnlinePayments && (
              <div className="px-4 py-3 rounded-lg bg-yellow-50 text-yellow-900 border border-yellow-200">Online payments are not available for this campaign.</div>
            )}

            <div className="space-y-3">
              <label className="block text-sm font-semibold">Email address (optional)</label>
              <Input
                type="text"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="yourname@example.com, or leave blank"
                className="h-12"
                inputMode="email"
                aria-label="email address (optional)"
              />
              <p className="text-xs text-muted-foreground">No email is required. Leave blank for anonymous contribution.</p>
            </div>

            <div className="space-y-3">
              <label className="block text-sm font-semibold">Amount (GHS)</label>
              <Input
                type="number"
                min={0.01}
                step={0.01}
                value={amount || String(campaign.amountPerMember)}
                onChange={(e) => setAmount(e.target.value)}
                className="h-12"
                readOnly={isMembershipAmountFixed}
                disabled={isMembershipAmountFixed}
              />
              {isMembershipAmountFixed && <p className="text-xs text-muted-foreground">Membership payments have fixed amount set by super admin.</p>}
            </div>

            <Button
              className="w-full h-14"
              disabled={!campaign.allowOnlinePayments || payMutation.isPending || !isValidAmount}
              onClick={() => payMutation.mutate()}
            >
              {payMutation.isPending ? "Starting payment..." : "Pay with Paystack"}
            </Button>

            <p className="text-xs text-muted-foreground">
              Logged-in members get credit for participating in campus initiatives.{' '}
              <a href="/login" className="text-primary hover:underline">
                Sign in
              </a>
            </p>

            <div className="border-t border-border/50 pt-4">
              <div className="flex items-center justify-between">
                <p className="text-sm font-bold text-foreground">Share this campaign</p>
                <Button variant="outline" size="sm" className="gap-1" onClick={copyShareUrlToClipboard}>
                  <Copy size={14} /> Copy URL
                </Button>
              </div>

              <div className="mt-3 rounded-xl border border-border/70 bg-white/80 p-3 shadow-inner">
                <div className="flex items-center justify-between gap-2">
                  <Input
                    type="text"
                    value={shareUrl}
                    readOnly
                    className="h-10 text-xs select-all"
                  />
                  <Button variant="secondary" size="sm" onClick={copyShareUrlToClipboard}>
                    <LinkIcon size={14} /> Copy
                  </Button>
                </div>
                <div className="mt-3 flex items-center gap-2 text-xs">
                  <a href={twitterShareUrl} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-twitter hover:text-twitter/90">
                    <Twitter size={16} /> Tweet
                  </a>
                  <a href={facebookShareUrl} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-blue-600 hover:text-blue-500">
                    <Facebook size={16} /> Facebook
                  </a>
                  <a href={whatsappShareUrl} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-emerald-700 hover:text-emerald-600">
                    <Share2 size={16} /> WhatsApp
                  </a>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      <Dialog open={statusModalOpen} onOpenChange={(open) => setStatusModalOpen(open)}>
        <DialogContent size="default">
          <DialogHeader>
            <DialogTitle>Transaction status</DialogTitle>
            <DialogDescription>Track where your contribution is in the flow.</DialogDescription>
          </DialogHeader>
          <div className="mt-4 space-y-3">
            <div className="rounded-xl border border-primary/30 bg-primary/10 p-4">
              <p className="text-sm font-semibold text-primary">{paymentStatus}</p>
            </div>
            <div className="text-xs text-muted-foreground">
              If your browser is not redirecting automatically, use the link in the Paystack window or refresh this page.
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setStatusModalOpen(false)}>
              Close status
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
