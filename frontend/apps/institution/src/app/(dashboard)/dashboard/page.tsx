"use client";

import { useState } from "react";
import { useQueries, useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Progress } from "@alumni/ui";
import { Skeleton } from "@alumni/ui";
import { StatCard, StatCardSkeleton } from "@alumni/ui";
import { TrendChart, DonutChart } from "@alumni/ui";
import { formatCurrency, formatDate } from "@alumni/ui";
import { Sparkles, Landmark, X } from "@alumni/ui";
import { getCampaigns, getContributions, getMembers, getEvents, getJobs, getBatches, getStoreOrders, getPayoutForecast, getInstitutionProfile } from "@/lib/institution-api";
import { useAuth } from "@/hooks/use-auth";

const DEFAULT_PRIMARY_COLOR = "#2563eb";
const SETUP_NUDGE_DISMISSED_KEY = "institution-setup-nudge-dismissed";

/**
 * A quiet, dismissible nudge for the two setup steps that actually matter —
 * a real brand identity and a working payout — shown only to a SuperAdmin
 * (both are SuperAdmin-only settings) and only while at least one is still
 * outstanding. Dismissal is per-browser (localStorage), not permanent: it
 * comes back on a fresh device/browser, which is deliberate — this is a
 * reminder, not a one-time tour, and a still-incomplete institution is worth
 * re-surfacing to whoever's looking.
 */
function SetupNudgeBanner() {
  const [dismissed, setDismissed] = useState(() => {
    if (typeof window === "undefined") return false;
    try { return localStorage.getItem(SETUP_NUDGE_DISMISSED_KEY) === "true"; } catch { return false; }
  });
  const { data: institution } = useQuery({ queryKey: ["institution-profile"], queryFn: getInstitutionProfile });

  if (dismissed || !institution) return null;

  const brandingIncomplete = !institution.logoUrl && (!institution.primaryColorHex || institution.primaryColorHex.toLowerCase() === DEFAULT_PRIMARY_COLOR);
  const payoutIncomplete = institution.payoutStatus === "None" || institution.payoutStatus === "Rejected";
  if (!brandingIncomplete && !payoutIncomplete) return null;

  function dismiss() {
    setDismissed(true);
    try { localStorage.setItem(SETUP_NUDGE_DISMISSED_KEY, "true"); } catch { /* ignore */ }
  }

  return (
    <div className="relative rounded-lg border border-accent/30 bg-accent/5 px-4 py-3.5 mb-4 pr-10">
      <button
        type="button"
        onClick={dismiss}
        aria-label="Dismiss"
        className="absolute top-3 right-3 text-muted-foreground hover:text-foreground"
      >
        <X size={14} />
      </button>
      <p className="text-[13px] font-semibold mb-2">Finish setting up your institution</p>
      <div className="flex flex-wrap gap-2.5">
        {brandingIncomplete && (
          <Link href="/settings" className="flex items-center gap-2 rounded-md border border-border/60 bg-background px-3 py-2 hover:border-accent/50 transition-colors">
            <Sparkles size={14} className="text-accent shrink-0" />
            <span className="text-[12.5px]">
              <span className="font-semibold">Add your logo and colors</span>
              <span className="text-muted-foreground"> — right now members see the platform default.</span>
            </span>
          </Link>
        )}
        {payoutIncomplete && (
          <Link href="/settings" className="flex items-center gap-2 rounded-md border border-border/60 bg-background px-3 py-2 hover:border-accent/50 transition-colors">
            <Landmark size={14} className="text-accent shrink-0" />
            <span className="text-[12.5px]">
              <span className="font-semibold">Set up payouts</span>
              <span className="text-muted-foreground"> — dues and contributions can&apos;t settle to your account yet.</span>
            </span>
          </Link>
        )}
      </div>
    </div>
  );
}

const STATUS_COLORS: Record<string, string> = {
  Successful: "var(--success, #16a34a)",
  Pending: "var(--warning, #d97706)",
  Failed: "var(--destructive, #dc2626)",
  Rejected: "var(--destructive, #dc2626)",
};

