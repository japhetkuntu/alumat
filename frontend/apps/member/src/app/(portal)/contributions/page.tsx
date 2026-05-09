"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { CheckCircle2, CreditCard, Loader2, RefreshCcw, XCircle, Award } from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Progress } from "@/components/ui/progress";
import { TableSkeleton } from "@/components/ui/skeleton";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { formatCurrency, formatDate } from "@/lib/utils";
import {
  getMyCampaigns,
  getMyContributions,
  getPaystackPaymentStatus,
  initiatePaystackPayment,
  renewMembership,
  getMyProfile,
} from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import type { Campaign, ContributionStatus } from "@/types";

const statusVariant: Record<ContributionStatus, "success" | "warning" | "destructive"> = {
  Confirmed: "success",
  Pending: "warning",
  Rejected: "destructive",
};

type PollStatus = "loading" | "pending" | "success" | "error";

function PaymentStatusModal({
  reference,
  open,
  onClose,
  onConfirmed,
}: {
  reference: string | null;
  open: boolean;
  onClose: () => void;
  onConfirmed: () => void;
}) {
  const [status, setStatus] = useState<PollStatus>("loading");
  const [message, setMessage] = useState<string>("");
  const [seconds, setSeconds] = useState(10);

  const reset = useCallback(() => {
    setStatus("loading");
    setMessage("");
    setSeconds(10);
  }, []);

  const poll = useCallback(async () => {
    if (!reference) return;
    setStatus("loading");

    try {
      const res = await getPaystackPaymentStatus(reference);
      const normalized = res.status?.toLowerCase() ?? "unknown";

      if (normalized === "confirmed" || normalized === "success") {
        setStatus("success");
        setMessage(res.message ?? "Payment confirmed.");
        onConfirmed();
        return;
      }

      if (normalized === "pending" || normalized === "unknown") {
        setStatus("pending");
        setMessage(res.message ?? "We are still waiting on payment confirmation. Please check again shortly.");
        return;
      }

      setStatus("error");
      setMessage(res.message ?? `Payment status: ${res.status}`);
    } catch (err) {
      setStatus("error");
      setMessage(handleApiError(err));
    }
  }, [reference, onConfirmed]);

  useEffect(() => {
    if (!open) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    reset();
    poll();
  }, [open, reset, poll]);

  useEffect(() => {
    if (!open || status === "success" || status === "error") return;

    const interval = setInterval(() => {
      setSeconds((prev) => {
        if (prev <= 1) {
          poll();
          return 10;
        }
        return prev - 1;
      });
    }, 1000);

    return () => clearInterval(interval);
  }, [open, status, poll]);

  const statusLabel = useMemo(() => {
    switch (status) {
      case "loading":
        return "Verifying payment...";
      case "pending":
        return "Payment pending";
      case "success":
        return "Payment confirmed";
      case "error":
        return "Payment failed";
      default:
        return "Payment status";
    }
  }, [status]);

  return (
    <Dialog open={open} onOpenChange={(isOpen) => (isOpen ? null : onClose())}>
      <DialogContent>
        <DialogHeader className="items-center text-center">
          <div className="mx-auto mb-3">
            {status === "loading" ? (
              <Loader2 size={48} className="animate-spin text-primary" />
            ) : status === "success" ? (
              <CheckCircle2 size={48} className="text-green-600" />
            ) : (
              <XCircle size={48} className="text-destructive" />
            )}
          </div>
          <DialogTitle className="w-full">{statusLabel}</DialogTitle>
          <DialogDescription className="w-full mx-auto">{message}</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button onClick={onClose}>Close</Button>
          <Button variant="outline" onClick={() => poll()} disabled={status === "loading"}>
            <RefreshCcw className="mr-2 h-4 w-4" />
            {status === "loading" ? "Checking..." : `Check again (${seconds}s)`}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default function MemberContributionsPage() {
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const { data: campaignsData } = useQuery({
    queryKey: ["m-campaigns"],
    queryFn: () => getMyCampaigns(1, 50),
  });

  const { data: contribData, isLoading } = useQuery({
    queryKey: ["m-contributions", page],
    queryFn: () => getMyContributions({ page, pageSize }),
    placeholderData: (prev) => prev,
  });

  // Fetch all contributions to detect membership payment status across all campaigns
  const { data: allContribData } = useQuery({
    queryKey: ["m-contributions-all"],
    queryFn: () => getMyContributions({ pageSize: 500 }),
    staleTime: 30_000,
  });

  const { data: profile } = useQuery({
    queryKey: ["m-profile"],
    queryFn: getMyProfile,
  });

  const isPensioner = profile?.employmentStatus === "Pensioner";
  const getMemberAmount = useCallback((c: Campaign) =>
    isPensioner && c.pensionerAmountPerMember != null ? c.pensionerAmountPerMember : c.amountPerMember, [isPensioner]);

  const qc = useQueryClient();

  const [pendingReference, setPendingReference] = useState<string | null>(null);
  const [modalOpen, setModalOpen] = useState(false);

  const payMut = useMutation({
    mutationFn: ({ campaignId, amount, isMembership }: { campaignId: string; amount: number; isMembership?: boolean }) => {
      if (isMembership) {
        return renewMembership(campaignId, 1, "online");
      }
      return initiatePaystackPayment({ campaignId, amount });
    },
    onSuccess: (data: { authorizationUrl?: string; reference?: string }) => {
      if (data?.authorizationUrl) {
        if (data.reference) {
          localStorage.setItem("umat-paystack-pending-ref", data.reference);
          setPendingReference(data.reference);
        }
        window.location.href = data.authorizationUrl;
      } else {
        toast.success("Payment initiated successfully.");
        qc.invalidateQueries({ queryKey: ["m-contributions"] });
        qc.invalidateQueries({ queryKey: ["m-campaigns"] });
      }
    },
    onError: (e) => toast.error(handleApiError(e)),
    onSettled: () => setPayingCampaignId(null),
  });

  const openStatusModal = (reference: string) => {
    localStorage.setItem("umat-paystack-pending-ref", reference);
    setPendingReference(reference);
    setModalOpen(true);
  };

  useEffect(() => {
    const stored = localStorage.getItem("umat-paystack-pending-ref");
    if (stored) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setPendingReference(stored);
    }
  }, []);

  useEffect(() => {
    // Always show modal when we know there is a pending reference.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setModalOpen(!!pendingReference);
  }, [pendingReference]);

  const closeModal = () => {
    setModalOpen(false);
    setPendingReference(null);
    localStorage.removeItem("umat-paystack-pending-ref");
  };

  const allContributions = allContribData?.results ?? contribData?.results ?? [];

  const activeCampaigns = (campaignsData?.results ?? []).filter((c) => c.status === "Active")
    .sort((a, b) => {
      // Sort: unpaid membership first, then unpaid regular, then paid
      const aHasPaid = allContributions.some((co) => co.campaignId === a.id && (co.status === "Confirmed" || co.status === "Pending"));
      const bHasPaid = allContributions.some((co) => co.campaignId === b.id && (co.status === "Confirmed" || co.status === "Pending"));
      if (aHasPaid !== bHasPaid) return aHasPaid ? 1 : -1;
      // Among unpaid, membership campaigns first
      if (a.isMembershipCampaign !== b.isMembershipCampaign) return a.isMembershipCampaign ? -1 : 1;
      return 0;
    });
  const contributions = contribData?.results ?? [];
  const totalPages = contribData?.totalPages ?? 1;
  const totalPaid = allContributions.filter((c) => c.status === "Confirmed").reduce((sum, c) => sum + c.amount, 0);

  const [payingCampaignId, setPayingCampaignId] = useState<string | null>(null);

  return (
    <div className="p-2 sm:p-4 lg:p-6 space-y-6 sm:space-y-8 max-w-7xl mx-auto">
      <header className="flex flex-col md:flex-row md:items-end justify-between gap-4 animate-in fade-in slide-in-from-bottom-3 duration-700">
        <div className="space-y-1.5">
          <div className="flex items-center gap-2 mb-2">
            <Badge variant="outline" className="text-[10px] font-bold uppercase tracking-widest border-primary/20 text-primary bg-primary/5 px-2 py-0.5">Financial support</Badge>
          </div>
          <h1 className="text-2xl sm:text-3xl lg:text-4xl font-extrabold tracking-tight text-foreground">Member Contributions</h1>
          <p className="text-muted-foreground text-sm sm:text-base max-w-2xl font-medium leading-relaxed">Fueling the future of UMaT through collective impact. Every contribution counts toward our shared legacy.</p>
        </div>
      </header>

      {activeCampaigns.length > 0 && (
        <div className="space-y-8">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-2 h-8 bg-primary rounded-full shadow-[0_0_15px_rgba(var(--primary-rgb),0.4)]" />
              <h2 className="text-2xl font-extrabold tracking-tight">Active Campaigns</h2>
            </div>
          </div>
          <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
            {activeCampaigns.map((c, i) => {
              const isMembership = !!c.isMembershipCampaign;
              const pct = isMembership && c.totalEligibleMembers
                ? Math.round((c.paidCount / c.totalEligibleMembers) * 100)
                : c.targetAmount > 0 ? Math.round((c.collectedAmount / c.targetAmount) * 100) : 0;
              const myPayment = contributions
                .filter((co) => co.campaignId === c.id && co.status !== "Rejected")
                .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())[0];
              const membershipPaid = isMembership && myPayment && (myPayment.status === "Confirmed" || myPayment.status === "Pending");
              return (
                <Card key={c.id} className="group relative overflow-hidden border-border/40 hover:border-primary/30 transition-all duration-500 bg-card flex flex-col lg:flex-row items-stretch h-auto shadow-sm hover:shadow-2xl hover:shadow-primary/5 animate-in fade-in slide-in-from-bottom-6 duration-700" style={{ animationDelay: `${i * 150}ms` }}>
                  {/* Visual Brand Mark */}
                  <div className="absolute top-0 right-0 w-28 h-28 bg-primary/6 rounded-bl-full -mr-14 -mt-14 transition-transform duration-700 group-hover:scale-105" />
                  
                  {/* Left Side: Visual Content */}
                  <div className="relative w-full lg:w-1/2 h-48 sm:h-56 lg:h-auto min-h-0 lg:min-h-[280px] overflow-hidden shrink-0">
                    {c.bannerImageUrl ? (
                      <img
                        src={c.bannerImageUrl}
                        alt={c.title}
                        className="absolute inset-0 min-w-full min-h-full w-full h-full object-cover object-center transition-transform duration-1000 group-hover:scale-105"
                      />
                    ) : (
                      <div className="absolute inset-0 w-full h-full bg-gradient-to-br from-primary/20 via-primary/5 to-muted/20 flex items-center justify-center">
                        <CreditCard size={56} className="text-primary/20" />
                      </div>
                    )}
                    <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-black/20 to-transparent sm:bg-gradient-to-r sm:from-black/40 sm:to-transparent" />

                    <div className="absolute bottom-3 left-3 right-3 sm:bottom-4 sm:left-4 sm:right-4">
                      <Badge className="bg-white/10 backdrop-blur-md border border-white/20 text-white font-bold text-[10px] uppercase tracking-widest px-2 py-1">
                        {isMembership ? `${c.paidCount}/${c.totalEligibleMembers ?? '?'} paid` : `${pct}% Funded`}
                      </Badge>
                    </div>
                  </div>

                  {/* Right Side: Content & Actions */}
                  <div className="flex-1 p-6 lg:p-8 flex flex-col relative z-10 min-w-0">
                    <div className="flex justify-between items-start gap-3 mb-2 min-w-0">
                      <div className="flex items-center gap-1.5 shrink-0">
                        {isMembership && (
                          <Badge variant="outline" className="font-bold uppercase tracking-wider text-[9px] px-2 py-0.5 border-primary/30 text-primary bg-primary/5">Membership</Badge>
                        )}
                        {myPayment && (
                          <Badge variant={statusVariant[myPayment.status]} className="font-bold uppercase tracking-wider text-[9px] px-2 py-0.5 shadow-sm shadow-current/10">
                            {myPayment.status}
                          </Badge>
                        )}
                      </div>
                    </div>
                                          <h3 className="text-xl font-bold leading-tight group-hover:text-primary transition-colors min-w-0 flex-1 break-words">{c.title}</h3>


                    <p className="text-sm text-muted-foreground font-medium line-clamp-2 mb-3 leading-relaxed">
                      {c.description || "No description provided for this campaign."}
                    </p>

                    <div className="space-y-2 mt-auto">
                      <div className="space-y-1.5 min-w-0">
                        <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 text-[11px] font-bold uppercase tracking-wider min-w-0">
                          <span className="text-primary min-w-0 break-words">{isMembership ? `${c.paidCount} members paid` : `${formatCurrency(c.collectedAmount)} raised`}</span>
                          <span className="text-muted-foreground/60 shrink-0 whitespace-nowrap">{isMembership ? `${c.totalEligibleMembers ?? '?'} eligible members` : `Goal: ${formatCurrency(c.targetAmount)}`}</span>
                        </div>
                        <Progress value={pct} className="h-1.5 rounded-full bg-primary/10 overflow-hidden">
                          <div className="h-full bg-primary relative">
                            <div className="absolute inset-0 bg-[linear-gradient(45deg,rgba(255,255,255,0.2)_25%,transparent_25%,transparent_50%,rgba(255,255,255,0.2)_50%,rgba(255,255,255,0.2)_75%,transparent_75%,transparent)] bg-[length:20px_20px] animate-shimmer" />
                          </div>
                        </Progress>
                      </div>

                      <div className="flex items-center justify-between gap-3 pt-4 border-t border-border/40 min-w-0">
                        <div>
                          <p className="text-[10px] font-bold text-muted-foreground tracking-widest mb-0.5">{isMembership ? "Your amount" : "Suggested target"}</p>
                          <p className="text-sm text-foreground/80 leading-tight">{isMembership ? formatCurrency(getMemberAmount(c)) : formatCurrency(c.amountPerMember)}</p>
                        </div>
                        <div className="text-right shrink-0">
                          <p className="text-[10px] font-bold text-muted-foreground tracking-widest mb-0.5">Deadline</p>
                          <p className="text-[13px] font-bold text-foreground/80 whitespace-nowrap">{formatDate(c.deadline)}</p>
                        </div>
                      </div>

                      <div className="space-y-3 pt-4 min-w-0">
                        {membershipPaid && (
                          <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-green-50 border border-green-200 text-green-800 dark:bg-green-950/30 dark:border-green-800 dark:text-green-300">
                            <CheckCircle2 size={16} />
                            <span className="text-xs font-bold">Membership paid — this is a one-time payment.</span>
                          </div>
                        )}
                        <div className="flex flex-wrap items-stretch gap-2">
                          <Link href={`/contributions/${c.id}`} className="flex-1 min-w-[9rem] no-card-click">
                            <Button variant="outline" className="w-full h-10 font-bold text-[13px] border-border/60 hover:bg-primary/5 hover:border-primary/30 hover:text-primary transition-all">
                              View Details
                            </Button>
                          </Link>

                          {membershipPaid && (
                            <Link href="/membership-certificate" className="flex-1 min-w-[9rem] no-card-click">
                              <Button variant="outline" className="w-full h-10 font-bold text-[13px] border-amber-300 text-amber-700 hover:bg-amber-50 dark:border-amber-700 dark:text-amber-400 dark:hover:bg-amber-950/30 transition-all">
                                <Award size={16} className="mr-2" />
                                Certificate
                              </Button>
                            </Link>
                          )}

                          {c.allowOnlinePayments && !membershipPaid && (
                          <Button
                            className="w-full h-12 rounded-2xl bg-primary text-white font-black text-sm shadow-xl shadow-primary/20 hover:bg-primary/90 transition-all"
                            onClick={(e) => {
                              e.stopPropagation();
                              const amount = isMembership ? getMemberAmount(c) : c.amountPerMember;
                              if (amount > 0) {
                                setPayingCampaignId(c.id);
                                payMut.mutate({ campaignId: c.id, amount, isMembership });
                              }
                            }}
                            disabled={payMut.isPending}
                          >
                            {payingCampaignId === c.id && payMut.isPending ? (
                              <span className="inline-flex items-center gap-2">
                                <Loader2 size={16} className="animate-spin" />
                                Processing payment...
                              </span>
                            ) : (
                              <span className="inline-flex items-center justify-center gap-2">
                                <CreditCard size={16} />
                                Pay {formatCurrency(isMembership ? getMemberAmount(c) : c.amountPerMember)} now
                              </span>
                            )}
                          </Button>
                        )}
                        </div>

                        {myPayment?.status === "Pending" && myPayment.transactionRef && (
                          <div className="flex items-center gap-2 pt-2">
                            <Button
                              size="sm"
                              variant="outline"
                              className="w-full no-card-click"
                              onClick={(e) => {
                                e.stopPropagation();
                                openStatusModal(myPayment.transactionRef!);
                              }}
                            >
                              Check Payment Status
                            </Button>
                            <span className="text-[11px] text-muted-foreground">Pending status—open the status modal to keep checking.</span>
                          </div>
                        )}
                      </div>
                    </div>
                  </div>
                </Card>
              );
            })}
          </div>
        </div>
      )}

      <div className="space-y-6">
        <div className="flex items-center justify-between border-b border-border/30 pb-4">
          <div className="flex items-center gap-3">
            <div className="w-1.5 h-6 bg-primary rounded-full" />
            <h2 className="text-xl font-bold tracking-tight">Payment History</h2>
          </div>
          <div className="text-right">
            <p className="text-[10px] font-bold text-muted-foreground uppercase tracking-widest mb-1">Total Confirmed</p>
            <p className="text-xl font-extrabold text-primary">{formatCurrency(totalPaid)}</p>
          </div>
        </div>
        
        <section className="border border-border/45 rounded-xl overflow-hidden bg-background">
            <div className="overflow-x-auto">
              <Table className="min-w-[860px]">
                <TableHeader className="bg-muted/30">
                  <TableRow>
                    <TableHead className="py-4 font-bold uppercase tracking-wider text-[11px]">Campaign / Initiative</TableHead>
                    <TableHead className="py-4 font-bold uppercase tracking-wider text-[11px]">Amount</TableHead>
                    <TableHead className="py-4 font-bold uppercase tracking-wider text-[11px]">Payment Method</TableHead>
                    <TableHead className="py-4 font-bold uppercase tracking-wider text-[11px]">Status</TableHead>
                    <TableHead className="py-4 font-bold uppercase tracking-wider text-[11px] text-right">Date</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {isLoading ? (
                    <TableSkeleton rows={5} cols={5} />
                  ) : contributions.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={5} className="text-center py-24">
                        <div className="space-y-3 max-w-xs mx-auto">
                          <div className="w-16 h-16 bg-muted/30 rounded-full flex items-center justify-center mx-auto text-muted-foreground/30">
                            <CreditCard size={32} />
                          </div>
                          <p className="text-muted-foreground font-medium">Your contribution history is empty.</p>
                          <p className="text-[12px] text-muted-foreground/60">Start by supporting one of the active campaigns above.</p>
                        </div>
                      </TableCell>
                    </TableRow>
                  ) : contributions.map((c) => (
                    <TableRow key={c.id} className="hover:bg-muted/40 transition-colors group">
                      <TableCell className="py-5">
                        <p className="font-bold text-sm group-hover:text-primary transition-colors line-clamp-1">{c.campaignTitle ?? "Unknown Campaign"}</p>
                        <p className="text-[11px] text-muted-foreground/60 font-medium">Ref: {c.id.slice(0, 8)}</p>
                      </TableCell>
                      <TableCell className="py-5 font-bold text-sm">{formatCurrency(c.amount)}</TableCell>
                      <TableCell className="py-5">
                        <Badge variant="secondary" className="font-bold text-[10px] uppercase tracking-wider">{c.paymentMethod}</Badge>
                      </TableCell>
                      <TableCell className="py-5">
                        <Badge variant={statusVariant[c.status]} className="font-bold text-[10px] uppercase tracking-wider">{c.status}</Badge>
                      </TableCell>
                      <TableCell className="py-5 text-right font-medium text-muted-foreground text-[13px]">
                        {formatDate(c.confirmedAt ?? c.createdAt)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
        </section>
        <div className="pt-4">
          <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
        </div>
      </div>
      <PaymentStatusModal
        reference={pendingReference}
        open={modalOpen}
        onClose={closeModal}
        onConfirmed={() => {
          closeModal();
          qc.invalidateQueries({ queryKey: ["m-contributions", page] });
        }}
      />
    </div>
  );
}
