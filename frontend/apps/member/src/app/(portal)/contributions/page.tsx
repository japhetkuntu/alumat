"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  CheckCircle2, CreditCard, Loader2, RefreshCcw,
  XCircle, Award, TrendingUp, ChevronRight, AlertCircle,
} from "lucide-react";
import { Pagination } from "@/components/ui/pagination";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { TableSkeleton } from "@/components/ui/skeleton";
import {
  Dialog, DialogContent, DialogDescription,
  DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog";
import { formatCurrency, formatDate } from "@/lib/utils";
import { cn } from "@/lib/utils";
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
  Pending:   "warning",
  Rejected:  "destructive",
};

type PollStatus = "loading" | "pending" | "success" | "error";

/* ─────────────────────────────────────────────────────────────────────────
   PAYMENT STATUS MODAL
   ───────────────────────────────────────────────────────────────────────── */
function PaymentStatusModal({
  reference, open, onClose, onConfirmed,
}: {
  reference: string | null;
  open: boolean;
  onClose: () => void;
  onConfirmed: () => void;
}) {
  const [status, setStatus]   = useState<PollStatus>("loading");
  const [message, setMessage] = useState("");
  const [seconds, setSeconds] = useState(10);

  const reset = useCallback(() => { setStatus("loading"); setMessage(""); setSeconds(10); }, []);

  const poll = useCallback(async () => {
    if (!reference) return;
    setStatus("loading");
    try {
      const res = await getPaystackPaymentStatus(reference);
      const n = res.status?.toLowerCase() ?? "unknown";
      if (n === "confirmed" || n === "success") {
        setStatus("success"); setMessage(res.message ?? "Payment confirmed."); onConfirmed(); return;
      }
      if (n === "pending" || n === "unknown") {
        setStatus("pending"); setMessage(res.message ?? "Still waiting for confirmation. Check again shortly."); return;
      }
      setStatus("error"); setMessage(res.message ?? `Payment status: ${res.status}`);
    } catch (err) {
      setStatus("error"); setMessage(handleApiError(err));
    }
  }, [reference, onConfirmed]);

  useEffect(() => { if (!open) return; reset(); poll(); }, [open, reset, poll]);

  useEffect(() => {
    if (!open || status === "success" || status === "error") return;
    const id = setInterval(() => setSeconds(p => { if (p <= 1) { poll(); return 10; } return p - 1; }), 1000);
    return () => clearInterval(id);
  }, [open, status, poll]);

  const Icon = status === "loading" ? Loader2
    : status === "success" ? CheckCircle2
    : status === "error"   ? XCircle
    : AlertCircle;

  const iconColor = status === "success" ? "text-green-600"
    : status === "error" ? "text-destructive"
    : status === "pending" ? "text-amber-500"
    : "text-primary";

  const title = {
    loading: "Verifying payment…",
    pending: "Payment pending",
    success: "Payment confirmed",
    error:   "Something went wrong",
  }[status];

  return (
    <Dialog open={open} onOpenChange={open => !open && onClose()}>
      <DialogContent className="max-w-sm">
        <DialogHeader className="items-center text-center gap-3">
          <div className={cn("w-14 h-14 rounded-full flex items-center justify-center",
            status === "success" ? "bg-green-50" :
            status === "error"   ? "bg-destructive/10" :
            status === "pending" ? "bg-amber-50" : "bg-primary/10")}>
            <Icon size={28} className={cn(iconColor, status === "loading" && "animate-spin")} />
          </div>
          <DialogTitle className="text-[17px]">{title}</DialogTitle>
          {message && (
            <DialogDescription className="text-[13.5px] text-center leading-relaxed">
              {message}
            </DialogDescription>
          )}
        </DialogHeader>
        <DialogFooter className="flex-col sm:flex-row gap-2 mt-2">
          <Button
            variant="outline"
            onClick={() => poll()}
            disabled={status === "loading"}
            className="flex-1 gap-2 font-semibold"
          >
            <RefreshCcw size={14} className={status === "loading" ? "animate-spin" : ""} />
            {status === "loading" ? "Checking…" : `Check again (${seconds}s)`}
          </Button>
          <Button onClick={onClose} className="flex-1 font-semibold">
            Done
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   CAMPAIGN CARD
   ───────────────────────────────────────────────────────────────────────── */
function CampaignCard({
  campaign,
  myPayment,
  membershipPaid,
  isPensioner,
  getMemberAmount,
  onPay,
  onCheckStatus,
  isPaying,
}: {
  campaign: Campaign;
  myPayment: any;
  membershipPaid: boolean;
  isPensioner: boolean;
  getMemberAmount: (c: Campaign) => number;
  onPay: () => void;
  onCheckStatus: () => void;
  isPaying: boolean;
}) {
  const c = campaign;
  const isMembership = !!c.isMembershipCampaign;
  const amount = getMemberAmount(c);

  const pct = isMembership && c.totalEligibleMembers
    ? Math.round((c.paidCount / c.totalEligibleMembers) * 100)
    : c.targetAmount > 0 ? Math.round((c.collectedAmount / c.targetAmount) * 100) : 0;

  return (
    <div
      className="rounded-2xl border overflow-hidden"
      style={{ borderColor: "var(--border)", background: "var(--background)" }}
    >
      {/* Banner image — fixed height, never breaks layout */}
      {c.bannerImageUrl && (
        <div className="relative w-full overflow-hidden" style={{ height: 160 }}>
          <img
            src={c.bannerImageUrl}
            alt={c.title}
            className="absolute inset-0 w-full h-full object-cover"
          />
          <div
            className="absolute inset-0"
            style={{ background: "linear-gradient(to top, rgba(0,0,0,0.45) 0%, transparent 55%)" }}
          />
          {/* Badges over image */}
          <div className="absolute bottom-3 left-4 flex items-center gap-2">
            {isMembership && (
              <span
                className="inline-flex items-center px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wide text-white"
                style={{ background: "rgba(255,255,255,0.15)", backdropFilter: "blur(8px)", border: "1px solid rgba(255,255,255,0.25)" }}
              >
                Membership
              </span>
            )}
          </div>
        </div>
      )}

      {/* Card body */}
      <div className="p-5 space-y-4">

        {/* Title + status badge */}
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0 flex-1">
            {!c.bannerImageUrl && isMembership && (
              <p className="text-[10.5px] font-bold tracking-[0.1em] uppercase mb-1.5" style={{ color: "var(--primary)" }}>
                Membership campaign
              </p>
            )}
            <h3
              className="font-[family-name:var(--font-display)] leading-snug"
              style={{ fontSize: "1rem", fontWeight: 700, color: "var(--foreground)" }}
            >
              {c.title}
            </h3>
            {c.description && (
              <p className="text-[13px] mt-1.5 line-clamp-2 leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
                {c.description}
              </p>
            )}
          </div>
          {myPayment && (
            <Badge variant={statusVariant[myPayment.status]} className="text-[11px] font-bold shrink-0">
              {myPayment.status}
            </Badge>
          )}
        </div>

        {/* Progress */}
        <div className="space-y-1.5">
          <Progress value={pct} className="h-2" />
          <div className="flex items-center justify-between text-[12px]" style={{ color: "var(--muted-foreground)" }}>
            <span>
              {isMembership
                ? `${c.paidCount} of ${c.totalEligibleMembers ?? "?"} members paid`
                : `${formatCurrency(c.collectedAmount)} raised of ${formatCurrency(c.targetAmount)}`}
            </span>
            <span className="font-semibold">{pct}%</span>
          </div>
        </div>

        {/* Amount + deadline row */}
        <div
          className="flex items-center justify-between py-3 px-4 rounded-xl"
          style={{ background: "var(--secondary)" }}
        >
          <div>
            <p className="text-[11px] font-semibold uppercase tracking-wide mb-0.5" style={{ color: "var(--muted-foreground)" }}>
              {isMembership ? "Your amount" : "Per member"}
            </p>
            <p className="text-[17px] font-bold tabular-nums" style={{ color: "var(--foreground)" }}>
              {formatCurrency(amount)}
              {isPensioner && isMembership && (
                <span className="text-[11px] font-normal ml-1.5" style={{ color: "var(--muted-foreground)" }}>
                  pensioner rate
                </span>
              )}
            </p>
          </div>
          <div className="text-right">
            <p className="text-[11px] font-semibold uppercase tracking-wide mb-0.5" style={{ color: "var(--muted-foreground)" }}>
              Due
            </p>
            <p className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>
              {formatDate(c.deadline)}
            </p>
          </div>
        </div>

        {/* Membership paid confirmation */}
        {membershipPaid && (
          <div
            className="flex items-center gap-2.5 px-3 py-2.5 rounded-xl"
            style={{ background: "rgba(34,197,94,0.08)", border: "1px solid rgba(34,197,94,0.2)" }}
          >
            <CheckCircle2 size={16} className="text-green-600 shrink-0" />
            <p className="text-[13px] font-semibold text-green-700">
              Membership paid for this year.
            </p>
          </div>
        )}

        {/* Pending payment check */}
        {myPayment?.status === "Pending" && myPayment.transactionRef && (
          <div
            className="flex items-center justify-between gap-3 px-3 py-2.5 rounded-xl"
            style={{ background: "rgba(245,158,11,0.08)", border: "1px solid rgba(245,158,11,0.2)" }}
          >
            <p className="text-[13px] font-medium" style={{ color: "var(--muted-foreground)" }}>
              Payment is being processed.
            </p>
            <Button
              size="sm"
              variant="outline"
              className="shrink-0 font-semibold text-[12.5px] gap-1.5"
              style={{ height: 34 }}
              onClick={onCheckStatus}
            >
              <RefreshCcw size={12} />
              Check status
            </Button>
          </div>
        )}

        {/* Action buttons */}
        <div className="flex flex-wrap gap-2 pt-1">
          <Link href={`/contributions/${c.id}`} className="flex-1 min-w-[8rem]">
            <Button
              variant="outline"
              className="w-full font-semibold text-[13.5px]"
              style={{ height: 42 }}
            >
              View details
            </Button>
          </Link>

          {membershipPaid && (
            <Link href="/membership-certificate" className="flex-1 min-w-[8rem]">
              <Button
                variant="outline"
                className="w-full font-semibold text-[13.5px] gap-1.5"
                style={{ height: 42, borderColor: "#d97706", color: "#b45309" }}
              >
                <Award size={14} />
                Certificate
              </Button>
            </Link>
          )}

          {c.allowOnlinePayments && !membershipPaid && (
            <Button
              className="flex-1 min-w-[8rem] font-bold text-[13.5px] gap-2"
              style={{ height: 42 }}
              onClick={onPay}
              disabled={isPaying}
            >
              {isPaying
                ? <><Loader2 size={14} className="animate-spin" /> Processing…</>
                : <><CreditCard size={14} /> Pay {formatCurrency(amount)}</>}
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────
   PAGE
   ───────────────────────────────────────────────────────────────────────── */
export default function MemberContributionsPage() {
  const [page, setPage]               = useState(1);
  const [payingCampaignId, setPaying] = useState<string | null>(null);
  const [pendingReference, setPendingRef] = useState<string | null>(null);
  const [modalOpen, setModalOpen]     = useState(false);
  const pageSize = 20;
  const qc = useQueryClient();

  const { data: campaignsData } = useQuery({
    queryKey: ["m-campaigns"],
    queryFn:  () => getMyCampaigns(1, 50),
  });

  const { data: contribData, isLoading } = useQuery({
    queryKey:        ["m-contributions", page],
    queryFn:         () => getMyContributions({ page, pageSize }),
    placeholderData: (prev) => prev,
  });

  const { data: allContribData } = useQuery({
    queryKey:  ["m-contributions-all"],
    queryFn:   () => getMyContributions({ pageSize: 500 }),
    staleTime: 30_000,
  });

  const { data: profile } = useQuery({
    queryKey: ["m-profile"],
    queryFn:  getMyProfile,
  });

  const isPensioner = profile?.employmentStatus === "Pensioner";
  const getMemberAmount = useCallback((c: Campaign) =>
    isPensioner && c.pensionerAmountPerMember != null ? c.pensionerAmountPerMember : c.amountPerMember,
  [isPensioner]);

  const allContributions = allContribData?.results ?? contribData?.results ?? [];

  const activeCampaigns = (campaignsData?.results ?? [])
    .filter((c) => c.status === "Active")
    .sort((a, b) => {
      const aHasPaid = allContributions.some((co) => co.campaignId === a.id && co.status !== "Rejected");
      const bHasPaid = allContributions.some((co) => co.campaignId === b.id && co.status !== "Rejected");
      if (aHasPaid !== bHasPaid) return aHasPaid ? 1 : -1;
      if (a.isMembershipCampaign !== b.isMembershipCampaign) return a.isMembershipCampaign ? -1 : 1;
      return 0;
    });

  const contributions = contribData?.results ?? [];
  const totalPages    = contribData?.totalPages ?? 1;
  const totalPaid     = allContributions
    .filter((c) => c.status === "Confirmed")
    .reduce((sum, c) => sum + c.amount, 0);

  const payMut = useMutation({
    mutationFn: ({ campaignId, amount, isMembership }: { campaignId: string; amount: number; isMembership?: boolean }) =>
      isMembership ? renewMembership(campaignId, 1, "online") : initiatePaystackPayment({ campaignId, amount }),
    onSuccess: (data: { authorizationUrl?: string; reference?: string }) => {
      if (data?.authorizationUrl) {
        if (data.reference) {
          localStorage.setItem("umat-paystack-pending-ref", data.reference);
          setPendingRef(data.reference);
        }
        window.location.href = data.authorizationUrl;
      } else {
        toast.success("Payment initiated.");
        qc.invalidateQueries({ queryKey: ["m-contributions"] });
        qc.invalidateQueries({ queryKey: ["m-campaigns"] });
      }
    },
    onError:   (e) => toast.error(handleApiError(e)),
    onSettled: () => setPaying(null),
  });

  const openStatusModal = (reference: string) => {
    localStorage.setItem("umat-paystack-pending-ref", reference);
    setPendingRef(reference);
    setModalOpen(true);
  };

  useEffect(() => {
    const stored = localStorage.getItem("umat-paystack-pending-ref");
    if (stored) setPendingRef(stored);
  }, []);

  useEffect(() => { setModalOpen(!!pendingReference); }, [pendingReference]);

  const closeModal = () => {
    setModalOpen(false);
    setPendingRef(null);
    localStorage.removeItem("umat-paystack-pending-ref");
  };

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-8 sm:space-y-10">

      {/* ── Page header ── */}
      <div className="animate-in fade-in slide-in-from-bottom-3 duration-500">
        <h1
          className="font-[family-name:var(--font-display)] tracking-tight"
          style={{ fontSize: "clamp(1.5rem, 3vw, 2rem)", fontWeight: 700, color: "var(--foreground)" }}
        >
          Campaigns & contributions
        </h1>
        <p className="mt-1 text-[14px]" style={{ color: "var(--muted-foreground)" }}>
          Support active campaigns and track your payment history.
        </p>
      </div>

      {/* ── Active campaigns ── */}
      {activeCampaigns.length > 0 && (
        <section className="space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-[15px] font-bold" style={{ color: "var(--foreground)" }}>
              Active campaigns
            </h2>
            <span
              className="w-6 h-6 rounded-full flex items-center justify-center text-[11px] font-bold"
              style={{ background: "var(--color-background-info)", color: "var(--primary)", border: "1px solid var(--color-border-info)" }}
            >
              {activeCampaigns.length}
            </span>
          </div>

          <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
            {activeCampaigns.map((c) => {
              const myPayment = allContributions
                .filter((co) => co.campaignId === c.id && co.status !== "Rejected")
                .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())[0];
              const membershipPaid = !!c.isMembershipCampaign && !!myPayment &&
                (myPayment.status === "Confirmed" || myPayment.status === "Pending");

              return (
                <CampaignCard
                  key={c.id}
                  campaign={c}
                  myPayment={myPayment}
                  membershipPaid={membershipPaid}
                  isPensioner={isPensioner}
                  getMemberAmount={getMemberAmount}
                  isPaying={payingCampaignId === c.id && payMut.isPending}
                  onPay={() => {
                    const amount = getMemberAmount(c);
                    if (amount > 0) {
                      setPaying(c.id);
                      payMut.mutate({ campaignId: c.id, amount, isMembership: !!c.isMembershipCampaign });
                    }
                  }}
                  onCheckStatus={() => myPayment?.transactionRef && openStatusModal(myPayment.transactionRef)}
                />
              );
            })}
          </div>
        </section>
      )}

      {/* ── Payment history ── */}
      <section className="space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-[15px] font-bold" style={{ color: "var(--foreground)" }}>
              Payment history
            </h2>
            <p className="text-[13px] mt-0.5" style={{ color: "var(--muted-foreground)" }}>
              All your contributions over time
            </p>
          </div>
          {totalPaid > 0 && (
            <div className="text-right">
              <p className="text-[11px] font-semibold uppercase tracking-wide mb-0.5" style={{ color: "var(--muted-foreground)" }}>
                Total confirmed
              </p>
              <p
                className="font-[family-name:var(--font-display)] font-bold tabular-nums"
                style={{ fontSize: "1.25rem", color: "var(--primary)", letterSpacing: "-0.02em" }}
              >
                {formatCurrency(totalPaid)}
              </p>
            </div>
          )}
        </div>

        {/* Table — desktop */}
        <div
          className="hidden sm:block rounded-2xl border overflow-hidden"
          style={{ borderColor: "var(--border)" }}
        >
          <div className="overflow-x-auto">
            <table className="w-full min-w-[600px] border-collapse">
              <thead>
                <tr style={{ background: "var(--secondary)", borderBottom: "1px solid var(--border)" }}>
                  {["Campaign", "Amount", "Method", "Status", "Date"].map((h) => (
                    <th
                      key={h}
                      className="text-left px-5 py-3.5 text-[11px] font-bold uppercase tracking-[0.08em]"
                      style={{ color: "var(--muted-foreground)" }}
                    >
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {isLoading ? (
                  <tr>
                    <td colSpan={5} className="px-5 py-16 text-center">
                      <Loader2 size={24} className="animate-spin mx-auto" style={{ color: "var(--primary)" }} />
                    </td>
                  </tr>
                ) : contributions.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="px-5 py-16 text-center">
                      <div className="space-y-2 max-w-xs mx-auto">
                        <div
                          className="w-12 h-12 rounded-full flex items-center justify-center mx-auto"
                          style={{ background: "var(--secondary)" }}
                        >
                          <TrendingUp size={20} style={{ color: "var(--muted-foreground)" }} />
                        </div>
                        <p className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>
                          No payments yet
                        </p>
                        <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>
                          Support a campaign above to get started.
                        </p>
                      </div>
                    </td>
                  </tr>
                ) : contributions.map((c, i) => (
                  <tr
                    key={c.id}
                    className="transition-colors"
                    style={{
                      borderBottom: i < contributions.length - 1 ? "1px solid var(--border)" : "none",
                      background: "var(--background)",
                    }}
                    onMouseEnter={e => (e.currentTarget.style.background = "var(--secondary)")}
                    onMouseLeave={e => (e.currentTarget.style.background = "var(--background)")}
                  >
                    <td className="px-5 py-4">
                      <p className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>
                        {c.campaignTitle ?? "Contribution"}
                      </p>
                      <p className="text-[11.5px] mt-0.5 font-mono" style={{ color: "var(--muted-foreground)" }}>
                        #{c.id.slice(0, 8)}
                      </p>
                    </td>
                    <td className="px-5 py-4">
                      <span className="text-[14px] font-bold tabular-nums" style={{ color: "var(--foreground)" }}>
                        {formatCurrency(c.amount)}
                      </span>
                    </td>
                    <td className="px-5 py-4">
                      <Badge variant="secondary" className="text-[11px] font-semibold">
                        {c.paymentMethod}
                      </Badge>
                    </td>
                    <td className="px-5 py-4">
                      <Badge variant={statusVariant[c.status]} className="text-[11px] font-semibold">
                        {c.status}
                      </Badge>
                    </td>
                    <td className="px-5 py-4 text-right">
                      <span className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>
                        {formatDate(c.confirmedAt ?? c.createdAt)}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Mobile list — stacked cards instead of table */}
        <div className="sm:hidden space-y-2">
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 size={24} className="animate-spin" style={{ color: "var(--primary)" }} />
            </div>
          ) : contributions.length === 0 ? (
            <div className="py-12 text-center space-y-2">
              <div
                className="w-12 h-12 rounded-full flex items-center justify-center mx-auto"
                style={{ background: "var(--secondary)" }}
              >
                <TrendingUp size={20} style={{ color: "var(--muted-foreground)" }} />
              </div>
              <p className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>No payments yet</p>
              <p className="text-[13px]" style={{ color: "var(--muted-foreground)" }}>
                Support a campaign above to get started.
              </p>
            </div>
          ) : contributions.map((c) => (
            <div
              key={c.id}
              className="rounded-xl border p-4 flex items-start gap-4"
              style={{ borderColor: "var(--border)", background: "var(--background)" }}
            >
              <div
                className="w-9 h-9 rounded-xl flex items-center justify-center shrink-0 mt-0.5"
                style={{ background: "var(--color-background-info)", border: "1px solid var(--color-border-info)" }}
              >
                <CreditCard size={16} style={{ color: "var(--primary)" }} />
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-start justify-between gap-2">
                  <p className="text-[14px] font-semibold leading-snug" style={{ color: "var(--foreground)" }}>
                    {c.campaignTitle ?? "Contribution"}
                  </p>
                  <p className="text-[14px] font-bold tabular-nums shrink-0" style={{ color: "var(--foreground)" }}>
                    {formatCurrency(c.amount)}
                  </p>
                </div>
                <div className="flex items-center gap-2 mt-1.5">
                  <Badge variant={statusVariant[c.status]} className="text-[10px] font-semibold">
                    {c.status}
                  </Badge>
                  <span className="text-[12px]" style={{ color: "var(--muted-foreground)" }}>
                    {formatDate(c.confirmedAt ?? c.createdAt)}
                  </span>
                </div>
              </div>
            </div>
          ))}
        </div>

        <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
      </section>

      <PaymentStatusModal
        reference={pendingReference}
        open={modalOpen}
        onClose={closeModal}
        onConfirmed={() => {
          closeModal();
          qc.invalidateQueries({ queryKey: ["m-contributions", page] });
          qc.invalidateQueries({ queryKey: ["m-contributions-all"] });
          qc.invalidateQueries({ queryKey: ["m-membership-status"] });
        }}
      />
    </div>
  );
}
