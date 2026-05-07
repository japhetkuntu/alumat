"use client";

import { useQuery, useMutation } from "@tanstack/react-query";
import { useParams, useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import { CreditCard, Loader2, ArrowLeft, Calendar, Target, Users, PlayCircle, Image as ImageIcon, CheckCircle2, Award } from "lucide-react";
import Link from "next/link";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Input } from "@/components/ui/input";
import { YouTubeEmbed } from "@/components/ui/youtube-embed";
import { formatCurrency, formatDate } from "@/lib/utils";
import { getCampaignById, getMyProfile, initiatePaystackPayment, renewMembership, getMyContributions } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { useState } from "react";

export default function CampaignDetailPage() {
  useAuth();
  const { id } = useParams() as { id: string };
  const router = useRouter();
  const [amount, setAmount] = useState<string>("");
  const [preset, setPreset] = useState<number | null>(null);

  const { data: campaign, isLoading: isLoadingCampaign } = useQuery({
    queryKey: ["campaign", id],
    queryFn: () => getCampaignById(id),
  });

  const { data: myContributions } = useQuery({
    queryKey: ["m-contributions", { campaignId: id }],
    queryFn: () => getMyContributions({ campaignId: id }),
  });

  const { data: profile } = useQuery({
    queryKey: ["m-profile"],
    queryFn: getMyProfile,
  });

  const payMut = useMutation({
    mutationFn: (payAmount: number) => {
      if (campaign?.isMembershipCampaign) {
        return renewMembership(id, 1, "online");
      }
      return initiatePaystackPayment({ campaignId: id, amount: payAmount });
    },
    onSuccess: (data: { authorizationUrl?: string; reference?: string }) => {
      if (data?.authorizationUrl) {
        window.location.href = data.authorizationUrl;
      } else {
        toast.success("Payment initiated successfully.");
      }
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (isLoadingCampaign) {
    return (
      <div className="p-8 lg:p-12 max-w-5xl mx-auto space-y-8 animate-pulse">
        <div className="h-8 w-32 bg-muted rounded-lg" />
        <div className="aspect-video w-full bg-muted rounded-2xl" />
        <div className="space-y-4">
          <div className="h-10 w-2/3 bg-muted rounded-lg" />
          <div className="h-4 w-full bg-muted rounded-lg" />
          <div className="h-4 w-5/6 bg-muted rounded-lg" />
        </div>
      </div>
    );
  }

  if (!campaign) return null;

  const hasPaid = myContributions?.results.some(c => c.status === "Confirmed" || c.status === "Pending");
  const isMembership = !!campaign?.isMembershipCampaign;
  const membershipPaid = isMembership && hasPaid;
  const isPensioner = profile?.employmentStatus === "Pensioner";
  const memberAmount = isPensioner && campaign.pensionerAmountPerMember != null
    ? campaign.pensionerAmountPerMember : campaign.amountPerMember;
  const pct = isMembership && campaign.totalEligibleMembers
    ? Math.round((campaign.paidCount / campaign.totalEligibleMembers) * 100)
    : campaign.targetAmount > 0 ? Math.round((campaign.collectedAmount / campaign.targetAmount) * 100) : 0;
  const isPaying = payMut.isPending;
  const displayAmount = isMembership ? String(memberAmount) : (amount || String(campaign.amountPerMember ?? ""));
  const numericAmount = Number(displayAmount);
  const isValidAmount = numericAmount > 0;

  return (
    <div className="p-2 lg:px-6 lg:py-5 w-full max-w-[1400px] mx-auto space-y-6 sm:space-y-8 lg:space-y-10 pb-20 selection:bg-primary/20">
      {/* Navigation & Header */}
      <nav className="flex items-center justify-between animate-in fade-in slide-in-from-top-4 duration-500">
        <Button variant="ghost" size="sm" className="h-10 rounded-xl hover:bg-black/5 dark:hover:bg-white/5 font-bold group" onClick={() => router.back()}>
          <ArrowLeft size={18} className="mr-2 group-hover:-translate-x-1 transition-transform" />
          Back to Campaigns
        </Button>
        <Badge variant={campaign.status === "Active" ? "success" : "secondary"} className="h-7 px-3 font-black uppercase tracking-widest text-[10px]">
          {campaign.status}
        </Badge>
      </nav>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 sm:gap-8 lg:gap-12">
        {/* Main Content Area */}
        <div className="lg:col-span-8 space-y-6 sm:space-y-8 lg:space-y-10">
          <div className="flex flex-wrap items-center gap-3 justify-between bg-white/70 border border-border/50 rounded-2xl px-4 py-3">
            <p className="text-sm text-muted-foreground">Need help collecting for your campaign outside the member portal? Share this link with anyone:</p>
            <div className="flex gap-2 items-center">
              <Button variant="outline" size="sm" onClick={() => {
                const link = `${window.location.origin}/payment-campaign/${id}`;
                void navigator.clipboard.writeText(link);
                toast.success("Shareable link copied to clipboard");
              }}>
                Copy public contribution link
              </Button>
              <a href={`/payment-campaign/${id}`} target="_blank" rel="noreferrer" className="text-xs font-semibold text-primary hover:underline">
                Open as guest
              </a>
            </div>
          </div>
          <header className="space-y-4 animate-in fade-in slide-in-from-bottom-4 duration-700">
            <h1 className="text-2xl sm:text-3xl lg:text-4xl xl:text-5xl font-black tracking-tight text-foreground leading-[1.1]">{campaign.title}</h1>
            <div className="flex flex-wrap items-center gap-6 text-sm font-bold text-muted-foreground">
              <div className="flex items-center gap-2">
                <Calendar size={18} className="text-primary" />
                Ends {formatDate(campaign.deadline)}
              </div>
              <div className="flex items-center gap-2">
                <Target size={18} className="text-primary" />
                Goal: {formatCurrency(campaign.targetAmount)}
              </div>
              <div className="flex items-center gap-2">
                <Users size={18} className="text-primary" />
                {campaign.paidCount} Contributors
              </div>
            </div>
          </header>

          {/* Featured Visual */}
          <section className="animate-in fade-in zoom-in-95 duration-1000 delay-200">
            {campaign.youtubeVideoUrl ? (
              <div className="rounded-[2rem] overflow-hidden shadow-2xl border-4 border-white dark:border-white/5 ring-1 ring-black/5 ring-offset-4 ring-offset-background bg-muted/20">
                <YouTubeEmbed url={campaign.youtubeVideoUrl} />
              </div>
            ) : campaign.bannerImageUrl ? (
              <div className="rounded-[2rem] overflow-hidden shadow-2xl border-4 border-white dark:border-white/5 ring-1 ring-black/5 ring-offset-4 ring-offset-background aspect-video">
                <img src={campaign.bannerImageUrl} alt={campaign.title} className="w-full h-full object-cover" />
              </div>
            ) : (
              <div className="rounded-[2rem] bg-gradient-to-br from-primary/10 to-muted/20 aspect-video flex items-center justify-center border-4 border-white dark:border-white/5 ring-1 ring-black/5">
                <ImageIcon size={64} className="text-primary/20" />
              </div>
            )}
          </section>

          {/* Description & Details */}
          <section className="space-y-6 prose prose-lg dark:prose-invert max-w-none animate-in fade-in slide-in-from-bottom-6 duration-700 delay-300">
            <h2 className="text-2xl font-black tracking-tight border-b border-border/40 pb-4">About this campaign</h2>
            <div className="whitespace-pre-wrap font-medium text-muted-foreground leading-relaxed text-lg">
              {campaign.description || "The community hasn't provided details for this campaign yet. Please reach out to the alumni office for more information."}
            </div>
          </section>

          {/* Media Links / Extras */}
          {(campaign.youtubeVideoUrl || campaign.bannerImageUrl) && (
            <section className="animate-in fade-in slide-in-from-bottom-8 duration-700 delay-500">
              <h3 className="text-xl font-bold mb-6 flex items-center gap-2">
                <PlayCircle size={20} className="text-primary" />
                Campaign Media
              </h3>
              <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
                {campaign.bannerImageUrl && (
                  <div className="aspect-square rounded-2xl overflow-hidden border border-border/40 hover:border-primary/40 transition-colors cursor-pointer group">
                    <img src={campaign.bannerImageUrl} className="w-full h-full object-cover group-hover:scale-110 transition-transform duration-700" alt="Gallery item" />
                  </div>
                )}
                <div className="aspect-square rounded-2xl bg-muted/30 border border-dashed border-border/60 flex items-center justify-center opacity-40">
                  <ImageIcon size={24} className="text-muted-foreground" />
                </div>
              </div>
            </section>
          )}
        </div>

        {/* Sidebar: Stats & Payment Action */}
        <aside className="lg:col-span-4 space-y-8 animate-in fade-in slide-in-from-right-8 duration-1000 delay-300">
          <Card className="sticky top-24 overflow-hidden border-border/40 shadow-2xl shadow-primary/5 bg-card/80 backdrop-blur-xl">
            <div className="absolute top-0 left-0 right-0 h-2 bg-primary" />
            <CardContent className="p-5 sm:p-8 lg:p-10 space-y-6 sm:space-y-8">
              <div className="space-y-2">
                <div className="flex justify-between items-end">
                  <span className="text-2xl sm:text-3xl lg:text-4xl font-black tracking-tighter text-foreground">{pct}%</span>
                  <span className="text-[13px] font-extrabold text-muted-foreground uppercase tracking-wider mb-2">{isMembership ? 'Members Paid' : 'Funded'}</span>
                </div>
                <Progress value={pct} className="h-3 rounded-full bg-primary/10">
                  <div className="h-full bg-primary relative animate-pulse-slow">
                    <div className="absolute inset-0 bg-[linear-gradient(45deg,rgba(255,255,255,0.2)_25%,transparent_25%,transparent_50%,rgba(255,255,255,0.2)_50%,rgba(255,255,255,0.2)_75%,transparent_75%,transparent)] bg-[length:20px_20px]" />
                  </div>
                </Progress>
                <div className="flex justify-between pt-1 font-bold text-[13px]">
                  <span className="text-primary">{isMembership ? `${campaign.paidCount} paid` : `${formatCurrency(campaign.collectedAmount)} raised`}</span>
                  <span className="text-muted-foreground/60">{isMembership ? `${campaign.totalEligibleMembers ?? '?'} eligible` : `Target ${formatCurrency(campaign.targetAmount)}`}</span>
                </div>
              </div>

              <div className="pt-8 border-t border-border/40 space-y-6">
                <div>
                  <p className="text-[11px] font-bold text-muted-foreground uppercase tracking-[0.2em] mb-2">{isMembership ? "Your Amount" : "Minimum Contribution"}</p>
                  <p className="text-xl sm:text-2xl lg:text-3xl font-black tracking-tight text-foreground/40 mb-4">{isMembership ? formatCurrency(memberAmount) : formatCurrency(campaign.amountPerMember)}</p>
                  
                  {isMembership && (
                    <p className="text-xs text-muted-foreground mb-4">This is a one-time membership payment{isPensioner ? " (pensioner rate)" : ""}. The amount is set by the admin.</p>
                  )}

                  {membershipPaid && (
                    <div className="flex items-center gap-2 px-3 py-2.5 rounded-xl bg-green-50 border border-green-200 text-green-800 dark:bg-green-950/30 dark:border-green-800 dark:text-green-300 mb-4">
                      <CheckCircle2 size={18} />
                      <span className="text-sm font-bold">Membership paid &mdash; thank you!</span>
                    </div>
                  )}

                  {membershipPaid && (
                    <Link href="/membership-certificate">
                      <Button variant="outline" className="w-full mb-4 h-11 font-bold text-[13px] border-amber-300 text-amber-700 hover:bg-amber-50 dark:border-amber-700 dark:text-amber-400 dark:hover:bg-amber-950/30">
                        <Award size={16} className="mr-2" />
                        View Membership Certificate
                      </Button>
                    </Link>
                  )}

                  {!isMembership && (
                    <div className="space-y-2 mb-6">
                      <p className="text-[11px] font-bold text-primary uppercase tracking-widest">Enter Amount (GHS)</p>
                      <div className="flex flex-wrap items-center gap-2">
                        {([1, 2, 5] as const).map((multiplier) => {
                          const val = multiplier * campaign.amountPerMember;
                          return (
                            <Button
                              key={multiplier}
                              size="sm"
                              variant={preset === multiplier ? "default" : "outline"}
                              className="text-[12px] font-bold"
                              onClick={() => {
                                setPreset(multiplier);
                                setAmount(String(val));
                              }}
                            >
                              {multiplier}× ({formatCurrency(val)})
                            </Button>
                          );
                        })}
                      </div>
                      <Input 
                        type="number" 
                        min={0.01}
                        step="0.01"
                        value={displayAmount}
                        onChange={(e) => {
                          setPreset(null);
                          setAmount(e.target.value);
                        }}
                        className="h-14 text-2xl font-black rounded-xl border-primary/20 focus:border-primary/50 focus:ring-primary/20"
                      />
                    </div>
                  )}
                </div>

                <div className="space-y-4">
                  {hasPaid && !isMembership && (
                    <div className="flex items-center gap-2 mb-2 px-2 py-1 bg-primary/5 rounded-lg border border-primary/10 animate-in fade-in slide-in-from-bottom-2">
                      <CheckCircle2 size={14} className="text-primary" />
                      <span className="text-[10px] font-extrabold text-primary uppercase tracking-tight">Previous Contributor</span>
                    </div>
                  )}
                  
                  <Button 
                    className="w-full h-16 rounded-2xl font-black text-lg shadow-2xl shadow-primary/20 hover:shadow-primary/40 transition-all hover:scale-[1.02] active:scale-[0.98] group/btn" 
                    disabled={isPaying || campaign.status !== "Active" || !isValidAmount || !campaign.allowOnlinePayments || membershipPaid}
                    onClick={() => payMut.mutate(numericAmount)}
                  >
                    {isPaying ? (
                      <Loader2 size={24} className="animate-spin" />
                    ) : membershipPaid ? (
                      <>
                        <CheckCircle2 size={22} className="mr-3" />
                        Already Paid
                      </>
                    ) : campaign.allowOnlinePayments ? (
                      <>
                        <CreditCard size={22} className="mr-3 group-hover/btn:rotate-12 transition-transform" />
                        {isMembership ? "Pay Membership" : hasPaid ? "Support Again" : "Complete Payment"}
                      </>
                    ) : (
                      "Online payments unavailable"
                    )}
                  </Button>
                </div>

                <p className="text-[11px] text-center font-bold text-muted-foreground/50 uppercase tracking-widest">
                  Secure payment via Paystack
                </p>

                {campaign.allowManualPayments && (
                  <div className="rounded-2xl border border-primary/20 bg-primary/5 p-4">
                    <h4 className="text-sm font-black text-primary mb-2 text-center p-b-2">Manual Payment Instructions</h4>
                    <p className="text-sm font-bold">Reference: <span className="text-primary">{profile?.memberNumber ?? "N/A"}</span></p>
                    {campaign.bankAccount ? (
                      <div className="space-y-1 text-sm">
                        <p className="font-bold">Bank Transfer</p>
                        <p>Account: {campaign.bankAccount.accountName}</p>
                        <p>Number: {campaign.bankAccount.accountNumber}</p>
                        <p>Bank: {campaign.bankAccount.bankName}</p>
                        <p>Branch: {campaign.bankAccount.branch}</p>
                      </div>
                    ) : null}
                    {campaign.mobileMoneyAccount ? (
                      <div className="space-y-1 text-sm pt-2 border-t border-primary/20">
                        <p className="font-bold">Mobile Money</p>
                        <p>Provider: {campaign.mobileMoneyAccount.provider}</p>
                        <p>Name: {campaign.mobileMoneyAccount.name}</p>
                        <p>Number: {campaign.mobileMoneyAccount.mobileMoneyNumber}</p>
                      </div>
                    ) : null}
                    {!campaign.bankAccount && !campaign.mobileMoneyAccount && (
                      <p className="text-sm text-muted-foreground">No manual account configured yet. Please contact the alumni office for details.</p>
                    )}
                  </div>
                )}
              </div>

              <div className="space-y-4 pt-4">
                <div className="flex items-center gap-3 p-4 rounded-2xl bg-muted/20 border border-border/40">
                  <div className="w-10 h-10 rounded-xl bg-primary/10 flex items-center justify-center text-primary shrink-0">
                    <Calendar size={20} />
                  </div>
                  <div>
                    <p className="text-[10px] font-bold text-muted-foreground uppercase tracking-widest">Ends in</p>
                    <p className="text-sm font-black">{formatDate(campaign.deadline)}</p>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </aside>
      </div>
    </div>
  );
}
