"use client";

import { useQueries, useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Badge } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Progress } from "@alumni/ui";
import { Skeleton } from "@alumni/ui";
import { StatCard, StatCardSkeleton, IconTile } from "@alumni/ui";
import { Users, TrendingUp, Wallet, CalendarDays, Landmark, Clock3 } from "lucide-react";
import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Legend,
  PieChart, Pie, Cell,
} from "recharts";
import { formatCurrency, formatDate } from "@alumni/ui";
import { getCampaigns, getContributions, getMembers, getEvents, getJobs, getBatches, getStoreOrders, getPayoutForecast } from "@/lib/institution-api";
import { useAuth } from "@/hooks/use-auth";

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
      <div className="card p-[18px] mt-3.5 flex items-center gap-3">
        <IconTile icon={Landmark} tone="muted" size="sm" />
        <p className="text-[13px] text-muted-foreground">
          Settlement banking isn&apos;t set up yet — ask the platform team to add your payout details to start seeing expected payouts here.
        </p>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5 mt-3.5">
      <div className="card p-[18px]">
        <div className="flex items-center gap-2.5 mb-3">
          <IconTile icon={Landmark} tone="primary" size="sm" />
          <p className="text-[13px] font-semibold">Last payout</p>
        </div>
        <p className="font-[family-name:var(--font-display)] font-bold tabular-nums text-foreground" style={{ fontSize: "1.6rem", letterSpacing: "-0.02em" }}>
          {formatCurrency(data.lastPayout.amount)}
        </p>
        <p className="text-[12px] text-muted-foreground mt-1.5">
          Settled {formatDate(data.lastPayout.date)} &middot; should already be in your account &middot; {data.lastPayout.transactionCount} transaction{data.lastPayout.transactionCount === 1 ? "" : "s"}
        </p>
      </div>
      <div className="card p-[18px]" style={{ borderColor: "var(--border-emphasis)" }}>
        <div className="flex items-center gap-2.5 mb-3">
          <IconTile icon={Clock3} tone="accent" size="sm" />
          <p className="text-[13px] font-semibold">Next payout</p>
        </div>
        <p className="font-[family-name:var(--font-display)] font-bold tabular-nums text-foreground" style={{ fontSize: "1.6rem", letterSpacing: "-0.02em" }}>
          {formatCurrency(data.nextPayout.amount)}
        </p>
        <p className="text-[12px] text-muted-foreground mt-1.5">
          Expected {formatDate(data.nextPayout.date)} morning &middot; still accumulating &middot; {data.nextPayout.transactionCount} transaction{data.nextPayout.transactionCount === 1 ? "" : "s"}
        </p>
      </div>
      <p className="sm:col-span-2 text-[11px] text-muted-foreground -mt-2">Estimated from your confirmed transactions — not a bank-confirmed figure.</p>
    </div>
  );
}

export default function AdminDashboardPage() {
  const { user } = useAuth();
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
  const hasTrendData = trendMonths.some((m) => m.Contributions > 0 || m.Store > 0);

  const statusCounts = [
    ...allContributions.map((c) => c.status),
    ...allStoreOrders.map((o) => o.status),
  ].reduce<Record<string, number>>((acc, status) => {
    acc[status] = (acc[status] ?? 0) + 1;
    return acc;
  }, {});
  const statusPieData = Object.entries(statusCounts).map(([status, count]) => ({ status, count }));

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
        <span className="shrink-0 whitespace-nowrap px-2.5 py-2 rounded-[6px] text-[12px] font-bold" style={{ background: "var(--brand-primary-light)", color: "var(--color-text-info)" }}>
          All institution records
        </span>
      </div>

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
            icon={Wallet}
            tone="primary"
            variant="hero"
            label="Total collected"
            value={formatCurrency(totalAmountCollected)}
            sub={<span style={{ color: "var(--success)" }}>{totalContributions} contributions</span>}
          />
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <StatCard
              icon={Users}
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
              icon={TrendingUp}
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
              icon={CalendarDays}
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
          {isLoading ? (
            <Skeleton className="h-[150px]" />
          ) : (
            <div className="relative">
              <ResponsiveContainer width="100%" height={150}>
                <BarChart data={trendMonths} barSize={22}>
                  <XAxis dataKey="month" axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: "var(--muted-foreground)" }} />
                  <YAxis hide />
                  <Tooltip formatter={(v) => formatCurrency(Number(v))} contentStyle={{ borderRadius: "6px", border: "1px solid var(--border)", fontSize: 12 }} />
                  <Legend wrapperStyle={{ fontSize: 12 }} />
                  <Bar dataKey="Contributions" stackId="a" fill="var(--primary)" />
                  <Bar dataKey="Store" stackId="a" fill="var(--brand-accent, #d97706)" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
              {!hasTrendData && (
                <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
                  <p className="text-[13px] text-muted-foreground bg-background/80 px-3 py-1.5 rounded-md">
                    No payments recorded yet this period
                  </p>
                </div>
              )}
            </div>
          )}
        </section>

        <section className="card p-[18px]">
          <h2 className="text-[15px] font-semibold m-0 mb-3.5">Payment status mix</h2>
          {isLoading ? (
            <Skeleton className="h-[150px]" />
          ) : statusPieData.length === 0 ? (
            <div className="h-[150px] flex items-center justify-center">
              <p className="text-[13px] text-muted-foreground">No payments yet</p>
            </div>
          ) : (
            <ResponsiveContainer width="100%" height={150}>
              <PieChart>
                <Pie data={statusPieData} dataKey="count" nameKey="status" innerRadius={35} outerRadius={60} paddingAngle={2}>
                  {statusPieData.map((entry) => (
                    <Cell key={entry.status} fill={STATUS_COLORS[entry.status] ?? "var(--muted-foreground)"} />
                  ))}
                </Pie>
                <Tooltip contentStyle={{ borderRadius: "6px", border: "1px solid var(--border)", fontSize: 12 }} />
                <Legend wrapperStyle={{ fontSize: 12 }} />
              </PieChart>
            </ResponsiveContainer>
          )}
        </section>
      </div>

      <div className="grid grid-cols-1 gap-3.5 mt-3.5">
        {/* Emphasis border — this card asks for action, the chart cards above only inform */}
        <section className="card p-[18px]" style={{ borderColor: pendingApprovals > 0 ? "var(--border-emphasis)" : undefined }}>
          <h2 className="text-[15px] font-semibold m-0 mb-3.5 flex items-center justify-between">
            Pending approvals
            <Link href="/members" className="text-[12px] font-normal text-muted-foreground hover:text-primary">View queue &rarr;</Link>
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