// Paystack settles our subaccount split straight to the institution's own
// bank account at the start of every working day — SuperAdmins are the ones
// who reconcile that account, so this is the one dashboard panel scoped to
// them alone, not every Admin.
function PayoutPanel() {
  const { data, isLoading } = useQuery({ queryKey: ["payout-forecast"], queryFn: getPayoutForecast, staleTime: 5 * 60 * 1000 });

  if (isLoading) {
    return (
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5 mt-3.5">
        {Array.from({ length: 2 }).map((_, i) => <div key={i} className="card p-[18px] h-[118px] skeleton" />)}
      </div>
    );
  }
  if (!data) return null;

  if (!data.payoutsConfigured) {
    return (
      <div className="card p-[18px] mt-3.5">
        <p className="text-[13px] text-muted-foreground">
          Settlement banking isn&apos;t set up yet.{" "}
          <Link href="/settings" className="text-accent font-semibold hover:underline">Set up payouts</Link> to start seeing expected payouts here.
        </p>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5 mt-3.5">
      <div className="card p-[18px] min-w-0">
        <p className="text-[13px] font-semibold mb-3">Last payout</p>
        <p
          className="font-[family-name:var(--font-display)] font-bold tabular-nums text-foreground"
          style={{ fontSize: "clamp(1.1rem, 3vw, 1.6rem)", letterSpacing: "-0.02em", wordBreak: "break-word", overflowWrap: "anywhere" }}
        >
          {formatCurrency(data.lastPayout.amount)}
        </p>
        <p className="text-[12px] text-muted-foreground mt-1.5">
          Settled {formatDate(data.lastPayout.date)} &middot; should already be in your account &middot; {data.lastPayout.transactionCount} transaction{data.lastPayout.transactionCount === 1 ? "" : "s"}
        </p>
      </div>
      <div className="card p-[18px] min-w-0" style={{ borderColor: "var(--border-emphasis)" }}>
        <p className="text-[13px] font-semibold mb-3">Next payout</p>
        <p
          className="font-[family-name:var(--font-display)] font-bold tabular-nums text-foreground"
          style={{ fontSize: "clamp(1.1rem, 3vw, 1.6rem)", letterSpacing: "-0.02em", wordBreak: "break-word", overflowWrap: "anywhere" }}
        >
          {formatCurrency(data.nextPayout.amount)}
        </p>
        <p className="text-[12px] text-muted-foreground mt-1.5">
          Expected {formatDate(data.nextPayout.date)} morning &middot; still accumulating &middot; {data.nextPayout.transactionCount} transaction{data.nextPayout.transactionCount === 1 ? "" : "s"}
        </p>
      </div>
      <p className="sm:col-span-2 text-[11px] text-muted-foreground -mt-2">Estimated from your confirmed transactions, not a bank-confirmed figure.</p>
    </div>
  );
}

export default function AdminDashboardPage() {
  const { user, isScopedAdmin } = useAuth();
  const results = useQueries({
    queries: [
      { queryKey: ["dash-members-total"], queryFn: () => getMembers({ pageSize: 1 }) },
      { queryKey: ["dash-members-pending"], queryFn: () => getMembers({ pageSize: 20, status: "Pending" }) },
      { queryKey: ["dash-campaigns"], queryFn: () => getCampaigns(1, 100) },
      { queryKey: ["dash-contributions"], queryFn: () => getContributions({ pageSize: 500 }) },
      { queryKey: ["dash-events"], queryFn: () => getEvents(1, 1) },
      { queryKey: ["dash-jobs"], queryFn: () => getJobs(1, 1) },
      { queryKey: ["dash-batches"], queryFn: getBatches },
      { queryKey: ["dash-store-orders"], queryFn: () => getStoreOrders(1, 500) },
    ],
  });

  const [membersTotal, membersPending, campaigns, contributions, events, jobs, batches, storeOrders] = results;
  const hasNoBatches = !batches.isLoading && (batches.data?.length ?? 0) === 0;
  const isLoading = results.some((r) => r.isLoading);

  const totalMembers = membersTotal.data?.totalCount ?? 0;
  const pendingApprovals = membersPending.data?.totalCount ?? 0;
  const now = new Date();
  const allCampaigns = campaigns.data?.results ?? [];
  const activeCampaigns = allCampaigns.filter((c) => c.status === "Active");
  const approachingDeadline = activeCampaigns.filter((c) => {
    const days = (new Date(c.deadline).getTime() - now.getTime()) / 86400000;
    return days >= 0 && days <= 14;
  }).length;
  const totalAmountCollected = allCampaigns.reduce((sum, c) => sum + c.collectedAmount, 0);
  const allContributions = contributions.data?.results ?? [];
  const recentContributions = allContributions.slice(0, 5);
  const totalContributions = contributions.data?.totalCount ?? 0;
  const upcomingEvents = events.data?.totalCount ?? 0;
  const openJobs = jobs.data?.totalCount ?? 0;

  const allStoreOrders = storeOrders.data?.results ?? [];

  const monthNames = ["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"];
  const trendMonths = Array.from({ length: 6 }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() - (5 - i), 1);
    return { month: monthNames[d.getMonth()], key: `${d.getFullYear()}-${d.getMonth()}`, Contributions: 0, Store: 0 };
  });
  allContributions.filter((c) => c.status === "Successful").forEach((c) => {
    const d = new Date(c.confirmedAt ?? c.createdAt);
    const key = `${d.getFullYear()}-${d.getMonth()}`;
    const slot = trendMonths.find((m) => m.key === key);
    if (slot) slot.Contributions += c.amount;
  });
  allStoreOrders.forEach((o) => {
    const d = new Date(o.confirmedAt ?? o.createdAt);
    const key = `${d.getFullYear()}-${d.getMonth()}`;
    const slot = trendMonths.find((m) => m.key === key);
    if (slot) slot.Store += o.totalAmount;
  });
  const statusCounts = [
    ...allContributions.map((c) => c.status),
    ...allStoreOrders.map((o) => o.status),
  ].reduce<Record<string, number>>((acc, status) => {
    acc[status] = (acc[status] ?? 0) + 1;
    return acc;
  }, {});
  const statusPieData = Object.entries(statusCounts).map(([status, count]) => ({
    label: status,
    value: count,
    color: STATUS_COLORS[status] ?? "var(--muted-foreground)",
  }));
  const totalPayments = statusPieData.reduce((sum, d) => sum + d.value, 0);

  const firstName = user?.name?.trim()?.split(" ")[0] || "";
  const greeting = firstName ? `Good morning, ${firstName}` : "Welcome back";
  const todayLabel = new Intl.DateTimeFormat("en-GB", { weekday: "long", day: "numeric", month: "long", year: "numeric" }).format(now);

  return (
    <div className="p-4 sm:p-[26px] pt-[26px] max-w-[1240px] mx-auto">
      <div className="flex items-end justify-between gap-4 mb-5 flex-wrap">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">{greeting}</h1>
          <p className="mt-1.5 text-muted-foreground text-[13px]">{todayLabel} &middot; Institution operations overview</p>
        </div>
        <span className="shrink-0 whitespace-nowrap px-2.5 py-2 rounded-[6px] text-[12px] font-bold" style={{ background: "var(--brand-primary-light)", color: "var(--brand-primary-700, var(--color-text-info))" }}>
          All institution records
        </span>
      </div>

      {!isScopedAdmin && <SetupNudgeBanner />}

      {hasNoBatches && (
        <div className="flex items-center justify-between gap-3 rounded-lg border border-border/60 bg-muted/50 px-4 py-3 mb-4">
          <div>
            <p className="text-[13px] font-semibold">Set up your graduating-class batches</p>
            <p className="text-[12px] text-muted-foreground">Unlock year-group targeting and better member organization by defining your batches.</p>
          </div>
          <Link href="/batches">
            <Button size="sm">Set up batches</Button>
          </Link>
        </div>
      )}

      {isLoading ? (
        <div className="grid grid-cols-1 lg:grid-cols-[minmax(280px,1.3fr)_2fr] gap-3.5 items-stretch">
          <StatCardSkeleton variant="hero" />
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            {Array.from({ length: 3 }).map((_, i) => <StatCardSkeleton key={i} />)}
          </div>
        </div>
      ) : (
        // Money leads — one dominant "total collected" figure (the number an
        // institution admin cares about most, day to day) with the other three
        // metrics demoted to a supporting row, instead of four equal boxes.
        <div className="grid grid-cols-1 lg:grid-cols-[minmax(280px,1.3fr)_2fr] gap-3.5 items-stretch">
          <StatCard
            tone="primary"
            variant="hero"
            label="Total collected"
            value={formatCurrency(totalAmountCollected)}
            sub={<span style={{ color: "var(--success)" }}>{totalContributions} contributions</span>}
          />
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <StatCard
              tone="primary"
              label="Total members"
              value={totalMembers.toLocaleString()}
              sub={
                <span style={{ color: "var(--success)" }}>
                  +{Math.max(0, Math.round(totalMembers * 0.02))} this period &middot;{" "}
                  <Link href="/members" className="underline">{pendingApprovals} pending</Link>
                </span>
              }
            />
            <StatCard
              tone="accent"
              label="Active fundraisers & dues"
              value={activeCampaigns.length}
              sub={
                <span style={{ color: approachingDeadline > 0 ? "var(--warning)" : undefined }}>
                  {approachingDeadline} approaching deadline
                </span>
              }
            />
            <StatCard
              tone="accent"
              label="Upcoming events"
              value={upcomingEvents}
              sub={`+ ${openJobs} open job postings`}
            />
          </div>
        </div>
      )}

      {user?.role === "SuperAdmin" && <PayoutPanel />}

      <div className="grid grid-cols-1 lg:grid-cols-[1.2fr_.8fr] gap-3.5 mt-3.5">
        <section className="card p-[18px]">
          <h2 className="text-[15px] font-semibold m-0 mb-3.5">
            Revenue trend <span className="text-muted-foreground font-normal text-[13px]">Last 6 months, Contributions + Store</span>
          </h2>
          <TrendChart
            data={trendMonths}
            xKey="month"
            series={[
              { key: "Contributions", label: "Contributions", color: "var(--brand-primary-500, var(--primary))" },
              { key: "Store", label: "Store", color: "var(--brand-accent-500, var(--brand-accent))" },
            ]}
            variant="area"
            stacked
            height={150}
            loading={isLoading}
            emptyMessage="No payments recorded yet this period"
            valueFormatter={(v) => formatCurrency(v)}
          />
        </section>

        <section className="card p-[18px]">
          <h2 className="text-[15px] font-semibold m-0 mb-3.5">Payment status mix</h2>
          <DonutChart
            data={statusPieData}
            centerValue={totalPayments || undefined}
            centerLabel="payments"
            height={150}
            loading={isLoading}
            emptyMessage="No payments yet"
            valueFormatter={(v) => v.toLocaleString()}
          />
        </section>
      </div>

      <div className="grid grid-cols-1 gap-3.5 mt-3.5">
        {/* Emphasis border — this card asks for action, the chart cards above only inform */}
        <section className="card p-[18px]" style={{ borderColor: pendingApprovals > 0 ? "var(--border-emphasis)" : undefined }}>
          <h2 className="text-[15px] font-semibold m-0 mb-3.5 flex items-center justify-between">
            Pending approvals
            <Link href="/members" className="text-[12px] font-normal text-muted-foreground hover:text-accent">View queue &rarr;</Link>
          </h2>
          {isLoading ? (
            <div className="space-y-3 py-1">
              {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-10" />)}
            </div>
          ) : (
            <>
              {(membersPending.data?.results ?? []).slice(0, 3).map((m) => (
                <div key={m.id} className="flex items-center justify-between gap-2.5 py-3 border-t border-border first:border-0">
                  <div className="min-w-0">
                    <b className="text-[13px]">{m.firstName} {m.lastName}</b>
                    <br />
                    <small className="text-muted-foreground text-[12px]">
                      {m.graduationYear ? `Class of ${m.graduationYear}` : "Class year unknown"} &middot; {m.isEmailVerified ? "email verified" : "email unverified"}
                    </small>
                  </div>
                  <Badge variant="warning">Pending</Badge>
                </div>
              ))}
              {pendingApprovals === 0 && (
                <p className="text-[13px] text-muted-foreground text-center py-6">No pending approvals</p>
              )}
            </>
          )}
        </section>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-[1.2fr_.8fr] gap-3.5 mt-3.5">
        <section className="card p-[18px]">
          <h2 className="text-[15px] font-semibold m-0 mb-3.5">Active fundraisers &amp; dues</h2>
          {isLoading ? (
            <div className="space-y-3 py-1">
              {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-14" />)}
            </div>
          ) : (
            <>
              {activeCampaigns.length === 0 && <p className="text-[13px] text-muted-foreground py-4">No active fundraisers or dues.</p>}
              {activeCampaigns.map((c) => {
                const isMembership = !!c.isMembershipCampaign;
                const pct = isMembership && c.totalEligibleMembers
                  ? Math.round((c.paidCount / c.totalEligibleMembers) * 100)
                  : c.targetAmount > 0 ? Math.round((c.collectedAmount / c.targetAmount) * 100) : 0;
                return (
                  <div key={c.id} className="flex items-start justify-between gap-3 py-3 border-t border-border first:border-0">
                    <div className="min-w-0 flex-1">
                      <b className="text-[13px] break-words">{c.title}</b>
                      <br />
                      <small className="text-muted-foreground text-[12px]">
                        Deadline {formatDate(c.deadline)} &middot; {c.yearGroups?.length ? `Classes ${c.yearGroups.join(", ")}` : "All members"}
                      </small>
                      <Progress value={pct} className="h-[7px] mt-2" />
                    </div>
                    <b className="text-[13px] shrink-0 tabular-nums">{pct}%</b>
                  </div>
                );
              })}
            </>
          )}
        </section>

        <section className="card overflow-hidden">
          <h2 className="text-[15px] font-semibold m-0 p-[18px] pb-0">Recent contributions</h2>
          {isLoading ? (
            <div className="space-y-3 p-[18px]">
              {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-8" />)}
            </div>
          ) : recentContributions.length === 0 ? (
            <p className="text-[13px] text-muted-foreground text-center py-6">No contributions yet</p>
          ) : (
            <table className="w-full text-[13px] mt-2">
              <thead>
                <tr>
                  <th className="text-left text-[11px] uppercase text-muted-foreground font-semibold px-[18px] py-2.5 border-t border-border">Member</th>
                  <th className="text-left text-[11px] uppercase text-muted-foreground font-semibold px-2 py-2.5 border-t border-border">Amount</th>
                  <th className="text-left text-[11px] uppercase text-muted-foreground font-semibold px-[18px] py-2.5 border-t border-border">State</th>
                </tr>
              </thead>
              <tbody>
                {recentContributions.map((c) => (
                  <tr key={c.id}>
                    <td className="px-[18px] py-2.5 border-t border-border font-medium">{c.memberName ?? "Unknown"}</td>
                    <td className="px-2 py-2.5 border-t border-border tabular-nums">{formatCurrency(c.amount)}</td>
                    <td className="px-[18px] py-2.5 border-t border-border">
                      <Badge variant={c.status === "Successful" ? "success" : c.status === "Rejected" ? "destructive" : "warning"}>{c.status}</Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      </div>
    </div>
  );
}
